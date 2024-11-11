using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Linq;
using System.IO.Pipes;
using System.Collections.Concurrent;
using Unity.Collections;
using System.Reflection;

public class SceneLoader : MonoBehaviour {


  private object updateActionLock = new();
  private Action updateAction;
  public Action OnSceneLoaded;
  public Action OnSceneCleared;
  private IRXRNetManager _netManager;
  private GameObject _simSceneObj;
  private SimScene _simScene;
  private Dictionary<string, Transform> _simObjTrans = new ();

  // private Dictionary<string, SimMesh> _simMeshes;
  // private Dictionary<string, SimMaterial> _simMaterials;
  // private Dictionary<string, SimTexture> _simTextures;
  // private Dictionary<string, Mesh> _cachedMeshes;
  // private Dictionary<string, Texture> _cachedTextures;

  private Dictionary<string, List<MeshFilter>> _pendingMesh = new ();
  private Dictionary<string, List<Material>> _pendingMaterial = new ();


  void Awake() {
    // _simMeshes = new();
    // _simMaterials = new();
    // _simTextures = new();

    // _cachedTextures = new();
    // _cachedMeshes = new();
  }

  void Start() {
    _netManager = IRXRNetManager.Instance;
    _netManager.OnDisconnected += ClearScene;
    _netManager.OnConnectionStart += DownloadScene;
    updateAction = () => { };
    OnSceneLoaded += () => Debug.Log("Scene Loaded");
    OnSceneCleared += () => Debug.Log("Scene Cleared");
    _netManager.RegisterServiceCallback("ClearScene", LoadPointCloud);
  }

  private void LoadSimScene(string pointCloudStrData) {
    ClearScene();
    
  }

  void BuildScene() {
    // Don't include System.Diagnostics, Debug becomes disambiguous
    var local_watch = new System.Diagnostics.Stopwatch();
    local_watch.Start();
    Debug.Log("Building Scene");
    _simSceneObj = CreateObject(gameObject.transform, _simScene.root);
    local_watch.Stop();
    Debug.Log($"Building Scene in {local_watch.ElapsedMilliseconds} ms");
    OnSceneLoaded.Invoke();
  }

  private void DownloadScene() {

    var local_watch = new System.Diagnostics.Stopwatch();
    local_watch.Start();

    if (!_netManager.CheckServerService("Scene")) {
      Debug.LogWarning("Scene Service is not found");
      return;
    }
    
    string asset_info = _netManager.RequestString("Scene");
    
    if (asset_info == "Invalid Service") {
      Debug.LogWarning("Invalid Service");
      return;
    }

    _simScene = JsonConvert.DeserializeObject<SimScene>(asset_info);
    DownloadAssets(_simScene);
    local_watch.Stop();
    Debug.Log($"Downloaded Scene in {local_watch.ElapsedMilliseconds} ms");
    updateAction += BuildScene;
  }

  public void DownloadAssets(SimScene scene) {
    _simMeshes.Clear();
    _simMaterials.Clear();
    _simTextures.Clear();

    int textureSizeAcc = 0;
    int meshSizeAcc = 0;

    scene.meshes.ForEach(mesh => {
      meshSizeAcc += DownloadMesh(mesh);
    });
    scene.textures.ForEach(texture => {
      textureSizeAcc += DownloadTexture(texture);
    });  
    scene.materials.ForEach(material => _simMaterials.Add(material.name, material));

    print($"Downloaded {Math.Round(meshSizeAcc / Math.Pow(2, 20), 2)}MB meshes, {Math.Round(textureSizeAcc / Math.Pow(2, 20), 2)}MB textures, {scene.materials.Count} materials");
  }

  private int DownloadMesh(SimMesh mesh) {
    int meshSize = 0;
    if (!_cachedMeshes.TryGetValue(mesh.dataHash, out mesh.compiledMesh)) {
      byte[] data = _netManager.RequestBytes("Asset", mesh.dataHash).ToArray();
      meshSize += data.Length;
      RunOnMainThread(() => ProcessMesh(mesh, data));
    }
    _simMeshes.TryAdd(mesh.name, mesh);
    return meshSize;
  }

  private int DownloadTexture(SimTexture texture) {
    int textureSize = 0;
    if (!_cachedTextures.TryGetValue(texture.dataHash, out texture.compiledTexture)){
      List<byte> data = _netManager.RequestBytes("Asset", texture.dataHash);
      textureSize = data.Count;
      RunOnMainThread(() => ProcessTexture(texture, data));
    }
    _simTextures.TryAdd(texture.name, texture);
    return textureSize;
  }


  void RunOnMainThread(Action action) {
    lock(updateActionLock) {
      updateAction += action;
    }
  }

  void Update() {
    lock(updateActionLock) {
      updateAction.Invoke();
      updateAction = () => { };
    }
  }


  void ApplyTransform(Transform utransform, SimTransform trans) {
    utransform.localPosition = trans.GetPos();
    utransform.localRotation = trans.GetRot();
    utransform.localScale = trans.GetScale();
  }


  GameObject CreateObject(Transform root, SimBody body) {
    
    GameObject bodyRoot = new GameObject(body.name);
    bodyRoot.transform.SetParent(root, false);
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
          _pendingMesh[asset.name] = _pendingMesh.ContainsKey(asset.name) ? _pendingMesh[asset.name] : new List<GameObject>();
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
      renderer.material = new Material(Shader.Find("Standard"));
      if (visual.material != null) {
        var material = GetMaterial(visual.material);
        if (material.compiledMaterial == null) {
          ProcessMaterial(material);
        }
        renderer.material = material.compiledMaterial;
      }
      else {
        renderer.material = CreateColorMaterial(visual.color);
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
  }

  public SimMesh GetMesh(string id) => _simMeshes[id];
  public SimTexture GetTexture(string id) => _simTextures[id];
  public SimMaterial GetMaterial(string id) => _simMaterials[id];
  
  public Material CreateColorMaterial(List<float> color) {
    var material = new Material(Shader.Find("Standard"));
    if (color[3] < 1)
    {
      material.SetFloat("_Mode", 2);
      material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
      material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
      material.SetInt("_ZWrite", 0);
      material.DisableKeyword("_ALPHATEST_ON");
      material.EnableKeyword("_ALPHABLEND_ON");
      material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
      material.renderQueue = 3000;
    }
    material.SetColor("_Color", new Color(color[0], color[1], color[2], color[3]));
    return material;
  }

  public void ProcessMesh(SimAsset asset, byte[] data) {
    SimMesh mesh = (SimMesh)asset;


    mesh.compiledMesh = new Mesh{
      name = asset.name, 
      vertices = MemoryMarshal.Cast<byte, Vector3>(new ReadOnlySpan<byte>(data, mesh.verticesLayout[0], mesh.verticesLayout[1] * sizeof(float))).ToArray(),
      normals = MemoryMarshal.Cast<byte, Vector3>(new ReadOnlySpan<byte>(data, mesh.normalsLayout[0], mesh.normalsLayout[1] * sizeof(float))).ToArray(),
      triangles = MemoryMarshal.Cast<byte, int>(new ReadOnlySpan<byte>(data, mesh.indicesLayout[0], mesh.indicesLayout[1] * sizeof(int))).ToArray(),
      uv = MemoryMarshal.Cast<byte, Vector2>(new ReadOnlySpan<byte>(data, mesh.uvLayout[0], mesh.uvLayout[1] * sizeof(float))).ToArray()
    };

    _cachedMeshes[mesh.hash] = mesh.compiledMesh;
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

      _simTextures[material.texture] = texture; // Just to be shure the texture is updated (investigate)

      mat.mainTexture = texture.compiledTexture;
      mat.mainTextureScale = new Vector2(material.textureSize[0], material.textureSize[1]);
    }

    material.compiledMaterial = mat;
    _simMaterials[material.name] = material;
  }

  public SimAsset ProcessTexture(SimAsset asset, List<byte> data) {
    SimTexture simTexture = (SimTexture)asset;

    byte[] byteData = data.ToArray();
    data.Clear();
    data = null;

    var tex = new Texture2D(simTexture.width, simTexture.height, TextureFormat.RGB24, false);
    tex.LoadRawTextureData(byteData);
    tex.Apply();
    
    simTexture.compiledTexture = tex;
    _cachedTextures[simTexture.hash] = simTexture.compiledTexture;
    return asset;
  }

  public Dictionary<string, Transform> GetObjectsTrans() {
    return _simObjTrans;
  }

  public GameObject GetSimObject() {
    return _simSceneObj;
  }

}