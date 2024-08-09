using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

class StreamMessage {
    public Dictionary<string, List<float>> updateData;
    public float time;
}

[RequireComponent(typeof(SceneLoader))]
public class RigidObjectsController : MonoBehaviour
{
    private float lastSimulationTimeStamp = 0.0f;
    public Dictionary<string, Transform> _objectsTrans;
    private Transform _trans;
    private float timeOffset = 0.0f;
    private float frameCounter = 0;
    private float timeDelay = 0;

    void Start() {
        gameObject.GetComponent<SceneLoader>().OnSceneLoaded += StartSubscription;
        gameObject.GetComponent<SceneLoader>().OnSceneCleared += StopSubscription;
    }

    public void StartSubscription() {
        _trans = gameObject.transform;
        _objectsTrans = gameObject.GetComponent<SceneLoader>().GetObjectsTrans();
        timeOffset = IRXRNetManager.Instance.TimeOffset;
        Debug.Log("Start Update Scene");
        IRXRNetManager.Instance.SubscribeTopic("SceneUpdate", SubscribeCallback);
    }

    public void StopSubscription() {
        IRXRNetManager.Instance.UnsubscribeTopic("SceneUpdate");
    }

    public void SubscribeCallback(string message) {
        StreamMessage streamMsg = JsonConvert.DeserializeObject<StreamMessage>(message);
        if (streamMsg.time < lastSimulationTimeStamp) return;
        lastSimulationTimeStamp = streamMsg.time;
        foreach (var (name, value) in streamMsg.updateData) {
            _objectsTrans[name].position = transform.TransformPoint(new Vector3(value[0], value[1], value[2]));
            _objectsTrans[name].rotation = _trans.rotation * new Quaternion(value[3], value[4], value[5], value[6]);
        }
        timeDelay += Time.realtimeSinceStartup - streamMsg.time - timeOffset;
        frameCounter++;
        // measure latency every 1000 frames
        if (frameCounter == 1000) {
            Debug.Log($"Average Latency in the last 1000 frames: {timeDelay} ms");
            timeDelay = 0;
            frameCounter = 0;
        }
    }

}