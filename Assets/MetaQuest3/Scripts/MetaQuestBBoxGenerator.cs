using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oculus.Interaction;
using UnityEngine.UIElements;
using Unity.VisualScripting;
using UnityEngine.Animations;

class BBoxData {
    public List<float[]> data;
}

public class MetaQuestBBoxGenerator : MonoBehaviour {

    [SerializeField] public GameObject boundingBoxPrefab;
    private List<GameObject> bboxList = new List<GameObject>();
    
    private void Start() {
        IRXRNetManager _netManager = IRXRNetManager.Instance;
        _netManager.RegisterServiceCallback("GenerateBBox", CreateBBoxFromJson);
    }

    private void Update() {
        if (OVRInput.GetDown(OVRInput.Button.Two)) // button A locks / unlocks the scene
        {
            GenerateDefaultBBox();
        }
    }

    public string CreateBBoxFromJson(string bBoxJson) {
        BBoxData bboxData = JsonConvert.DeserializeObject<BBoxData>(bBoxJson);
        foreach (var item in bboxData.data)
        {
            Vector3 position = new Vector3(item[0], item[1], item[2]);
            Quaternion rotation = new Quaternion(item[3], item[4], item[5], item[6]);
            Vector3 localScale = new Vector3(item[7], item[8], item[9]);
            GenerateBBox(position, rotation, localScale);
        }
        return "Generated Bounding Box";
    }

    public void GenerateBBox(Vector3 pos, Quaternion rot, Vector3 scale) {
        GameObject boundingBox = Instantiate(boundingBoxPrefab);
        // if (tf == null) {
        //     tf = transform;
        //     boundingBox.transform.localScale = tf.localScale;
        // }
        boundingBox.transform.SetParent(transform);
        boundingBox.transform.localPosition = pos;
        boundingBox.transform.localRotation = rot;
        if (scale != Vector3.zero) {
            boundingBox.transform.localScale = scale;
        }
        bboxList.Add(boundingBox);
    }

    public void GenerateDefaultBBox() {
        GenerateBBox(new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0), Vector3.zero);
    }

}