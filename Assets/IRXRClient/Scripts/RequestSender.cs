using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json.Schema;

public class RequestSender : MonoBehaviour {

  public Action OnServiceConnection; 
  private RequestSocket _requestSocket;
  private Task _currentTask;
  private IRXRNetManager _netManager;
  public bool isServiceConnected = false;

  void Awake() {
    _netManager = gameObject.GetComponent<IRXRNetManager>();
    _requestSocket = _netManager.GetRequestSocket();
    _netManager.OnDiscoveryCompleted += Connect;
    isServiceConnected = false;
   }

  void Start() {
    OnServiceConnection += () => Debug.Log("Service Connection Established");
  }

  public void Connect() {
    Connect(_netManager.GetServerIp(), _netManager.GetServerPort("SERVICE"));
  }

  public void Connect(string server_ip, int server_port) {
    _requestSocket.Connect($"tcp://{server_ip}:{server_port}");
    Debug.Log("Connected service socket to " + $"tcp://{server_ip}:{server_port}");
    isServiceConnected = true;
    if (_currentTask != null && !_currentTask.IsCompleted) _currentTask.Dispose();
    Debug.Log("Starting service connection task");
    OnServiceConnection.Invoke();
  }

  public string RequestString(string requestString) {
    _requestSocket.SendFrame(requestString);
    string result = _requestSocket.ReceiveFrameString(out bool more);
    while(more) result += _requestSocket.ReceiveFrameString(out more);
    return result;
  }

  public List<byte> RequestBytes(string requestString) {
    _requestSocket.SendFrame(requestString);
    List<byte> result = new List<byte>(_requestSocket.ReceiveFrameBytes(out bool more));
    while (more) result.AddRange(_requestSocket.ReceiveFrameBytes(out more));
    return result;
  }

}