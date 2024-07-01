using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Schema;

public class ServiceConnection : MonoBehaviour {

  public Action OnServiceConnection; 
  private RequestSocket requestSocket;

  private Task _currentTask;

  void Awake() {
    requestSocket = new RequestSocket();
   }

  public void Connect(string server_ip, int server_port) {
    requestSocket.Connect($"tcp://{server_ip}:{server_port}");
    Debug.Log("Connected service socket to " + $"tcp://{server_ip}:{server_port}");

    if (_currentTask != null && !_currentTask.IsCompleted) _currentTask.Dispose();
    Debug.Log("Starting service connection task");
    _currentTask = Task.Run(() => {
      try {
        OnServiceConnection();
      } catch (Exception e) {
        Debug.LogException(e.InnerException);
      }
    });
  }

  public string RequestString(string requestString) {
    requestSocket.SendFrame(requestString);
    string result = requestSocket.ReceiveFrameString(out bool more);
    while(more) result += requestSocket.ReceiveFrameString(out more);
    return result;
  }

  public List<byte> RequestBytes(string requestString) {
    requestSocket.SendFrame(requestString);
    List<byte> result = new List<byte>(requestSocket.ReceiveFrameBytes(out bool more));
    while (more) result.AddRange(requestSocket.ReceiveFrameBytes(out more));
    return result;
  }

  // TODO: Working on this
  public T SendRequest<T>(string requestString) {
    string response = RequestString(requestString);
    return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(response);
  }

  void OnApplicationQuit() {
    requestSocket.Dispose();
  }

}