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
    private GameObject _client;
    private float lastSimulationTimeStamp = 0.0f;
    public Dictionary<string, Transform> _objectsTrans;
    public Transform _trans;

    public void StartUpdate(Dictionary<string, Transform> objectsTrans) {
        _trans = gameObject.transform;
        _objectsTrans = objectsTrans;
        _client = IRXRNetManager.Instance.gameObject;
        IRXRNetManager netManager = _client.GetComponent<IRXRNetManager>();
        StreamReceiver streamingReceiver = _client.GetComponent<StreamReceiver>();
        streamingReceiver.RegisterTopicCallback("SceneUpdate", Subscribe);
        netManager.OnDiscoveryCompleted += streamingReceiver.Connect;
    }

    public void Subscribe(string message) {
        // TODO: not sure it is ok???
        if (string.Compare(message, "END") == 0) {
            lastSimulationTimeStamp = 0.0f;
            return;
        }
        StreamMessage streamMsg = JsonConvert.DeserializeObject<StreamMessage>(message);

        if (streamMsg.time < lastSimulationTimeStamp) return;
        lastSimulationTimeStamp = streamMsg.time;
        foreach (var (name, value) in streamMsg.updateData) {
            _objectsTrans[name].position = new Vector3(value[0], value[1], value[2]) + _trans.position;
            _objectsTrans[name].rotation = new Quaternion(value[3], value[4], value[5], value[6]) * _trans.rotation;
        }
    }
}
