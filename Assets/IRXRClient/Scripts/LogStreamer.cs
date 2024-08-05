using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;


public class LogStreamer : Streamer {

  private int counter = 0;

  void HandleLog(string logString, string stackTrace, LogType type) {
    _publisher.Publish(logString);
  }

  protected override void SetupTopic() {
    _topic = "Log";
  }

  protected override void Initialize() {
    Application.logMessageReceived += HandleLog;
  }

  void Update() {
    if (counter++ % 100 == 0) _publisher.Publish("Heartbeat");
    // CaculateTimestampDiff();
  }

  // TODO: caculate the timestamp difference between the server and the client
  void CaculateTimestampDiff() {
    // _publisher.Publish("CaculateTimestampDiff");
  }


}