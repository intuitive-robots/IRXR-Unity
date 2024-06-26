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

  public void Connect(string serverIp, int serverPort) {

    if (_Address != null) {
      subscriberSocket.Disconnect(_Address);
      while (subscriberSocket.HasIn) subscriberSocket.SkipFrame();
    }

    _Address = $"tcp://{serverIp}:{serverPort}";
    subscriberSocket.Connect(_Address);
    // subscriberSocket.Subscribe("SceneUpdate");
    subscriberSocket.Subscribe("");

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