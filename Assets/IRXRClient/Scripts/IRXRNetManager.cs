
using NetMQ;
using NetMQ.Sockets;
using UnityEngine;
using System;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Net.NetworkInformation;

public enum ServerPort {
  Discovery = 7720,
  Service = 7721,
  Topic = 7722,
}

public enum ClientPort {
  Discovery = 7720,
  Service = 7723,
  Topic = 7724,
}

class HostInfo {
  public string name;
  public string ip;
  public List<string> topics = new();
  public List<string> services = new();
}

public class IRXRNetManager : Singleton<IRXRNetManager> {

  public Action OnDisconnected;
  public Action OnConnectionStart;
  // public Action OnServerDiscovered;
  public Action ConnectionSpin;
  private UdpClient _discoveryClient;
  private string _conncetionID = null;
  private HostInfo _serverInfo = null;
  private HostInfo _localInfo = new HostInfo();

  private List<NetMQSocket> _sockets;

  // subscriber socket
  private SubscriberSocket _subSocket;
  private Dictionary<string, Action<string>> _topicsCallbacks;
  // publisher socket
  private PublisherSocket _pubSocket;
  private RequestSocket _reqSocket;
  private ResponseSocket _resSocket;
  private Dictionary<string, Func<string, string>> _serviceCallbacks;

  private float lastTimeStamp;
  private bool isConnected = false;
  private float timeOffset = 0.0f;
  public float TimeOffset
  {
    get { return timeOffset; }
    set { timeOffset = value; }
  }


  void Awake() {
    AsyncIO.ForceDotNet.Force();
    _discoveryClient = new UdpClient((int)ServerPort.Discovery);
    _sockets = new List<NetMQSocket>();
    _reqSocket = new RequestSocket();
    // response socket
    _resSocket = new ResponseSocket();
    _serviceCallbacks = new Dictionary<string, Func<string, string>>();
    // subscriber socket
    _subSocket = new SubscriberSocket();
    _topicsCallbacks = new Dictionary<string, Action<string>>();
    _pubSocket = new PublisherSocket();
    // the collection of all sockets
    _sockets = new List<NetMQSocket> { _reqSocket, _resSocket, _subSocket, _pubSocket };
    // Default host name
    if (PlayerPrefs.HasKey("HostName"))
    {
        // The key exists, proceed to get the value
        string savedHostName = PlayerPrefs.GetString("HostName");
        _localInfo.name = savedHostName;
        Debug.Log($"Find Host Name: {_localInfo.name}");
    }
    else
    {
        // The key does not exist, handle it accordingly
        _localInfo.name = "UnityClient";
        Debug.Log($"Host Name not found, using default name {_localInfo.name}");
    }
  }

  void Start() {
    // OnServerDiscovered += StartConnection;
    // OnServerDiscovered += RegisterInfo2Server;
    // OnConnectionStart += RegisterInfo2Server;
    OnConnectionStart += () => { isConnected = true; };
    ConnectionSpin += () => {};
    OnDisconnected += StopConnection;
    lastTimeStamp = -1.0f;
    RegisterServiceCallback("ChangeHostName", ChangeHoseName);
  }

  void Update() {
    ConnectionSpin.Invoke();
    if (_discoveryClient.Available == 0) return; // there's no message to read
    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
    byte[] result = _discoveryClient.Receive(ref endPoint);
    string message =  Encoding.UTF8.GetString(result);
    if (!message.StartsWith("SimPub")) return; // not the right tag
    var split = message.Split(":", 3);
    // check if the message is from the same server
    if (_conncetionID != split[1]) {
      if (isConnected) OnDisconnected.Invoke();
      _conncetionID = split[1];
      string infoStr = split[2];
      _serverInfo = JsonConvert.DeserializeObject<HostInfo>(infoStr);
      _serverInfo.ip = endPoint.Address.ToString();
      _localInfo.ip = GetLocalIPsInSameSubnet(_serverInfo.ip);
      Debug.Log($"Discovered server at {_serverInfo.ip} with local IP {_localInfo.ip}");
      StartConnection();
      RegisterInfo2Server();
      OnConnectionStart.Invoke();
    }
    lastTimeStamp = Time.realtimeSinceStartup;
  }

  private void CaculateTimestampOffset() {
    float startTimer = Time.realtimeSinceStartup;
    float serverTime = float.Parse(RequestString("GetServerTimestamp"));
    float endTimer = Time.realtimeSinceStartup;
    timeOffset = (startTimer + endTimer) / 2 - serverTime;
    float requestDelay = (endTimer - startTimer) / 2 * 1000;
    Debug.Log($"Request Delay: {requestDelay} ms");
  }

  void OnApplicationQuit() {
    _discoveryClient.Dispose();
    foreach (var socket in _sockets) {
      socket.Dispose();
    }
    // This must be called after all the NetMQ sockets are disposed
    NetMQConfig.Cleanup();
  }

  public PublisherSocket GetPublisherSocket() {
    return _pubSocket;
  }

  // Please use these two request functions to send request to the server.
  // It may stuck if the server is not responding,
  // which will cause the Unity Editor to freeze.
  public string RequestString(string service, string request = "") {
    _reqSocket.SendFrame($"{service}:{request}");
    // string result = _reqSocket.TryReceiveFrame(out bool more);
    if (!_reqSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(5000), out string result, out bool more)) {
      Debug.LogWarning("Request Timeout");
      return "Request Timeout";
    }
    while(more) result += _reqSocket.ReceiveFrameString(out more);
    return result;
  }

  public List<byte> RequestBytes(string service, string request = "") {
    _reqSocket.SendFrame($"{service}:{request}");
    List<byte> result = new List<byte>();
    if (!_reqSocket.TryReceiveFrameBytes(TimeSpan.FromMilliseconds(5000), out byte[] bytes, out bool more)) {
      Debug.LogWarning("Request Timeout");
    }
    else {      
      result.AddRange(bytes);
      while (more) result.AddRange(_reqSocket.ReceiveFrameBytes(out more));
    }
    return result;
  }

  public void StartConnection() {
    if (isConnected) StopConnection();
    _subSocket.Connect($"tcp://{_serverInfo.ip}:{(int)ServerPort.Topic}");
    _subSocket.Subscribe("");
    ConnectionSpin += TopicUpdateSpin;
    Debug.Log($"Connected topic to {_serverInfo.ip}:{(int)ServerPort.Topic}");
    _resSocket.Bind($"tcp://{_localInfo.ip}:{(int)ClientPort.Service}");
    ConnectionSpin += ServiceRespondSpin;
    _reqSocket.Connect($"tcp://{_serverInfo.ip}:{(int)ServerPort.Service}");
    Debug.Log($"Starting service connection to {_serverInfo.ip}:{(int)ServerPort.Service}");
    _pubSocket.Bind($"tcp://{_localInfo.ip}:{(int)ClientPort.Topic}");
    isConnected = true;
    CaculateTimestampOffset();
  }

  public void StopConnection() {
    if (isConnected) {
      _subSocket.Disconnect($"tcp://{_serverInfo.ip}:{(int)ServerPort.Topic}");
      while (_subSocket.HasIn) _subSocket.SkipFrame();
    }
    ConnectionSpin -= TopicUpdateSpin;
    // It is not necessary to clear the topics callbacks
    // _topicsCallbacks.Clear();
    _resSocket.Unbind($"tcp://{_localInfo.ip}:{(int)ClientPort.Service}");
    ConnectionSpin -= ServiceRespondSpin;
    _pubSocket.Unbind($"tcp://{_localInfo.ip}:{(int)ClientPort.Topic}");
    _reqSocket.Disconnect($"tcp://{_serverInfo.ip}:{(int)ServerPort.Service}");
    isConnected = false;
    Debug.Log("Disconnected");
  }

  public void TopicUpdateSpin() {
    // Only process the latest message of each topic
    Dictionary<string, string> messageProcessed = new();
    while (_subSocket.HasIn)
    {
      string messageReceived = _subSocket.ReceiveFrameString();
      string[] messageSplit = messageReceived.Split(":", 2);
      if (_topicsCallbacks.ContainsKey(messageSplit[0])) {
        messageProcessed[messageSplit[0]] = messageSplit[1];
      }
      foreach (var (topic, msg) in messageProcessed) {
        _topicsCallbacks[topic](msg);
      }
    }
  }

  public void ServiceRespondSpin() {
    if (!_resSocket.HasIn) return;
    string messageReceived = _resSocket.ReceiveFrameString();
    string[] messageSplit = messageReceived.Split(":", 2);
    if (_serviceCallbacks.ContainsKey(messageSplit[0])) {
      string response = _serviceCallbacks[messageSplit[0]](messageSplit[1]);
      _resSocket.SendFrame(response);
    }
    else {
      Debug.LogWarning($"Service {messageSplit[0]} not found");
      _resSocket.SendFrame("Invalid Service");
    }
  }

  public void SubscribeTopic(string topic, Action<string> callback) {
    _topicsCallbacks[topic] = callback;
    Debug.Log($"Subscribe a new topic {topic}");
  }

  public void UnsubscribeTopic(string topic) {
    if (_topicsCallbacks.ContainsKey(topic)) _topicsCallbacks.Remove(topic);
  }

  public void CreatePublishTopic(string topic) {
    if (_localInfo.topics.Contains(topic)) Debug.LogWarning($"Topic {topic} already exists");
    _localInfo.topics.Add(topic);
    RegisterInfo2Server();
  }

  public void RegisterInfo2Server() {
    if (isConnected) {
      string data = JsonConvert.SerializeObject(_localInfo);
      RequestString("Register", JsonConvert.SerializeObject(_localInfo));
    }
  }

  public void RegisterServiceCallback(string service, Func<string, string> callback) {
    Debug.Log($"Register service {service}");
    _serviceCallbacks[service] = callback;
    _localInfo.services.Add(service);
  }

  public string GetHostName() {
    return _localInfo.name;
  }

  public static string GetLocalIPsInSameSubnet(string inputIPAddress)
  {
    IPAddress inputIP;
    if (!IPAddress.TryParse(inputIPAddress, out inputIP))
    {
      throw new ArgumentException("Invalid IP address format.", nameof(inputIPAddress));
    }
    IPAddress subnetMask = IPAddress.Parse("255.255.255.0");
    // Get all network interfaces
    NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
    foreach (NetworkInterface ni in networkInterfaces)
    {
      // Get IP properties of the network interface
      IPInterfaceProperties ipProperties = ni.GetIPProperties();
      UnicastIPAddressInformationCollection unicastIPAddresses = ipProperties.UnicastAddresses;
      foreach (UnicastIPAddressInformation ipInfo in unicastIPAddresses)
      {
        if (ipInfo.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
          IPAddress localIP = ipInfo.Address;
          Debug.Log($"Local IP: {localIP}");
          // Check if the IP is in the same subnet
          if (IsInSameSubnet(inputIP, localIP, subnetMask))
          {
            return localIP.ToString();;
          }
        }
      }
    }
    return "127.0.0.1";
  }

  private static bool IsInSameSubnet(IPAddress ip1, IPAddress ip2, IPAddress subnetMask)
  {
    byte[] ip1Bytes = ip1.GetAddressBytes();
    byte[] ip2Bytes = ip2.GetAddressBytes();
    byte[] maskBytes = subnetMask.GetAddressBytes();

    for (int i = 0; i < ip1Bytes.Length; i++)
    {
      if ((ip1Bytes[i] & maskBytes[i]) != (ip2Bytes[i] & maskBytes[i]))
      {
        return false;
      }
    }
    return true;
  }

  public string ChangeHoseName(string name) {
    _localInfo.name = name;
    PlayerPrefs.SetString("HostName", name);
    Debug.Log($"Change Host Name to {_localInfo.name}");
    PlayerPrefs.Save();
    return "Host Name Changed";
  }

  public bool CheckServerService(string serviceName) {
    if (!isConnected) return false;
    if (_serverInfo == null) return false;
    if (_serverInfo.services.Contains(serviceName)) return true;
    return false;
  }

}