using NetMQ;
using NetMQ.Sockets;
using System;
using System.Text;
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

public class Subscriber <MsgType> {
  protected string _topic;
  private Action<MsgType> _receiveAction;

  public Subscriber(string topic, Action<MsgType> receiveAction) {
    _topic = topic;
    _receiveAction = receiveAction;
  }

  public void StartSubscription() {
    if (typeof(MsgType) == typeof(byte[]))
    {
      IRXRNetManager.Instance.SubscribeTopic(_topic, OnByteReceive);
    }
    else
    {
      IRXRNetManager.Instance.SubscribeTopic(_topic, OnReceive);
    }
  }

  public void OnByteReceive(byte[] byteMessage) {
    _receiveAction((MsgType)(object)byteMessage);
  }

  public void OnReceive(byte[] byteMessage) {
    string jsonString = Encoding.UTF8.GetString(byteMessage);
    MsgType msg = JsonConvert.DeserializeObject<MsgType>(jsonString);
    _receiveAction(msg);
  }

  public void Unsubscribe() {
    IRXRNetManager.Instance.UnsubscribeTopic(_topic);
  }

}
