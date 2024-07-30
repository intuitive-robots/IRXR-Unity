using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

[RequireComponent(typeof(IRXRNetManager))]
public class StreamPublisher : MonoBehaviour {

  protected PublisherSocket _pubSocket;
  protected IRXRNetManager _netManager;
  [SerializeField] private string port;
  [SerializeField] private string topic;

  void Start() {
    _netManager = gameObject.GetComponent<IRXRNetManager>();
    _pubSocket = _netManager.CreatePublisherSocket();
    _pubSocket.Bind($"tcp://*:{port}");
  }

  public void Publish(object data) {
    string msg = JsonConvert.SerializeObject(data);
    _pubSocket.SendMoreFrame(topic).SendFrame(msg);
    
  }

}