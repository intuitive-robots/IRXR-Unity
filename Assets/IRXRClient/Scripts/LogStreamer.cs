using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;


public class LogStreamer : Streamer {

  private int frameCounter = 0;
  private float timer = 0;

  void HandleLog(string logString, string stackTrace, LogType type) {
    _publisher.Publish(logString);
  }

  protected override void SetupTopic() {
    _topic = "Log";
  }

  protected override void Initialize() {
    Application.logMessageReceived += HandleLog;
    timer = Time.realtimeSinceStartup;
  }

  void Update() {
    frameCounter += 1;
    float totalTime = Time.realtimeSinceStartup - timer;
    if (totalTime > 5.0f) {
      float fps = frameCounter / totalTime;
      Debug.Log("Average FPS in the last 5s: " + fps);
      timer = Time.realtimeSinceStartup;
      frameCounter = 0;
    }
  }

}