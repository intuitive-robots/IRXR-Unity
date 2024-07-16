using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

// TODO: Singleton Pattern
public class SceneLoader : MonoBehaviour {

  [SerializeField] ServiceConnection _connection;

  [SerializeField] StreamingConnection _streamingConnection;

  private GameObject _simSceneObj;
  private SimScene _simScene;
  private Dictionary<string, Transform> _simObjTrans = new Dictionary<string, Transform>();

  private System.Diagnostics.Stopwatch _watch;
 
  void Start() {
    _connection.OnServiceConnection += LoadScene;
  }


  void LoadScene() {
    Debug.Log("Loading Scene");
    var local_watch = new System.Diagnostics.Stopwatch(); // Don't include System.Diagnostics, Debug becomes disambiguous
    local_watch.Start();
    string asset_info = _connection.RequestString("SCENE");
    _simScene = JsonConvert.DeserializeObject<SimScene>(asset_info);
    _assetHandler.LoadAssets(_simScene);
    _watch = local_watch;
    Debug.Log("Scene has been loaded");
  }

  void Update() {

    if (_watch == null) return;

    // TODO: Do it in the background
    _assetHandler.Process(); // Compile Meshes, textures and Materials can only be done on the main thread
    BuildObjects(); // Build the scene objects, also only on the main thread

    _watch.Stop();
    Debug.Log($"Loaded Scene in {_watch.ElapsedMilliseconds} ms");
    _watch = null;
  }

  void BuildObjects() {
    ClearScene();

    _simSceneObj = CreateObject(gameObject.transform, _simScene.root);


    for body in _simScene.bodies {
      CreateObject(_simSceneObj.transform, body);
    }

    SceneController sceneController = _simSceneObj.AddComponent<SceneController>();
    sceneController.StartUpdate(_simObjTrans);
    _streamingConnection.OnMessage += sceneController.listener;
  }


  void ApplyTransform(Transform utransform, SimTransform trans) {
    utransform.localPosition = trans.GetPos();
    utransform.localRotation = trans.GetRot();
    utransform.localScale = trans.GetScale();
  }


  GameObject CreateObject(Transform root, SimBody body, string name = null) {
    GameObject bodyRoot = new GameObject(name != null ? name : body.name);
    if (root != null)  bodyRoot.transform.SetParent(root, false);
    ApplyTransform(bodyRoot.transform, body.trans);

    GameObject VisualContainer = new GameObject("Visuals");
    VisualContainer.transform.SetParent(bodyRoot.transform, false);

    foreach (SimVisual visual in body.visuals) { 
      GameObject visualObj;
      switch (visual.type) {
        case "MESH": {
          SimMesh meshData = _simScene.meshes[visual.mesh];
          visualObj = new GameObject(asset.Tag, typeof(MeshFilter), typeof(MeshRenderer));
          visualObj.GetComponent<MeshFilter>().mesh = meshData.compiledMesh;
          break;
        }
        case "CUBE":
          visualObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
          break;
        case "PLANE":
          visualObj = GameObject.CreatePrimitive(PrimitiveType.Plane);    
          break;
        case "CYLINDER":
          visualObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
          break;
        case "CAPSULE":
          visualObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
          break;
        case "SPHERE":
          visualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
          break;
        default:
          Debug.LogWarning("Invalid visual, " + visual.type + body.name);
          visualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
          break;
      }

      Renderer renderer = visualObj.GetComponent<Renderer>();
      if (visual.material != null) 
        renderer.material = _assetHandler.GetMaterial(visual.material).compiledMaterial;
      else {
        renderer.material = new Material(Shader.Find("Standard"));
        renderer.material.SetColor("_Color", new Color(visual.color[0], visual.color[1], visual.color[2], visual.color[3]));
      }

      visualObj.transform.SetParent(VisualContainer.transform, false);
      ApplyTransform(visualObj.transform, visual.trans);
    }
    
    // body.children.ForEach(body => CreateObject(bodyRoot.transform, body));
    _simObjTrans.Add(body.name, bodyRoot.transform);
    return bodyRoot;
  }

  void ClearScene() {
    if (_simSceneObj != null) Destroy(_simSceneObj);
    _simObjTrans.Clear();
  }

}

// TODO: Merge the AssetHandler and the SceneLoader because AssetHandler has no update function
public class AssetHandler : MonoBehaviour
{
  [SerializeField] private ServiceConnection _serviceConnection;

  // private Dictionary<string, SimMesh> _meshes;
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
  public void RetrieveAssetsByte(SimScene assets) {

    _meshes.Clear();
    _materials.Clear();
    _textures.Clear();

    assets.meshes.ForEach(LoadMesh);
    assets.materials.ForEach(LoadMaterial);
    assets.textures.ForEach(Loadtexture);
  }

  // public SimMesh GetMesh(string tag) => _meshes[tag];
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