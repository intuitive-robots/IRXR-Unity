using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.Threading.Tasks;


public class SceneLoader : MonoBehaviour {

  private object updateActionLock = new();
  private Action updateAction;
  public Action OnSceneLoaded;
  public Action OnSceneCleared;
  private IRXRNetManager _netManager;
  private GameObject _simSceneObj;
  private SimScene _simScene;
  private Dictionary<string, Transform> _simObjTrans = new ();
  private Dictionary<string, Tuple<SimMesh, List<MeshFilter>>> _pendingMesh = new ();
  private Dictionary<string, Tuple<SimTexture, List<Material>>> _pendingTexture = new ();


  void Start() {
    _netManager = IRXRNetManager.Instance;
    _netManager.OnDisconnected += ClearScene;
    updateAction = () => { };
    OnSceneLoaded += () => Debug.Log("Scene Loaded");
    OnSceneCleared += () => Debug.Log("Scene Cleared");
    _netManager.RegisterServiceCallback("LoadSimScene", LoadSimScene);
  }

  private string LoadSimScene(string simSceneJsonStr) {
    ClearScene();
    _simScene = JsonConvert.DeserializeObject<SimScene>(simSceneJsonStr);
    updateAction += BuildScene;
    Debug.Log("Downloaded scene json and starting to build scene");
    return "Received Scene";
  }

  void BuildScene() {
    // Don't include System.Diagnostics, Debug becomes disambiguous
    // It is more accurate to use System.Diagnostics.Stopwatch, theoretically
    var local_watch = new System.Diagnostics.Stopwatch();
    local_watch.Start();
    // Debug.Log("Start Building Scene");
    _simSceneObj = CreateObject(gameObject.transform, _simScene.root);
    local_watch.Stop();
    Debug.Log($"Building Scene in {local_watch.ElapsedMilliseconds} ms");
    Task.Run(() => DownloadAssets());
    OnSceneLoaded.Invoke();
  }

  public void DownloadAssets() {
    var local_watch = new System.Diagnostics.Stopwatch();
    local_watch.Start();
    int totalMeshSize = 0;
    int totalTextureSize = 0;
    foreach (string hash in _pendingMesh.Keys)
    {
      byte[] meshData = _netManager.RequestBytes("Asset", hash).ToArray();
      var(simMesh, meshFilters) = _pendingMesh[hash];
      RunOnMainThread(() => BuildMesh(meshData, simMesh, meshFilters));
      totalMeshSize += meshData.Length;
    }
    foreach (string hash in _pendingTexture.Keys)
    {
      byte[] texData = _netManager.RequestBytes("Asset", hash).ToArray();
      var(simTex, materials) = _pendingTexture[hash];
      RunOnMainThread(() => BuildTexture(texData, simTex, materials));
      totalTextureSize += texData.Length;
    }

    double meshSizeMB = Math.Round(totalMeshSize / Math.Pow(2, 20), 2);
    double textureSizeMB = Math.Round(totalTextureSize / Math.Pow(2, 20), 2);
    
    local_watch.Stop();
    _pendingMesh.Clear();
    _pendingTexture.Clear();
    
    // When debug run in the subthread, it will not send the log to the server
    RunOnMainThread(() => Debug.Log($"Downloaded {meshSizeMB}MB meshes, {textureSizeMB}MB textures."));
    RunOnMainThread(() => Debug.Log($"Downloaded Asset in {local_watch.ElapsedMilliseconds} ms"));
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
    if (body.visuals.Count != 0) {
      GameObject VisualContainer = new GameObject($"{body.name}_Visuals");
      VisualContainer.transform.SetParent(bodyRoot.transform, false);
      foreach (SimVisual visual in body.visuals) { 
        GameObject visualObj;
        switch (visual.type) {
          case "MESH": {
            SimMesh simMesh = visual.mesh;
            visualObj = new GameObject(simMesh.hash, typeof(MeshFilter), typeof(MeshRenderer));
            if (!_pendingMesh.ContainsKey(simMesh.hash)) {
              _pendingMesh[simMesh.hash] = new(simMesh, new());
            }
            _pendingMesh[simMesh.hash].Item2.Add(visualObj.GetComponent<MeshFilter>());
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
          renderer.material = BuildMaterial(visual.material, body.name);
        }
        else {
          Debug.LogWarning($"Material of {body.name}_Visuals not found");
        }
        visualObj.transform.SetParent(VisualContainer.transform, false);
        ApplyTransform(visualObj.transform, visual.trans);
      }
    }
    body.children.ForEach(body => CreateObject(bodyRoot.transform, body));
    if (_simObjTrans.ContainsKey(body.name)) 
      _simObjTrans.Remove(body.name);
    _simObjTrans.Add(body.name, bodyRoot.transform);
    return bodyRoot;
  }

  void ClearScene() {
    OnSceneCleared.Invoke();
    if (_simSceneObj != null) Destroy(_simSceneObj);
    _pendingMesh.Clear();
    _pendingTexture.Clear();
    _simObjTrans.Clear();
  }

  public Material BuildMaterial(SimMaterial simMat, string objName) {
    Material mat = new Material(Shader.Find("Standard"));
    // Transparency
    if (simMat.color[3] < 1)
    {
      mat.SetFloat("_Mode", 2);
      mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
      mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
      mat.SetInt("_ZWrite", 0);
      mat.DisableKeyword("_ALPHATEST_ON");
      mat.EnableKeyword("_ALPHABLEND_ON");
      mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
      mat.renderQueue = 3000;
    }
    mat.SetColor("_Color", new Color(simMat.color[0], simMat.color[1], simMat.color[2], simMat.color[3]));
    if (simMat.emissionColor != null){
      mat.SetColor("_emissionColor", new Color(simMat.emissionColor[0], simMat.emissionColor[1], simMat.emissionColor[2], simMat.emissionColor[3]));
    }
    mat.SetFloat("_specularHighlights", simMat.specular);
    mat.SetFloat("_Smoothness", simMat.shininess);
    mat.SetFloat("_GlossyReflections", simMat.reflectance);
    if (simMat.texture != null) {
      // Debug.Log($"Texture found for {objName}");
      SimTexture simTex = simMat.texture;
      if (!_pendingTexture.ContainsKey(simTex.hash)) {
        _pendingTexture[simTex.hash] = new(simTex, new());
      }
      _pendingTexture[simTex.hash].Item2.Add(mat);
    }
    return mat;
  }

  public void BuildMesh(byte[] meshData, SimMesh simMesh, List<MeshFilter> meshFilters) {
    var mesh = new Mesh{
      vertices = MemoryMarshal.Cast<byte, Vector3>(new ReadOnlySpan<byte>(meshData, simMesh.verticesLayout[0], simMesh.verticesLayout[1] * sizeof(float))).ToArray(),
      normals = MemoryMarshal.Cast<byte, Vector3>(new ReadOnlySpan<byte>(meshData, simMesh.normalsLayout[0], simMesh.normalsLayout[1] * sizeof(float))).ToArray(),
      triangles = MemoryMarshal.Cast<byte, int>(new ReadOnlySpan<byte>(meshData, simMesh.indicesLayout[0], simMesh.indicesLayout[1] * sizeof(int))).ToArray(),
      uv = MemoryMarshal.Cast<byte, Vector2>(new ReadOnlySpan<byte>(meshData, simMesh.uvLayout[0], simMesh.uvLayout[1] * sizeof(float))).ToArray()
    };
    foreach (MeshFilter meshFilter in meshFilters) {
      meshFilter.mesh = mesh;
    }
  }

  public void BuildTexture(byte[] texData, SimTexture simTex, List<Material> materials) {
    Texture2D tex = new Texture2D(simTex.width, simTex.height, TextureFormat.RGB24, false);
    tex.LoadRawTextureData(texData);
    tex.Apply();

    foreach (Material material in materials) {
      material.mainTexture = tex;
      material.mainTextureScale = new Vector2(simTex.textureSize[0], simTex.textureSize[1]);
    }
  }

  public Dictionary<string, Transform> GetObjectsTrans() {
    return _simObjTrans;
  }

  public GameObject GetSimObject() {
    return _simSceneObj;
  }

}