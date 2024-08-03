using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;


public class LogStreamer : Streamer {

  void HandleLog(string logString, string stackTrace, LogType type) {
    string msg = "UnityLog" + ":" + logString;
    _pubSocket.SendFrame(msg);
  }

  void Initialize() {
    Application.logMessageReceived += HandleLog;
  }

}