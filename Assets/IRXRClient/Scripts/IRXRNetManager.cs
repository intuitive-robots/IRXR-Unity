
using NetMQ;
using NetMQ.Sockets;
using UnityEngine;
using System;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Net.Sockets;
using System.Collections.Generic;


class DiscoveryMessage {
  public Dictionary<string, string> Topic = new();
  public Dictionary<string, string> Service = new();
}

public class IRXRNetManager : Singleton<IRXRNetManager> {

  [SerializeField] private string host;
  public Action OnDisconnected;
  public Action OnConnectionCompleted;
  public Action OnNewServerDiscovered;
  public Action OnServiceConnection;
  public Action ConnectionSpin;
  private string _serverAddress;

  private UdpClient _discoveryClient;

  private DiscoveryMessage _informations = null;
  private DiscoveryMessage clientInfo = new DiscoveryMessage();

  private List<NetMQSocket> _sockets;

  // subscriber socket
  private SubscriberSocket _subSocket;
  private Dictionary<string, Action<string>> _topicsCallbacks;
  // publisher socket
  private PublisherSocket _pubSocket;
  private RequestSocket _reqSocket;
  private ResponseSocket _resSocket;

  private float lastTimeStamp;
  private bool isConnected = false;

  void Awake() {
    AsyncIO.ForceDotNet.Force();
    _discoveryClient = new UdpClient(7720);
    _sockets = new List<NetMQSocket>();
    _reqSocket = new RequestSocket();
    _resSocket = new ResponseSocket();
    // subscriber socket
    _subSocket = new SubscriberSocket();
    _topicsCallbacks = new Dictionary<string, Action<string>>();
    _pubSocket = new PublisherSocket();
    _pubSocket.Bind("tcp://*:7723");
    _sockets = new List<NetMQSocket> { _reqSocket, _resSocket, _subSocket, _pubSocket };
  }

  void Start() {
    OnNewServerDiscovered += () => Debug.Log("New Server Discovered");
    OnNewServerDiscovered += ConnectService;
    OnNewServerDiscovered += ConnectTopics;
    OnConnectionCompleted += () => Debug.Log("Connection Completed");
    OnServiceConnection += () => {};
    ConnectionSpin += () => {};
    lastTimeStamp = -2.0f;
  }

  void Update() {
    ConnectionSpin.Invoke();
    if (_discoveryClient.Available == 0) return; // there's no message to read
    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
    byte[] result = _discoveryClient.Receive(ref endPoint);
    string message =  Encoding.UTF8.GetString(result);

    // Debug.Log(_discoveryClient.LocalEndPoint.Address.ToString());
    // Socket udpSocket = _discoveryClient.Client;
    // byte[] buffer = new byte[1024];
    // SocketFlags socketFlags = SocketFlags.None;
    // EndPoint senderRemote = new IPEndPoint(IPAddress.Any, 0);
    // IPPacketInformation packetInfo;
    // int receivedBytes = udpSocket.ReceiveMessageFrom(buffer, 0, buffer.Length, ref socketFlags, ref senderRemote, out packetInfo);
    // string receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
    // Console.WriteLine($"Received message from {senderRemote}: {receivedMessage}");
    // Console.WriteLine($"Received on interface: {packetInfo.Address}");

    if (!message.StartsWith("SimPub")) return; // not the right tag
    var split = message.Split(":", 2);
    string info = split[1];
    _informations = JsonConvert.DeserializeObject<DiscoveryMessage>(info);
    _serverAddress = endPoint.Address.ToString();
    if (lastTimeStamp + 2.0f < Time.realtimeSinceStartup) {
      string localIpAddress = ((IPEndPoint)_discoveryClient.Client.LocalEndPoint).Address.ToString();
      Debug.Log($"Discovered server at {endPoint.Address} with local IP {localIpAddress}");
      OnNewServerDiscovered.Invoke();
      OnConnectionCompleted.Invoke();
      isConnected = true; // not really elegant, just for the disconnection of subsocket
    }
    lastTimeStamp = Time.realtimeSinceStartup;
  }

  void OnApplicationQuit() {
    _discoveryClient.Dispose();
    foreach (var socket in _sockets) {
      socket.Dispose();
    }
    // This must be called after all the NetMQ sockets are disposed
    NetMQConfig.Cleanup();
  }

  // public bool HasDiscovered() {
  //     return _serverAddress != null;
  // }

  public string GetServerAddress() {
      return _serverAddress;
  }

  public string GetServiceAddress(string service) {
      if (_informations == null) return null;
      if (_informations.Service.TryGetValue(service, out string result)) {
          return result;
      }
      return null;
  }

  public SubscriberSocket GetSubscriberSocket() {
    return _subSocket;
  }

  public RequestSocket GetRequestSocket() {
    return _reqSocket;
  }

  public PublisherSocket GetPublisherSocket() {
    return _pubSocket;
  }

  public ResponseSocket GetResponseSocket() {
    return _resSocket;
  }

  public void ConnectService () {
    _reqSocket.Connect($"tcp://{_serverAddress}:7721");
    Debug.Log($"Starting service connection to {_serverAddress} at prot 7721");
    OnServiceConnection.Invoke();
  }

  public string RequestString(string service, string request = "") {
    _reqSocket.SendFrame($"{service}:{request}");
    string result = _reqSocket.ReceiveFrameString(out bool more);
    while(more) result += _reqSocket.ReceiveFrameString(out more);
    return result;
  }

  public List<byte> RequestBytes(string service, string request = "") {
    _reqSocket.SendFrame($"{service}:{request}");
    List<byte> result = new List<byte>(_reqSocket.ReceiveFrameBytes(out bool more));
    while (more) result.AddRange(_reqSocket.ReceiveFrameBytes(out more));
    return result;
  }

  public void DisconnectTopics() {
    if (isConnected) {
      _subSocket.Disconnect($"tcp://{_serverAddress}:7722");
      while (_subSocket.HasIn) _subSocket.SkipFrame();
    }
    ConnectionSpin -= TopicUpdate;
    _topicsCallbacks.Clear();
  }

  public void ConnectTopics() {
    DisconnectTopics();
    _subSocket.Connect($"tcp://{_serverAddress}:7722");
    _subSocket.Subscribe("");
    ConnectionSpin += TopicUpdate;
    Debug.Log($"Connected topic to {_serverAddress} at port 7722");
  }

  public void TopicUpdate() {
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

  public void ReleasePublishTopic(string topic) {
    clientInfo.Topic[topic] = "Release";
  }

}
