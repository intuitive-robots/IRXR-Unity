using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using UnityEngine;

public class SimAsset {
  public string Tag;

  public string dataID;
}

public class SimMeshData {
  public int[] indices;

  public Vector3[] vertices;

  public Vector3[] normals;

  public Vector2[] uvs;

}

public class SimMesh : SimAsset {
  public List<int> indicesLayout;

  public List<int> verticesLayout;

  public List<int> normalsLayout;

  public List<int> uvLayout;

  [JsonIgnore]
  public Mesh compiledMesh;

  
  [JsonIgnore]
  public SimMeshData rawData;
}



public class SimMaterial : SimAsset {
  public List<float> color;
  public List<float> emissionColor;
  public float specular;
  public float shininess;
  public float reflectance;
  public string texture;
  public List<float> textureSize;

  [JsonIgnore]
  public Material compiledMaterial;
}

public class SimTexture  : SimAsset { 

  public int width;

  public int height;

  public string texType;
  
  [JsonIgnore]
  public byte[] textureData;

  [JsonIgnore]
  public Texture compiledTexture;
}

// TODO: Merge the AssetHandler and the SceneLoader because AssetHandler has no update function
public class AssetHandler : MonoBehaviour
{
  [SerializeField] private ServiceConnection _serviceConnection;

  private Dictionary<string, SimMesh> _meshes;
  private Dictionary<string, SimMaterial> _materials;
  private Dictionary<string, SimTexture> _textures;
  
  private Dictionary<string, Mesh> _cachedMeshes;
  private Dictionary<string, Texture> _cachedtextures;

  private List<(SimAsset, Action<SimAsset>)> _todo;

  void Awake() {
    _meshes = new();
    _materials = new();
    _textures = new();
    _todo = new();

    _cachedtextures = new();
    _cachedMeshes = new();
  }
  public void LoadAssets(SimScene assets) {

    _meshes.Clear();
    assets.meshes.ForEach(LoadMesh);
    
    _materials.Clear();
    assets.materials.ForEach(LoadMaterial);
    
    _textures.Clear();
    assets.textures.ForEach(Loadtexture);
  }

  public SimMesh GetMesh(string tag) => _meshes[tag];
  public SimTexture Gettexture(string tag) => _textures[tag];
  public SimMaterial GetMaterial(string tag) => _materials[tag];
  public void Process() => _todo.ForEach(entry => entry.Item2.Invoke(entry.Item1));

  
  private void LoadMesh(SimMesh mesh) {

    if (_cachedMeshes.TryGetValue(mesh.dataID, out Mesh cached)) {
      mesh.compiledMesh = cached;
      _meshes.Add(mesh.Tag, mesh);
      return;
    }

    Span<byte> data = _serviceConnection.RequestBytes("ASSET:" + mesh.dataID).ToArray();

    mesh.rawData = new SimMeshData
    {
      indices = MemoryMarshal.Cast<byte, int>(data.Slice(mesh.indicesLayout[0], mesh.indicesLayout[1] * sizeof(int))).ToArray(),
      vertices = MemoryMarshal.Cast<byte, Vector3>(data.Slice(mesh.verticesLayout[0], mesh.verticesLayout[1] * sizeof(float))).ToArray(),
      normals = MemoryMarshal.Cast<byte, Vector3>(data.Slice(mesh.normalsLayout[0], mesh.normalsLayout[1] * sizeof(float))).ToArray(), 
      uvs = MemoryMarshal.Cast<byte, Vector2>(data.Slice(mesh.uvLayout[0], mesh.uvLayout[1] * sizeof(float))).ToArray()
    };
    
    _todo.Add((mesh, ProcessMesh));
  }
  

  private void LoadMaterial(SimMaterial material) => _todo.Add((material, ProcessMaterial));

  private void Loadtexture(SimTexture texture) {
    if (_cachedtextures.TryGetValue(texture.dataID, out Texture cached)){
      texture.compiledTexture = cached;
    } else {
      texture.textureData = _serviceConnection.RequestBytes("ASSET:" + texture.dataID).ToArray();
    }

    _textures.Add(texture.Tag, texture);
  }

  public void ProcessMesh(SimAsset asset) {
    SimMesh mesh = (SimMesh)asset;

    mesh.compiledMesh = new Mesh{
      name = mesh.Tag,
      vertices = mesh.rawData.vertices,
      normals = mesh.rawData.normals,
      triangles = mesh.rawData.indices,
      uv = mesh.rawData.uvs,
    };
    // TODO: Check if we need it
    // mesh.compiledMesh.RecalculateNormals();
    
    _cachedMeshes[mesh.dataID] = mesh.compiledMesh;
    _meshes[mesh.Tag] = mesh;
  }

  public void ProcessMaterial(SimAsset asset) {
    SimMaterial material = (SimMaterial)asset;

    Material mat = new Material(Shader.Find("Standard"));

    mat.SetColor("_Color", new Color(material.color[0], material.color[1], material.color[2], material.color[3]));
    mat.SetColor("_emissionColor", new Color(material.emissionColor[0], material.emissionColor[1], material.emissionColor[2], material.emissionColor[3]));
    mat.SetFloat("_specularHighlights", material.specular);
    mat.SetFloat("_Smoothness", material.shininess);
    mat.SetFloat("_GlossyReflections", material.reflectance);


    if (material.texture != null) {
      SimTexture texture = _textures[material.texture];
      if (texture.compiledTexture == null) Processtexture(texture);
      mat.mainTexture = texture.compiledTexture;
      mat.mainTextureScale = new Vector2(material.textureSize[0], material.textureSize[1]);
    }

    material.compiledMaterial = mat;
    _materials[material.Tag] = material;
  }

  public void Processtexture(SimAsset asset) {
    SimTexture simTexture = (SimTexture)asset;

    var tex = new Texture2D(simTexture.width, simTexture.height, TextureFormat.RGBA32, false);
    tex.LoadRawTextureData(simTexture.textureData);
    tex.Apply();

    simTexture.compiledTexture = tex;
    _cachedtextures[simTexture.dataID] = simTexture.compiledTexture;
  }
}