using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Schema;

/*
DOCS:
- https://netmq.readthedocs.io/en/latest/request-response/

Note to self: no async with netmq - DOES NOT WORK 

*/
public class ServiceConnection : MonoBehaviour {

  public Action OnServiceConnection; 
  private RequestSocket requestSocket;

  private Task _currentTask;

  void Awake() {
    requestSocket = new RequestSocket();
   }

  public void connect(string server_ip, int server_port) {
    requestSocket.Connect($"tcp://{server_ip}:{server_port}");
    Debug.Log("Connected service socket to " + $"tcp://{server_ip}:{server_port}");

    if (_currentTask != null && !_currentTask.IsCompleted) _currentTask.Dispose();

    _currentTask = Task.Run(() => {
      try {
        OnServiceConnection();
      } catch (Exception e) {
        Debug.LogException(e.InnerException);
      }
    });
  }

  public string request_string(string request_string) {
    requestSocket.SendFrame(request_string);

    string result = requestSocket.ReceiveFrameString(out bool more);
    while(more) result += requestSocket.ReceiveFrameString(out more);

    return result;
  }

  public List<byte> request_bytes(string request_string) {

    requestSocket.SendFrame(request_string);

    List<byte> result = new List<byte>(requestSocket.ReceiveFrameBytes(out bool more));
    while (more) result.AddRange(requestSocket.ReceiveFrameBytes(out more));
    
    return result;
  }

  void OnApplicationQuit() {
    requestSocket.Dispose();
  }

}