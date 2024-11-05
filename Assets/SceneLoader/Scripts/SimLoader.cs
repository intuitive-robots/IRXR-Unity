using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using UnityEngine.Networking;

public class SceneLoader : MonoBehaviour {

  private Action updateAction;
  public Action OnSceneLoaded;
  public Action OnSceneCleared;
  private IRXRNetManager _netManager;
  private GameObject _simSceneObj;
  private SimScene _simScene;
  private Dictionary<string, Transform> _simObjTrans = new Dictionary<string, Transform>();

  private Dictionary<string, SimMesh> _simMeshes;
  private Dictionary<string, SimMaterial> _simMaterials;
  private Dictionary<string, SimTexture> _simTextures;
  private Dictionary<string, Mesh> _cachedMeshes;
  private Dictionary<string, Texture> _cachedTextures;

  private int _coroutinesCompleted = 0;

  void Awake() {
    _simMeshes = new();
    _simMaterials = new();
    _simTextures = new();

    _cachedTextures = new();
    _cachedMeshes = new();
  }

  void Start() {
    _netManager = IRXRNetManager.Instance;
    _netManager.OnDisconnected += ClearScene;
    _netManager.OnConnectionStart += DownloadScene;
    updateAction = () => { };
    OnSceneLoaded += () => Debug.Log("Scene Loaded");
    OnSceneCleared += () => Debug.Log("Scene Cleared");
  }

  void BuildScene() {
    // Don't include System.Diagnostics, Debug becomes disambiguous
    var local_watch = new System.Diagnostics.Stopwatch();
    local_watch.Start();
    Debug.Log("Building Scene");
    ProcessAsset(); // Compile Meshes, textures and Materials can only be done on the main thread
    _simSceneObj = CreateObject(gameObject.transform, _simScene.root);
    local_watch.Stop();
    Debug.Log($"Building Scene in {local_watch.ElapsedMilliseconds} ms");
    updateAction -= BuildScene;
    OnSceneLoaded.Invoke();
  }

  void DownloadScene() {
    if (!_netManager.CheckServerService("Scene")) {
      Debug.LogWarning("Scene Service is not found");
      return;
    }
    // float downloadStartTime = Time.realtimeSinceStartup;
    string asset_info = _netManager.RequestString("Scene");
    if (asset_info == "Invild Service") {
      Debug.LogWarning("Invalid Service");
      return;
    }
    _simScene = JsonConvert.DeserializeObject<SimScene>(asset_info);
    DownloadAssets(_simScene);
    // float timeSpent = (Time.realtimeSinceStartup - downloadStartTime) * 1000;
    // Debug.Log($"Downloaded Scene in {(int)timeSpent} ms");
    // updateAction += BuildScene;
  }

  public void DownloadAssets(SimScene scene) {
    _simMeshes.Clear();
    _simMaterials.Clear();
    _simTextures.Clear();
    _coroutinesCompleted = 0;
    // scene.meshes.ForEach(DownloadMesh);
    // scene.textures.ForEach(DownloadTexture);
    StartCoroutine(DownloadAllAssets(scene));
  }


private IEnumerator DownloadAllAssets(SimScene scene)
{
    float downloadStartTime = Time.realtimeSinceStartup;
    List<IEnumerator> coroutines = new List<IEnumerator>();
    for (int i = 0; i < scene.meshes.Count; i++)
    {
        coroutines.Add(DownloadMeshHTTP(scene.meshes[i]));
    }

    for (int i = 0; i < scene.textures.Count; i++)
    {
        coroutines.Add(DownloadTextureHTTP(scene.textures[i]));
    }

    List<Coroutine> runningCoroutines = new List<Coroutine>();
    foreach (var coroutine in coroutines)
    {
        runningCoroutines.Add(StartCoroutine(coroutine));
    }

    foreach (var runningCoroutine in runningCoroutines)
    {
        yield return runningCoroutine;
    }

    Debug.Log("All assets have been downloaded.");
    updateAction += BuildScene;
    float timeSpent = (Time.realtimeSinceStartup - downloadStartTime) * 1000;
    Debug.Log($"Downloaded Scene in {(int)timeSpent} ms");
}

  IEnumerator DownloadMeshHTTP(SimMesh mesh)
  {
    if (_cachedMeshes.TryGetValue(mesh.dataHash, out Mesh cached)) {
      mesh.compiledMesh = cached;
      _simMeshes.Add(mesh.name, mesh);
    }
    else
    {
      HostInfo serverInfo = _netManager.GetServerInfo();
      string fileUrl = $"http://{serverInfo.ip}:{(int)ServerPort.HTTP}/?asset_tag={mesh.dataHash}";
      using (UnityWebRequest request = UnityWebRequest.Get(fileUrl))
      {
        float downloadStartTime = Time.realtimeSinceStartup;
        Debug.Log($"Downloading mesh data: {mesh.name} at {downloadStartTime}");
        yield return request.SendWebRequest();
        float timeSpent = (Time.realtimeSinceStartup - downloadStartTime) * 1000;
        Debug.Log($"Downloaded {mesh.name} in {(int)timeSpent} ms");
        if (request.result != UnityWebRequest.Result.Success)
        {
          Debug.LogError($"Error downloading mesh data: {request.error}");
        }
        else
        {
          // Debug.Log($"Downloaded mesh data: {mesh.name}");
          downloadStartTime = Time.realtimeSinceStartup;
          byte[] byteArray = request.downloadHandler.data;
          Span<byte> data = new Span<byte>(byteArray);
          mesh.rawData = new SimMeshData
          {
            indices = MemoryMarshal.Cast<byte, int>(data.Slice(mesh.indicesLayout[0], mesh.indicesLayout[1] * sizeof(int))).ToArray(),
            vertices = MemoryMarshal.Cast<byte, Vector3>(data.Slice(mesh.verticesLayout[0], mesh.verticesLayout[1] * sizeof(float))).ToArray(),
            normals = MemoryMarshal.Cast<byte, Vector3>(data.Slice(mesh.normalsLayout[0], mesh.normalsLayout[1] * sizeof(float))).ToArray(),
            uvs = MemoryMarshal.Cast<byte, Vector2>(data.Slice(mesh.uvLayout[0], mesh.uvLayout[1] * sizeof(float))).ToArray(),
          };
          timeSpent = (Time.realtimeSinceStartup - downloadStartTime) * 1000;
          Debug.Log($"Processed {mesh.name} in {timeSpent} ms");
        }
      }
    }
  }

  IEnumerator DownloadTextureHTTP(SimTexture texture)
  {
    if (_cachedTextures.TryGetValue(texture.dataHash, out Texture cached)){
      texture.compiledTexture = cached;
    }
    else
    {
      HostInfo serverInfo = _netManager.GetServerInfo();
      string fileUrl = $"http://{serverInfo.ip}:{(int)ServerPort.HTTP}/?asset_tag={texture.dataHash}";
      using (UnityWebRequest request = UnityWebRequest.Get(fileUrl))
      {
        // Debug.Log($"Downloading texture data: {texture.name}");
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success)
        {
          Debug.LogError($"Error downloading mesh data: {request.error}");
        }
        else
        {
          // Debug.Log($"Downloaded texture data: {texture.name}");
          texture.textureData = request.downloadHandler.data;
          _simTextures.Add(texture.name, texture);
        }
      }
    }
  }


  private void DownloadMesh(SimMesh mesh) {
    if (_cachedMeshes.TryGetValue(mesh.dataHash, out Mesh cached)) {
      mesh.compiledMesh = cached;
      _simMeshes.Add(mesh.name, mesh);
      return;
    }
    Span<byte> data = _netManager.RequestBytes("Asset", mesh.dataHash).ToArray();
    mesh.rawData = new SimMeshData
    {
      indices = MemoryMarshal.Cast<byte, int>(data.Slice(mesh.indicesLayout[0], mesh.indicesLayout[1] * sizeof(int))).ToArray(),
      vertices = MemoryMarshal.Cast<byte, Vector3>(data.Slice(mesh.verticesLayout[0], mesh.verticesLayout[1] * sizeof(float))).ToArray(),
      normals = MemoryMarshal.Cast<byte, Vector3>(data.Slice(mesh.normalsLayout[0], mesh.normalsLayout[1] * sizeof(float))).ToArray(),
      uvs = MemoryMarshal.Cast<byte, Vector2>(data.Slice(mesh.uvLayout[0], mesh.uvLayout[1] * sizeof(float))).ToArray(),
    };
  }

  private void DownloadTexture(SimTexture texture) {
    if (_cachedTextures.TryGetValue(texture.dataHash, out Texture cached)){
      texture.compiledTexture = cached;
    } else {
      texture.textureData = _netManager.RequestBytes("Asset", texture.dataHash).ToArray();
    }

    _simTextures.Add(texture.name, texture);
  }


  void Update() {
    updateAction.Invoke();
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
          if (!_simMeshes.ContainsKey(visual.mesh)) {
            Debug.LogWarning("Mesh not found, " + visual.mesh);
            visualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualObj.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            break;
          }
          SimMesh asset = _simMeshes[visual.mesh];
          visualObj = new GameObject(asset.name, typeof(MeshFilter), typeof(MeshRenderer));
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
        if (visual.color[3] < 1)
        {
          renderer.material.SetFloat("_Mode", 2);
          renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
          renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
          renderer.material.SetInt("_ZWrite", 0);
          renderer.material.DisableKeyword("_ALPHATEST_ON");
          renderer.material.EnableKeyword("_ALPHABLEND_ON");
          renderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
          renderer.material.renderQueue = 3000;
        }
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
    OnSceneCleared.Invoke();
    if (_simSceneObj != null) Destroy(_simSceneObj);
    _simSceneObj = null;
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
  }

  public void ProcessMesh(SimAsset asset) {
    SimMesh mesh = (SimMesh)asset;

    mesh.compiledMesh = new Mesh{
      name = mesh.name,
      vertices = mesh.rawData.vertices,
      normals = mesh.rawData.normals,
      triangles = mesh.rawData.indices,
      uv = mesh.rawData.uvs,
    };

    mesh.rawData = null;

    _cachedMeshes[mesh.dataHash] = mesh.compiledMesh;
    _simMeshes[mesh.name] = mesh;
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
      if (texture.compiledTexture == null) 
        ProcessTexture(texture);
      mat.mainTexture = texture.compiledTexture;
      mat.mainTextureScale = new Vector2(material.textureSize[0], material.textureSize[1]);
    }

    material.compiledMaterial = mat;
    _simMaterials[material.name] = material;
  }

  public SimAsset ProcessTexture(SimAsset asset) {
    SimTexture simTexture = (SimTexture)asset;

    var tex = new Texture2D(simTexture.width, simTexture.height, TextureFormat.RGB24, false);
    tex.LoadRawTextureData(simTexture.textureData);
    tex.Apply();

    simTexture.textureData = null;

    simTexture.compiledTexture = tex;
    _cachedTextures[simTexture.dataHash] = simTexture.compiledTexture;
    return asset;
  }

  public Dictionary<string, Transform> GetObjectsTrans() {
    return _simObjTrans;
  }

  public GameObject GetSimObject() {
    return _simSceneObj;
  }

}