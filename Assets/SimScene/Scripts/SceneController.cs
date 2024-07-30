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

[RequireComponent(typeof(SceneLoader))]
public class SceneController : MonoBehaviour
{
    private GameObject _client;
    private float lastSimulationTimeStamp = 0.0f;
    public Dictionary<string, Transform> _objectsTrans;
    private Transform _trans;

    void Start() {
        gameObject.GetComponent<SceneLoader>().OnSceneLoaded += StartUpdate;
        gameObject.GetComponent<SceneLoader>().OnSceneCleared += StopUpdate;
    }

    public void StartUpdate() {
        _trans = gameObject.transform;
        _objectsTrans = gameObject.GetComponent<SceneLoader>().GetObjectsTrans();
        _client = IRXRNetManager.Instance.gameObject;
        IRXRNetManager netManager = _client.GetComponent<IRXRNetManager>();
        StreamReceiver streamReceiver = _client.GetComponent<StreamReceiver>();
        streamReceiver.RegisterTopicCallback("SceneUpdate", Subscribe);
        netManager.OnDiscoveryCompleted += streamReceiver.Connect;
    }

    public void StopUpdate() {
        _client = IRXRNetManager.Instance.gameObject;
        _client.GetComponent<StreamReceiver>().RegisterTopicCallback("SceneUpdate", null);
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
            _objectsTrans[name].position = transform.TransformPoint(new Vector3(value[0], value[1], value[2]));
            _objectsTrans[name].rotation = _trans.rotation * new Quaternion(value[3], value[4], value[5], value[6]);
        }
    }
}
