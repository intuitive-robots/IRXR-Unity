using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oculus.Interaction.Surfaces;
using Oculus.Interaction;

// TODO: Singleton Pattern
public class SceneLoader : MonoBehaviour {

  [SerializeField] SceneController _sceneController;
  [SerializeField] Material _defaultMaterial;
  [SerializeField] ServiceConnection _connection;

  [SerializeField] StreamingConnection _streamingConnection;

  [SerializeField] AssetHandler _assetHandler;

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
<<<<<<< HEAD:Assets/Scripts/SceneLoader.cs
    if (_simSceneObj != null) Destroy(_simSceneObj);
    _simSceneObj = CreateObject(_sceneController.transform, _simScene.root);

    _sceneController.StartUpdate(_simObjTrans);
    _streamingConnection.OnMessage += _sceneController.listener;
=======
    ClearScene();

    _simSceneObj = CreateObject(gameObject.transform, _simScene.root);
    SceneController sceneController = _simSceneObj.AddComponent<SceneController>();
    sceneController.StartUpdate(_simObjTrans);
    _streamingConnection.OnMessage += sceneController.listener;
>>>>>>> meta-quest3-dev:Assets/SimScene/Scripts/SceneLoader.cs
  }


  void ApplyTransform(Transform utransform, SimTransform trans) {
    utransform.localPosition = trans.GetPos();
    utransform.localRotation = trans.GetRot();
    utransform.localScale = trans.GetScale();
  }



  GameObject CreateObject(Transform root, SimBody body, string name = null) {

    GameObject bodyRoot = new GameObject(name != null ? name : body.name);
    bodyRoot.transform.SetParent(root, false);
    ApplyTransform(bodyRoot.transform, body.trans);

    GameObject VisualContainer = new GameObject("Visuals");
    VisualContainer.transform.SetParent(bodyRoot.transform, false);

    foreach (SimVisual visual in body.visuals) { 
      GameObject visualObj;
      switch (visual.type) {
        case "MESH": {
          SimMesh asset = _assetHandler.GetMesh(visual.mesh);
          visualObj = new GameObject(asset.Tag, typeof(MeshFilter), typeof(MeshRenderer));
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
          // throw new Exception("Invalid visual, " + visual.type);
      }

      Renderer renderer = visualObj.GetComponent<Renderer>();
      if (visual.material != null) 
        renderer.material = _assetHandler.GetMaterial(visual.material).compiledMaterial;
      else {
        renderer.material = new Material(_defaultMaterial);
        renderer.material.SetColor("_Color", new Color(visual.color[0], visual.color[1], visual.color[2], visual.color[3]));
      }

      visualObj.transform.SetParent(VisualContainer.transform, false);
      ApplyTransform(visualObj.transform, visual.trans);
    }
    
    body.children.ForEach(body => CreateObject(bodyRoot.transform, body));
    if (_simObjTrans.ContainsKey(body.name)) 
      _simObjTrans.Remove(body.name);
    _simObjTrans.Add(body.name, bodyRoot.transform);
    return bodyRoot;
  }

  void ClearScene() {
    if (_simSceneObj != null) Destroy(_simSceneObj);
    _simObjTrans.Clear();
  }

}