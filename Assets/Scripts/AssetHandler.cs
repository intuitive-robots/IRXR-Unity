using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using UnityEngine;

public class SimAsset {
  public string Tag;

  public string DataID;
}

public class SimMeshData {
  public int[] Indices;

  public Vector3[] Vertices;

  public Vector3[] Normals;

  public Vector2[] UVs;

}

public class SimMesh : SimAsset {
  public List<int> IndicesLayout;

  public List<int> VerticesLayout;

  public List<int> NormalsLayout;

  public List<int> UvLayout;

  [JsonIgnore]
  public Mesh compiledMesh;

  
  [JsonIgnore]
  public SimMeshData rawData;
}



public class SimMaterial : SimAsset {
  public List<float> Color;
  public List<float> EmissionColor;
  public float Specular;
  public float Shininess;
  public float Reflectance;
  public string Texture;
  public List<float> TexSize;

  [JsonIgnore]
  public Material compiledMaterial;
}

public class SimTexture  : SimAsset { 

  public int Width;

  public int Height;

  public string TexType;
  
  [JsonIgnore]
  public byte[] Data;

  [JsonIgnore]
  public Texture compiledTexture;
}


public class AssetHandler : MonoBehaviour
{
  [SerializeField] private ServiceConnection _serviceConnection;

  private Dictionary<string, SimMesh> _meshes;
  private Dictionary<string, SimMaterial> _materials;
  private Dictionary<string, SimTexture> _textures;
  
  private Dictionary<string, Mesh> _cachedMeshes;
  private Dictionary<string, Texture> _cachedTextures;

  private List<(SimAsset, Action<SimAsset>)> _todo;

  void Awake() {
    _meshes = new();
    _materials = new();
    _textures = new();
    _todo = new();

    _cachedTextures = new();
    _cachedMeshes = new();
  }
  public void LoadAssets(SimScene assets) {

    _meshes.Clear();
    assets.Meshes.ForEach(LoadMesh);
    
    _materials.Clear();
    assets.Materials.ForEach(LoadMaterial);
    
    _textures.Clear();
    assets.Textures.ForEach(LoadTexture);
  }

  public SimMesh GetMesh(string tag) => _meshes[tag];
  public SimTexture GetTexture(string tag) => _textures[tag];
  public SimMaterial GetMaterial(string tag) => _materials[tag];
  public void Process() => _todo.ForEach(entry => entry.Item2.Invoke(entry.Item1));

  
  private void LoadMesh(SimMesh mesh) {

    if (_cachedMeshes.TryGetValue(mesh.DataID, out Mesh cached)) {
      mesh.compiledMesh = cached;
      _meshes.Add(mesh.Tag, mesh);
      return;
    }


    Span<byte> data = _serviceConnection.request_bytes("ASSET_DATA:" + mesh.DataID).ToArray();

    mesh.rawData = new SimMeshData
    {
      Indices = MemoryMarshal.Cast<byte, int>(data.Slice(mesh.IndicesLayout[0], mesh.IndicesLayout[1] * sizeof(int))).ToArray(),
      Vertices = MemoryMarshal.Cast<byte, Vector3>(data.Slice(mesh.VerticesLayout[0], mesh.VerticesLayout[1] * sizeof(float))).ToArray(),
      Normals = MemoryMarshal.Cast<byte, Vector3>(data.Slice(mesh.NormalsLayout[0], mesh.NormalsLayout[1] * sizeof(float))).ToArray(), 
      UVs = MemoryMarshal.Cast<byte, Vector2>(data.Slice(mesh.UvLayout[0], mesh.UvLayout[1] * sizeof(float))).ToArray()
    };
    
    _todo.Add((mesh, ProcessMesh));
  }
  

  private void LoadMaterial(SimMaterial material) => _todo.Add((material, ProcessMaterial));

  private void LoadTexture(SimTexture texture) {
    if (_cachedTextures.TryGetValue(texture.DataID, out Texture cached)){
      texture.compiledTexture = cached;
    } else {
      texture.Data = _serviceConnection.request_bytes("ASSET_DATA:" + texture.DataID).ToArray();
    }

    _textures.Add(texture.Tag, texture);
  }

  public void ProcessMesh(SimAsset asset) {
    SimMesh mesh = (SimMesh)asset;

    mesh.compiledMesh = new Mesh{
      name = mesh.Tag,
      vertices = mesh.rawData.Vertices,
      normals = mesh.rawData.Normals,
      triangles = mesh.rawData.Indices,
      uv = mesh.rawData.UVs,
    };
    
    _cachedMeshes[mesh.DataID] = mesh.compiledMesh;
    _meshes[mesh.Tag] = mesh;
  }
  public void ProcessMaterial(SimAsset asset) {
    SimMaterial material = (SimMaterial)asset;

    Material mat = new Material(Shader.Find("Standard"));

    mat.SetColor("_Color", new Color(material.Color[0], material.Color[1], material.Color[2], material.Color[3]));
    mat.SetColor("_EmissionColor", new Color(material.EmissionColor[0], material.EmissionColor[1], material.EmissionColor[2], material.EmissionColor[3]));
    mat.SetFloat("_SpecularHighlights", material.Specular);
    mat.SetFloat("_Smoothness", material.Shininess);
    mat.SetFloat("_GlossyReflections", material.Reflectance);


    if (material.Texture != null) {
      SimTexture texture = _textures[material.Texture];
      if (texture.compiledTexture == null) ProcessTexture(texture);
      mat.mainTexture = texture.compiledTexture;
      mat.mainTextureScale = new Vector2(material.TexSize[0], material.TexSize[1]);
    }

    material.compiledMaterial = mat;
    _materials[material.Tag] = material;
  }

  public void ProcessTexture(SimAsset asset) {
    SimTexture texture = (SimTexture)asset;

    var tex = new Texture2D(texture.Width, texture.Height, TextureFormat.RGBA32, false);
    tex.LoadRawTextureData(texture.Data);
    tex.Apply();

    texture.compiledTexture = tex;
    _cachedTextures[texture.DataID] = texture.compiledTexture;
  }
}