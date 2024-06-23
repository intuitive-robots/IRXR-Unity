using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class DataLoader : MonoBehaviour {

  [SerializeField] ServiceConnection _connection;

  [SerializeField] StreamingConnection _streamingConnection;

  [SerializeField] AssetHandler _assetHandler;

  GameObject _scene;

  SimScene _data;

  private System.Diagnostics.Stopwatch _watch;
 
  void Start() {
    _connection.OnServiceConnection += load_scene;
  }


  void load_scene() {
    var local_watch = new System.Diagnostics.Stopwatch(); // Dont include System.Diagnostics, Debug becomes disambiguous

    local_watch.Start();
    string asset_info = _connection.request_string("SCENE_INFO");
    _data = JsonConvert.DeserializeObject<SimScene>(asset_info);
    _assetHandler.LoadAssets(_data);
    _watch = local_watch;
  }

  void Update() {

    if (_watch == null) return;

    _assetHandler.Process(); // Compile Meshes, Textures and Materials (can only be done on the main thread)
    build_objects(); // Build the scene objects, also only on the main thread

    _watch.Stop();
    Debug.Log($"Loaded Scene in {_watch.ElapsedMilliseconds} ms");
    _watch = null;
  }

  void build_objects() {
    if (_scene != null) Destroy(_scene);
    _scene = create_body(null, _data.WorldBody, "Scene-" + _data.Id);
    var sceneController = _scene.AddComponent<SceneController>();
    
    sceneController.InitializeData(_data.WorldBody);
    _streamingConnection.OnMessage += sceneController.listener;
  }

  void apply_transform(Transform utransform, SimTransform transform) {
    utransform.localScale = new Vector3(transform.Scale[0], transform.Scale[1], transform.Scale[2]);
    utransform.localPosition = new Vector3(transform.Position[0], transform.Position[1], transform.Position[2]);
    utransform.localRotation = new Quaternion(transform.Rotation[0], transform.Rotation[1], transform.Rotation[2], transform.Rotation[3]);
  }

  Transform create_joint(Transform root, SimJoint joint) {
    
    Type jointType = JointController.GetJointType(joint.Type);
    GameObject jointRoot = new GameObject(joint.Name, jointType);

    jointRoot.transform.SetParent(root, false);
    apply_transform(jointRoot.transform, joint.Transform);

    JointController jController = (JointController)jointRoot.GetComponent(jointType);
    jController.InitializeState(joint);
    return jointRoot.transform;
  }


  GameObject create_body(Transform root, SimBody body, string name = null) {

    if (root != null)
      body.Joints.ForEach(joint => root = create_joint(root, joint)); // create joint chain

    GameObject bodyRoot = new GameObject(name != null ? name : body.Name);
    if (root != null) 
      bodyRoot.transform.SetParent(root, false);

    body.Bodies.ForEach(body => create_body(bodyRoot.transform, body));

    GameObject VisualContainer = new GameObject("Visuals");
    VisualContainer.transform.SetParent(bodyRoot.transform, false);

    foreach (SimVisual visual in body.Visuals) { 
      GameObject Visual;     
      switch (visual.Type) {
        case "MESH": {
          SimMesh asset = _assetHandler.GetMesh(visual.Mesh);
          Visual = new GameObject(asset.Tag, typeof(MeshFilter), typeof(MeshRenderer));
          Visual.GetComponent<MeshFilter>().mesh = asset.compiledMesh; 
          break;
        }
        case "BOX":
          Visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
          break;
        case "PLANE":
          Visual = GameObject.CreatePrimitive(PrimitiveType.Plane);    
          break;
        case "CYLINDER":
          Visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
          break;
        case "CAPSULE":
          Visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
          break;
        case "SPHERE":
          Visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
          break;
        default:
          throw new Exception("Invalid visual, " + visual.Type);
      }

      Renderer renderer = Visual.GetComponent<Renderer>();
      if (visual.Material != null) 
        renderer.material = _assetHandler.GetMaterial(visual.Material).compiledMaterial;
      else {
        renderer.material = new Material(Shader.Find("Standard"));
        renderer.material.SetColor("_Color", new Color(visual.Color[0], visual.Color[1], visual.Color[2], visual.Color[3]));
      }

      Visual.transform.SetParent(VisualContainer.transform, false);
      apply_transform(Visual.transform, visual.Transform);
    }
    return bodyRoot; 
  }
}