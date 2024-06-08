
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
    discovery.DiscoveryCompleted += OnDiscovery;
  }

  void OnDiscovery() {
    Debug.Log($"Discovered server {discovery.get_server_ip()}");
    serviceConnection.connect(discovery.get_server_ip(), discovery.get_service_port());
    streamingConnection.connect(discovery.get_server_ip(), discovery.get_streaming_port());
  }

  void OnApplicationQuit() {
    NetMQConfig.Cleanup();
  }

}
