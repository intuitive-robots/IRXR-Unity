/*
 * UnityClient.cs
 * Core scripts for connecting to SF
 * Receiving and sending message to SF
 */

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using System.Collections;
using NativeWebSocket;
// using Newtonsoft.Json;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System;


namespace IRXR
{
    
    public class IRXRClient : IRXRSingleton<IRXRClient>
    {

        public enum ClientState
        {
            Searching = 0,
            Connected = 1,
            Disconnected = 2,
            Reconnecting = 3,
            Closing = 4,
            Closed = 5,
        }


        // The websocket setting
        private Task _webSocketRunThread;
        private WebSocket _webSocket;
        private string _connectedIP = null;
        private ClientState _clientState = ClientState.Disconnected;
        private string port = "8052";
        private Action updateAction;
        public delegate void Subscriber(string message);
        private Dictionary<string, Subscriber> subscribers = new Dictionary<string, Subscriber>();

        private void Start() 
        {
            _clientState = ClientState.Searching;
            updateAction = SearchingAction;
            StartCoroutine(SearchForWebSocket());
        }

        private void Update() {
            updateAction?.Invoke();
            // if (_clientState == ClientState.Connected)
            // {
            //     _webSocket.DispatchMessageQueue();

            //     // if (updateMsg != null)
            //     // {
            //     //     OnSceneUpdate?.Invoke(updateMsg.Data);
            //     //     SendManipulableObjData();
            //     // }
            // }
            // else if (_clientState == ClientState.Disconnected)
            // {
            //     // SFObjectManager.Instance.ClearAllObjects();
            //     // UIObjectManager.Instance.ClearAllObjects();
            //     if (_connectedIP != null)
            //     {
            //         StartConnection(_ConnectedIP, port);
            //         // StartCoroutine(StartConnection(_ConnectedIP, port));
            //         _clientState = ClientState.Reconnecting;
            //         // StartCoroutine(Reconnect());
            //     }
            //     else
            //     {
            //         _clientState = ClientState.Searching;
            //         // StartCoroutine(SearchForWebSocket());
            //     }
            // }
        }

        private void SearchingAction()
        {

        }

        private void ConnectedAction()
        {
            _webSocket.DispatchMessageQueue();
        }

        private void DisconnectedAction()
        {
            StartCoroutine(Reconnect());
            updateAction = ReconnectAction;
        }

        private void ReconnectAction()
        {
            
        }


        public void SubscribeCallback(string header, Subscriber callback)
        {
            if (subscribers.ContainsKey(header))
            {
                subscribers[header] += callback;
            }
            else
            {
                subscribers[header] = callback;
            }
        }

        // public void SendRequest(string type, string value)
        // {
        //     Dictionary<string, string> req = new Dictionary<string, string>();
        //     req["Type"] = type;
        //     req["Value"] = value;
        //     _webSocket.SendText(JsonConvert.SerializeObject(req));
        // }

        // public async Task AsyncSendRequest(string type, string value)
        // {
        //     Dictionary<string, string> req = new Dictionary<string, string>();
        //     req["Type"] = type;
        //     req["Value"] = value;
        //     await _webSocket.SendText(JsonConvert.SerializeObject(req));
        // }

        private void OnDestroy()
        {
            if (_webSocket != null)
            {
                Debug.Log("Close application and shut down the Server");
                Task closeTask = Task.Run(
                    async () => {
                        await _webSocket.Close();
                        }
                    );
                closeTask.Wait();
            }
        }

        private IEnumerator SearchForWebSocket()
        {
            Debug.Log($"Searching for server");
            List<Coroutine> coroutineList = new List<Coroutine>();
            foreach (string ipAddress in GetTestIPList())
            {
                if (_clientState == ClientState.Connected) break;
                
                coroutineList.Add(StartCoroutine(TryWebSockets(ipAddress)));
            }
            foreach (Coroutine coroutine in coroutineList)
            {
                yield return coroutine;
            }
            if (_connectedIP == null) _clientState = ClientState.Disconnected;
        }


        private IEnumerator TryWebSockets(string ipAddress)
        {
            WebSocket testWebSocket = new WebSocket($"ws://{ipAddress}:{port}");
            Debug.Log($"Start to try ip: {ipAddress}:{port}");
            Task connectTask = testWebSocket.Connect();

            // wait until the websocket has updated 
            yield return new WaitUntil(() => testWebSocket.State == WebSocketState.Open || testWebSocket.State == WebSocketState.Closed);

            if (testWebSocket.State == WebSocketState.Open && _webSocket == null)
            {
                Debug.Log($"Found a WebSocket server in {ipAddress}");
                _connectedIP = ipAddress;
                _webSocket = testWebSocket;
                _clientState = ClientState.Connected;

                _webSocket.OnClose += OnWSClose;
                _webSocket.OnError += OnWSError;
                _webSocket.OnMessage += OnWSMessage;

                updateAction = ConnectedAction;

                Task.Run(async () =>
                {
                    await connectTask;
                    Debug.Log("Quit WebSocket run thread");
                    _clientState = ClientState.Disconnected;
                    _webSocket = null;
                });
            }

        }

        private void OnWSClose(WebSocketCloseCode ws_event) {
            Debug.Log($"Connection with {_connectedIP} is closed!");
            _clientState = ClientState.Reconnecting;
        }

        private void OnWSError(string error) {
            _clientState = ClientState.Reconnecting;
        }

        private void OnWSMessage(byte[] bytes) {
            
            // getting the message as a string
            IRXRMsgPack pack = new IRXRMsgPack(Encoding.UTF8.GetString(bytes));
            if (!subscribers.ContainsKey(pack.header))
            {
                return;
            }
            subscribers[pack.header]?.DynamicInvoke(pack.msg);
        }


        private IEnumerator Reconnect()
        {
            _clientState = ClientState.Reconnecting;
            yield return StartCoroutine(TryWebSockets(_connectedIP));
            if (_clientState == ClientState.Reconnecting)
            {
                _clientState = ClientState.Disconnected;
                updateAction = DisconnectedAction;
            }
        }


        private static List<string> GetTestIPList()
        {
            List<string> ipSearchList = new List<string>();

            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                NetworkInterfaceType type = adapter.NetworkInterfaceType;

                //easier to read when split
                if (type != NetworkInterfaceType.Wireless80211 && type != NetworkInterfaceType.Ethernet) continue;
                if (adapter.OperationalStatus != OperationalStatus.Up || !adapter.Supports(NetworkInterfaceComponent.IPv4)) continue;

                foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork) GetSubnetIPs(address.Address, ipSearchList);
                }
            }
            ipSearchList.Add("127.0.0.1");
            return ipSearchList;
        }


        private static void GetSubnetIPs(IPAddress ipAddress, List<string> ip_addresses)
        {

            byte[] networkAddressBytes = ipAddress.GetAddressBytes();
            int byte_length = networkAddressBytes.Length - 1;

            for (byte i = 1; i < 255; i++)
            {
                networkAddressBytes[byte_length] = i;
                ip_addresses.Add(new IPAddress(networkAddressBytes).ToString());
            }
        }

    }
}