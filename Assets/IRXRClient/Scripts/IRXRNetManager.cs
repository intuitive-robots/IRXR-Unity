
using NetMQ;
using UnityEngine;
using System;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Net.Sockets;
using System.Collections.Generic;


public class IRXRNetManager : Singleton<IRXRNetManager> {

  // public delegate void DiscoveryEventHandler();
  public Action OnDiscoveryCompleted;
  public Action OnNewServerDiscovered;
  private string _id = null;
  private string _serverIP;

  private UdpClient _discoveryClient;

  private Dictionary<string, string> _informations;

  private List<NetMQSocket> _sockets = new List<NetMQSocket>();

  void Awake() {
    AsyncIO.ForceDotNet.Force();
    _discoveryClient = new UdpClient(7720);
  }

  void Start() {
    OnNewServerDiscovered += () => Debug.Log("New Server Discovered");
    OnDiscoveryCompleted += () => { };
  }

  void Update()
  {
    if (_discoveryClient.Available == 0) return; // there's no message to read
    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
    byte[] result = _discoveryClient.Receive(ref endPoint);
    string message =  Encoding.UTF8.GetString(result);

    if (!message.StartsWith("SimPub")) return; // not the right tag

    var split = message.Split(":", 3);

    if (split[1] != _id){
      OnNewServerDiscovered.Invoke();
      _id = split[1];
      string info = split[2];
      _informations = JsonConvert.DeserializeObject<Dictionary<string, string>>(info);
      _serverIP = endPoint.Address.ToString();
      OnDiscoveryCompleted.Invoke();
    }
  }

  void OnApplicationQuit() {
    _discoveryClient.Dispose();
    foreach (var socket in _sockets) {
      socket.Dispose();
    }
    // This must be called after all the NetMQ sockets are disposed
    NetMQConfig.Cleanup();
  }

  public bool HasDiscovered() {
      return _serverIP != null;
  }

  public string GetServerIp() {
      return _serverIP;
  }

  public int GetServerPort(string service) {
      int.TryParse(_informations[service], out int result);
      return result;
  }

  public NetMQSocket CreateSocket(NetMQSocket socket) {
    _sockets.Add(socket);
    return socket;
  }

}
