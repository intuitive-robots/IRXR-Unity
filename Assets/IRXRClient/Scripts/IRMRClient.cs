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
using Newtonsoft.Json;
using System.Net;
using System.Net.NetworkInformation;
using System;


namespace IRXR
{
    public class IRXRClient : IRXRSingleton<IRXRClient>
    {

        public enum ClientState
        {
            Searching = 0,
            OnConnected = 1,
            Disconnected = 2,
            Reconnecting = 3,
            Closing = 4,
            Closed = 5,
        }


        // The websocket setting
        private Task _WebSocketRunThread;
        private WebSocket _WebSocket;
        private string _ConnectedIP;
        private ClientState _clientState = ClientState.Disconnected;
        private string port;

        private Dictionary<string, Delegate> _MsgCallbackDict;

        private void Start() 
        {

        }

        private void Update() {        
            if (_clientState == ClientState.OnConnected)
            {
                _WebSocket.DispatchMessageQueue();

                // if (updateMsg != null)
                // {
                //     OnSceneUpdate?.Invoke(updateMsg.Data);
                //     SendManipulableObjData();
                // }
            }
            else if (_clientState == ClientState.Disconnected)
            {
                // SFObjectManager.Instance.ClearAllObjects();
                // UIObjectManager.Instance.ClearAllObjects();
                if (_ConnectedIP != null)
                {
                    _clientState = ClientState.Reconnecting;
                    StartCoroutine(Reconnect());
                }
                else
                {
                    _clientState = ClientState.Searching;
                    StartCoroutine(SearchForWebSocket());
                }
            }
        }

        public void SubscribeCallback(string header, Delegate callback)
        {
            if (_MsgCallbackDict.ContainsKey(header))
            {
                _MsgCallbackDict[header] = Delegate.Combine(_MsgCallbackDict[header], callback);
            }
            else
            {
                _MsgCallbackDict[header] = callback;
            }
        }


        private void StartConnection(string ipAddress, string port)
        {
            Debug.Log($"Try the connection to {ipAddress}:{port}");
            WebSocket testWebSocket = new WebSocket($"ws://{ipAddress}:{port}");
            testWebSocket.OnOpen += () =>
            {
                Debug.Log($"Connection open with {ipAddress}!");
            };

            testWebSocket.OnError += (e) =>
            {
                testWebSocket.CancelConnection();
            };

            Task connectTask = testWebSocket.Connect();

            if (testWebSocket.State == WebSocketState.Open && _WebSocket == null)
            {
                Debug.Log($"Find a WebSocket server in {ipAddress}");
                _ConnectedIP = ipAddress;
                _WebSocket = testWebSocket;
                _clientState = ClientState.OnConnected;

                _WebSocket.OnOpen += () =>
                {
                    _WebSocket.SendText("Heollo");
                };

                _WebSocket.OnError += (e) =>
                {
                    Debug.Log("Error! " + e);
                    Task.Run(async () => {await _WebSocket.Close();});
                };

                _WebSocket.OnClose += (e) =>
                {
                    Debug.Log($"Connection with {this._ConnectedIP} is closed!");
                };

                _WebSocket.OnMessage += (bytes) =>
                {
                    // getting the message as a string
                    var str = Encoding.UTF8.GetString(bytes);
                    IRXRMsg msg = JsonConvert.DeserializeObject<IRXRMsg>(str);
                    if (!_MsgCallbackDict.ContainsKey(msg.header))
                    {
                        return;
                    }
                    _MsgCallbackDict[msg.header]?.DynamicInvoke(msg.data);
                };

                _WebSocketRunThread = Task.Run(async () => 
                {
                    // await AsyncSendRequest("start_stream", "");
                    await connectTask;
                    Debug.Log($"Quit WebSocket run thread");
                    _clientState = ClientState.Disconnected;
                    _WebSocket = null;
                });
                Debug.Log("Start a new thread for WebSocket");
            }
        }


        public void SendRequest(string type, string value)
        {
            Dictionary<string, string> req = new Dictionary<string, string>();
            req["Type"] = type;
            req["Value"] = value;
            _WebSocket.SendText(JsonConvert.SerializeObject(req));
        }

        public async Task AsyncSendRequest(string type, string value)
        {
            Dictionary<string, string> req = new Dictionary<string, string>();
            req["Type"] = type;
            req["Value"] = value;
            await _WebSocket.SendText(JsonConvert.SerializeObject(req));
        }

        private void OnApplicationQuit()
        {
            if (_WebSocket != null)
            {
                Task closeTask = Task.Run(
                    async () => {
                        await _WebSocket.Close();
                        }
                    );
                closeTask.Wait();
            }
        }

    }
}