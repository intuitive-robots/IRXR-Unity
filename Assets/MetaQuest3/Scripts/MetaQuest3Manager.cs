using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Oculus.Interaction;

// TODO
// class Vibration
// {
//     string hand;
//     float amplitude;
// }

class MetaQuest3Manager : MonoBehaviour {
    void Start() {
        IRXRNetManager _netManager = IRXRNetManager.Instance;
        string hostName = _netManager.GetHostName();
        _netManager.SubscribeTopic($"{hostName}/Vibration", Vibration);
    }

    public void Vibration(string message) {
        if (message == "left")
        {
           OVRInput.SetControllerVibration(1, 1, OVRInput.Controller.LTouch);
        }
        else if (message == "right")
        {
            OVRInput.SetControllerVibration(1, 1, OVRInput.Controller.RTouch);
        }
    }

}