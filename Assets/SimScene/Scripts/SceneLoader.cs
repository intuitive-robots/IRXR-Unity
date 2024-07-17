using UnityEngine;
using System; // Don't include System.Diagnostics, Debug becomes disambiguous
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Newtonsoft.Json;


public class SceneLoader : MonoBehaviour {

  private Action _updateAction;
  public Action OnSceneLoaded;
  private RequestSender reqSender;

  private GameObject _simSceneObj;
  private SimScene _simScene;
  private Dictionary<string, Transform> _simObjTrans = new Dictionary<string, Transform>();

  private Dictionary<string, SimMesh> _simMeshes;
  private Dictionary<string, SimMaterial> _simMaterials;
  private Dictionary<string, SimTexture> _simTextures;
  // TODO: The cache meshes and textures probably is not necessary
  private Dictionary<string, Mesh> _cachedMeshes;
  private Dictionary<string, Texture> _cachedTextures;

  void Awake() {
    _simMeshes = new();
    _simMaterials = new();
    _simTextures = new();

    _cachedTextures = new();
    _cachedMeshes = new();
  }

  void Start() {
    GameObject client = IRXRNetManager.Instance.gameObject;
    reqSender = client.GetComponent<RequestSender>();
    client.GetComponent<IRXRNetManager>().OnNewServerDiscovered += ClearScene;
    if (reqSender.isServiceConnected) DownloadScene();
    reqSender.OnServiceConnection += DownloadScene;
    _updateAction = () => { };
    OnSceneLoaded += () => Debug.Log("Scene Loaded");
  }

  void BuildScene() {
    var local_watch = new System.Diagnostics.Stopwatch();
    local_watch.Start();
    Debug.Log("Building Scene");
    ProcessAsset(); // Compile Meshes, textures and Materials can only be done on the main thread
    _simSceneObj = CreateObject(gameObject.transform, _simScene.root);
    SceneController sceneController = _simSceneObj.AddComponent<SceneController>();
    sceneController.StartUpdate(_simObjTrans);
    local_watch.Stop();
    Debug.Log($"Building Scene in {local_watch.ElapsedMilliseconds} ms");
    _updateAction -= BuildScene;
    OnSceneLoaded.Invoke();
  }

  void DownloadScene() {
    // Don't include System.Diagnostics, Debug becomes disambiguous
    var local_watch = new System.Diagnostics.Stopwatch();
    local_watch.Start();
    Debug.Log("Downloading Scene");
    string asset_info = reqSender.RequestString("SCENE");
    _simScene = JsonConvert.DeserializeObject<SimScene>(asset_info);
    DownloadAssets(_simScene);
    local_watch.Stop();
    Debug.Log($"Downloaded Scene in {local_watch.ElapsedMilliseconds} ms");
    _updateAction += BuildScene;
  }


  public void DownloadAssets(SimScene scene) {
    _simMeshes.Clear();
    _simMaterials.Clear();
    _simTextures.Clear();
    scene.meshes.ForEach(DownloadMesh);
    scene.textures.ForEach(DownloadTexture);
  }

  private void DownloadMesh(SimMesh mesh) {

    if (_cachedMeshes.TryGetValue(mesh.dataHash, out Mesh cached)) {
      mesh.compiledMesh = cached;
      _simMeshes.Add(mesh.id, mesh);
      return;
    }

    Span<byte> data = reqSender.RequestBytes("ASSET:" + mesh.dataHash).ToArray();
    mesh.rawData = new SimMeshData
    {
      indices = MemoryMarshal.Cast<byte, int>(data.Slice(mesh.indicesLayout[0], mesh.indicesLayout[1] * sizeof(int))).ToArray(),
      vertices = MemoryMarshal.Cast<byte, Vector3>(data.Slice(mesh.verticesLayout[0], mesh.verticesLayout[1] * sizeof(float))).ToArray(),
      normals = MemoryMarshal.Cast<byte, Vector3>(data.Slice(mesh.normalsLayout[0], mesh.normalsLayout[1] * sizeof(float))).ToArray(),
      // uv is not implemented yet
      // uvs = MemoryMarshal.Cast<byte, Vector2>(data.Slice(mesh.uvLayout[0], mesh.uvLayout[1] * sizeof(float))).ToArray(),
    };
  }

  private void DownloadTexture(SimTexture texture) {
    if (_cachedTextures.TryGetValue(texture.dataHash, out Texture cached)){
      texture.compiledTexture = cached;
    } else {
      texture.textureData = reqSender.RequestBytes("ASSET:" + texture.dataHash).ToArray();
    }

    _simTextures.Add(texture.id, texture);
  }


  void Update() {
    _updateAction();
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
          SimMesh asset = _simMeshes[visual.mesh];
          visualObj = new GameObject(asset.id, typeof(MeshFilter), typeof(MeshRenderer));
          visualObj.GetComponent<MeshFilter>().mesh = asset.compiledMesh; 
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
      if (visual.material != null) {
        renderer.material = GetMaterial(visual.material).compiledMaterial;
      }
      else {
        renderer.material = new Material(Shader.Find("Standard"));
        renderer.material.SetColor("_Color", new Color(visual.color[0], visual.color[1], visual.color[2], visual.color[3]));
      }

      visualObj.transform.SetParent(VisualContainer.transform, false);
      ApplyTransform(visualObj.transform, visual.trans);
    }
    
    body.children.ForEach(body => CreateObject(bodyRoot.transform, body));
    _simObjTrans.Add(body.name, bodyRoot.transform);
    return bodyRoot;
  }

  void ClearScene() {
    SceneController sceneController = GetComponent<SceneController>();
    if (sceneController != null)
    {
        Destroy(sceneController);
    }
    if (_simSceneObj != null) Destroy(_simSceneObj);
    _simObjTrans.Clear();
    _simMeshes.Clear();
    _simMaterials.Clear();
    _simTextures.Clear();

    _cachedTextures.Clear();
    _cachedMeshes.Clear();
  }

  public SimMesh GetMesh(string id) => _simMeshes[id];
  public SimTexture GetTexture(string id) => _simTextures[id];
  public SimMaterial GetMaterial(string id) => _simMaterials[id];
  public void ProcessAsset() {
    _simScene.meshes.ForEach(ProcessMesh);
    _simScene.materials.ForEach(ProcessMaterial);
    _simScene.textures.ForEach(ProcessTexture);  
  }

  public void ProcessMesh(SimAsset asset) {
    SimMesh mesh = (SimMesh)asset;

    mesh.compiledMesh = new Mesh{
      name = mesh.id,
      vertices = mesh.rawData.vertices,
      normals = mesh.rawData.normals,
      triangles = mesh.rawData.indices,
      uv = mesh.rawData.uvs,
    };
    _cachedMeshes[mesh.dataHash] = mesh.compiledMesh;
    _simMeshes[mesh.id] = mesh;
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
      SimTexture texture = _simTextures[material.texture];
      if (texture.compiledTexture == null) ProcessTexture(texture);
      mat.mainTexture = texture.compiledTexture;
      mat.mainTextureScale = new Vector2(material.textureSize[0], material.textureSize[1]);
    }

    material.compiledMaterial = mat;
    _simMaterials[material.id] = material;
  }

  public void ProcessTexture(SimAsset asset) {
    SimTexture simTexture = (SimTexture)asset;

    var tex = new Texture2D(simTexture.width, simTexture.height, TextureFormat.RGBA32, false);
    tex.LoadRawTextureData(simTexture.textureData);
    tex.Apply();

    simTexture.compiledTexture = tex;
    _cachedTextures[simTexture.dataHash] = simTexture.compiledTexture;
  }

}