using System.Net;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using System.Net.Sockets;
using System.Collections.Generic;


public class Discovery : MonoBehaviour
{
    public delegate void DiscoveryCompletedEventHandler();

    public event DiscoveryCompletedEventHandler DiscoveryCompleted;

    private string id = "";
    private string _serverIP;

    private UdpClient _discoveryClient;

    private Dictionary<string, string> _informations;

    void Awake() {
        _discoveryClient = new UdpClient(7720);
    }

  
    void Update()
    {
        if (_discoveryClient.Available == 0) return; // there's no message to read
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
        byte[] result = _discoveryClient.Receive(ref endPoint);
        string message =  Encoding.UTF8.GetString(result);

        if (!message.StartsWith("SimPub")) return; // not the right tag

        var split = message.Split(":", 3);

        if (split[1] == id) return; // same id

        id = split[1];

        string info = split[2];
        _informations = JsonConvert.DeserializeObject<Dictionary<string, string>>(info);
        _serverIP = endPoint.Address.ToString();

        DiscoveryCompleted.Invoke();
    }

    void OnApplicationQuit() {
        _discoveryClient.Dispose();
    }

    public bool HasDiscovered() {
        return _serverIP != null;
    }

    public string GetServerIp() {
        return _serverIP;
    }

    public int GetServivePort(string service) {
        int.TryParse(_informations[service], out int result);
        return result;
    }

}
