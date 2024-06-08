using UnityEngine;
using NetMQ;
using NetMQ.Sockets;

public class StreamingConnection : MonoBehaviour {

  public delegate void Listener(string message); 

  public Listener OnMessage;

  private SubscriberSocket subscriberSocket;

  private string _Address;

  void Awake() {
    subscriberSocket = new SubscriberSocket();
  }

  public void connect(string server_ip, int server_port) {

    if (_Address != null) {
      subscriberSocket.Disconnect(_Address);
      while (subscriberSocket.HasIn) subscriberSocket.SkipFrame();
    }

    _Address = $"tcp://{server_ip}:{server_port}";
    subscriberSocket.Connect(_Address);
    subscriberSocket.SubscribeToAnyTopic();

    OnMessage -= OnMessage;


    Debug.Log("Connected streaming socket to " + _Address);
  }

  public void Update() {
    if (!subscriberSocket.HasIn) return;

    string message = subscriberSocket.ReceiveFrameString();
    OnMessage?.Invoke(message);
  }

  void OnApplicationQuit() {
    subscriberSocket.Dispose();
  }
}