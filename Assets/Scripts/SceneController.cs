using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Unity.Collections;
using UnityEngine;


class StreamMessage {
    public Dictionary<string, List<float>> updateData;
    public float time;
}



public class SceneController : MonoBehaviour
{
    private float lastSimulationTimeStamp = 0.0f;
    public Dictionary<string, Transform> _objectsTrans;

    public void StartUpdate(Dictionary<string, Transform> objectsTrans) {
        _objectsTrans = objectsTrans;
    }

    public void listener(string message) {

        if (string.Compare(message, "END") == 0) {
            lastSimulationTimeStamp = 0.0f;
            return;
        }

        StreamMessage streamMsg = JsonConvert.DeserializeObject<StreamMessage>(message);

        if (streamMsg.time < lastSimulationTimeStamp) return;

        lastSimulationTimeStamp = streamMsg.time;
        foreach (var (name, value) in streamMsg.updateData) {
            _objectsTrans[name].localPosition = new Vector3(value[0], value[1], value[2]);
            _objectsTrans[name].localRotation = new Quaternion(value[3], value[4], value[5], value[6]);
        }
    }
}
