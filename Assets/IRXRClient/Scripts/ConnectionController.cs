
using NetMQ;
using UnityEngine;



public class ConnectionController : MonoBehaviour {


  [SerializeField] private Discovery discovery;

  [SerializeField] private ServiceConnection serviceConnection;

  [SerializeField] private StreamingConnection streamingConnection;

  void Awake() {
    AsyncIO.ForceDotNet.Force();
  }

  void Start() {
    discovery.OnDiscoveryCompleted += OnDiscovery;
  }

  void OnDiscovery() {
    Debug.Log($"Discovered server {discovery.GetServerIp()}");
    serviceConnection.Connect(discovery.GetServerIp(), discovery.GetServivePort("SERVICE"));
    streamingConnection.Connect(discovery.GetServerIp(), discovery.GetServivePort("STREAMING"));
  }

  void OnApplicationQuit() {
    NetMQConfig.Cleanup();
  }

}
