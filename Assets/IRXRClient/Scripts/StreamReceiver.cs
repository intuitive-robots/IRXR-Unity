using System;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System.Collections.Generic;

[RequireComponent(typeof(IRXRNetManager))]
public class StreamReceiver : MonoBehaviour {

  private SubscriberSocket _subSocket;
  private string _Address;
  private IRXRNetManager _netManager;
  private Dictionary<string, Action<string>> _topicsCallbacks;

  void Awake() {
    _topicsCallbacks = new Dictionary<string, Action<string>>();
  }

  void Start() {
    _netManager = gameObject.GetComponent<IRXRNetManager>();
    _subSocket = _netManager.GetSubscriberSocket();
    _netManager.OnDiscoveryCompleted += Connect;
  }

  public void Connect() {
    Connect(_netManager.GetServerIp(), _netManager.GetServerPort("STREAMING"));
  }

  public void Connect(string serverIp, int serverPort) {

    if (_Address != null) {
      _subSocket.Disconnect(_Address);
      while (_subSocket.HasIn) _subSocket.SkipFrame();
    }

    _Address = $"tcp://{serverIp}:{serverPort}";
    _subSocket.Connect(_Address);
    _subSocket.Subscribe("");

    _topicsCallbacks.Clear();
    Debug.Log("Connected streaming socket to " + _Address);
  }

  public void Update() {
    if (!_subSocket.HasIn) return;
    string messageReceived = _subSocket.ReceiveFrameString();
    string[] messageSplit = messageReceived.Split(":", 2);
    if (_topicsCallbacks.ContainsKey(messageSplit[0])) {
      _topicsCallbacks[messageSplit[0]](messageSplit[1]);
    }
  }

  public void RegisterTopicCallback(string topic, Action<string> callback) {
    _topicsCallbacks[topic] = callback;
  }

}