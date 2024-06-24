using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class DataLoader : MonoBehaviour {

  [SerializeField] private Material _defaultMaterial;

  [SerializeField] ServiceConnection _connection;

  [SerializeField] StreamingConnection _streamingConnection;

  [SerializeField] AssetHandler _assetHandler;

  GameObject _scene;

  SimScene _data;

  private System.Diagnostics.Stopwatch _watch;
 
  void Start() {
    _connection.OnServiceConnection += LoadScene;
  }


  void LoadScene() {
    var local_watch = new System.Diagnostics.Stopwatch(); // Dont include System.Diagnostics, Debug becomes disambiguous

    local_watch.Start();
    string asset_info = _connection.request_string("SCENE_INFO");
    _data = JsonConvert.DeserializeObject<SimScene>(asset_info);
    _assetHandler.LoadAssets(_data);
    _watch = local_watch;
  }

  void Update() {

    if (_watch == null) return;

    _assetHandler.Process(); // Compile Meshes, Textures and Materials can only be done on the main thread
    BuildObjects(); // Build the scene objects, also only on the main thread

    _watch.Stop();
    Debug.Log($"Loaded Scene in {_watch.ElapsedMilliseconds} ms");
    _watch = null;
  }

  void BuildObjects() {
    if (_scene != null) Destroy(_scene);
    _scene = CreateObject(null, _data.root);

    var sceneController = _scene.AddComponent<SceneController>();
    
    sceneController.InitializeData(_data.root);
    _streamingConnection.OnMessage += sceneController.listener;
  }

  // Vector3 List2Vector3(List<float> values) {
  //   return new Vector3(values[0], values[1], values[2]);
  // }

  void ApplyTransform(Transform utransform, SimTransform trans) {
    utransform.localPosition = trans.GetPos();
    utransform.localRotation = trans.GetRot();
    // utransform.localEulerAngles = List2Vector3(trans.rot);
    utransform.localScale = trans.GetScale();
  }

  // Transform CreateJoint(Transform root, SimJoint joint) {
    
  //   Type jointType = JointController.GetJointType(joint.type);
  //   GameObject jointRoot = new GameObject(joint.name, jointType);

  //   jointRoot.transform.SetParent(root, false);
  //   ApplyTransform(jointRoot.transform, joint.trans);

  //   JointController jController = (JointController)jointRoot.GetComponent(jointType);
  //   jController.InitializeState(joint);
  //   return jointRoot.transform;
  // }

  void CreateJoint(GameObject obj, SimJoint joint) {
    
    Type jointType = JointController.GetJointType(joint.type);
    JointController jController = (JointController)obj.AddComponent(jointType);
    jController.joint_name = joint.name;
    jController.InitializeState(joint);
  }

  GameObject CreateObject(Transform root, SimBody body) {
    GameObject bodyRoot = new GameObject(body.name);
    if (root != null) bodyRoot.transform.SetParent(root, false);
    ApplyTransform(bodyRoot.transform, body.trans);
    if (body.joint != null)
    {
      CreateJoint(bodyRoot, body.joint);
    }
    
    body.children.ForEach(body => CreateObject(bodyRoot.transform, body));

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
      Debug.Log(body.name);
      ApplyTransform(visualObj.transform, visual.trans);
    }
    return bodyRoot; 
  }
}