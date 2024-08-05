using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

public class Publisher {
  protected PublisherSocket _pubSocket;
  protected string _topic;

  public Publisher(string topic) {
    IRXRNetManager _netManager = IRXRNetManager.Instance;
    string hostName = _netManager.GetHostName();
    _topic = $"{hostName}/{topic}";
    _pubSocket = _netManager.GetPublisherSocket();
    _netManager.CreatePublishTopic(_topic);
  }

  public void Publish(object data) {
    string msg = JsonConvert.SerializeObject(data);
    msg = _topic + ":" + msg;
    _pubSocket.SendFrame(msg); 
  }
}


public class Streamer : MonoBehaviour {

  protected string _topic;
  protected Publisher _publisher;

  void Start() {
    SetupTopic();
    _publisher = new Publisher(_topic);
    Initialize();
  }

  protected virtual void SetupTopic() {}

  protected virtual void Initialize() {}

}