using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Unity.Collections;
using UnityEngine;


class SimulationStreamMessage {
    public Dictionary<string, List<float>> Data;
    public float Time;
}



public class SceneController : MonoBehaviour
{
    private float lastSimulationTimeStamp = 0.0f;
    public Dictionary<string, Transform> gameObjects = new();

    public void Start() {
        var objList = new List<Transform>(transform.GetComponentsInChildren<Transform>());
        
        foreach(var obj in objList) {
            gameObjects.Add(obj.name, obj);
        }
    }

    public void listener(string message) {

        if (string.Compare(message, "END") == 0) {
            lastSimulationTimeStamp = 0.0f;
            return;
        }

        SimulationStreamMessage jointValues = JsonConvert.DeserializeObject<SimulationStreamMessage>(message);

        if (jointValues.Time < lastSimulationTimeStamp) return;

        lastSimulationTimeStamp = jointValues.Time;

        foreach (var (name, new_values) in jointValues.Data) {
            gameObjects[name].localPosition = new Vector3(new_values[0], new_values[1], new_values[2]);
            gameObjects[name].localRotation = new Quaternion(new_values[3], new_values[4], new_values[5], new_values[6]);
        }
    }

  

}
