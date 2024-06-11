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
    public string Name { get; private set; }
    [SerializeField] private Dictionary<string, JointController> joints;

    public void InitializeData(SimBody body) {
        var jointList = new List<JointController>(transform.GetComponentsInChildren<JointController>());
        
        joints = new Dictionary<string, JointController>();
        foreach(var joint in jointList) {
            joints.Add(joint.name, joint);
        }

        Name = body.Name;
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
            joints[name].SetValue(new_values);
        }
    }

  

}
