using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;


public class Streamer : MonoBehaviour {

  protected PublisherSocket _pubSocket;
  [SerializeField] private string topic;

  void Start() {
    // IRXRNetManager.Instance.
    // _pubSocket = IRXRNetManager.Instance.GetPublisherSocket();
    // Initialize();
  }

  void Initialize() {}

  public void Publish(object data) {
    string msg = JsonConvert.SerializeObject(data);
    msg = topic + ":" + msg;
    _pubSocket.SendFrame(msg);
    
  }

}