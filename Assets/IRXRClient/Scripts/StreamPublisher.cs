using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

[RequireComponent(typeof(IRXRNetManager))]
public class StreamPublisher : MonoBehaviour {

  protected PublisherSocket _pubSocket;
  protected IRXRNetManager _netManager;
  [SerializeField] private string topic;

  void Start() {
    _pubSocket = IRXRNetManager.Instance.GetPublisherSocket();
    Application.logMessageReceived += HandleLog;
  }

  void HandleLog(string logString, string stackTrace, LogType type) {
    string msg = "UnityLog" + ":" + logString;
    _pubSocket.SendFrame(msg);
  }

  public void Publish(object data) {
    string msg = JsonConvert.SerializeObject(data);
    msg = topic + ":" + msg;
    _pubSocket.SendFrame(msg);
    
  }

}