using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NetMQ;
using NetMQ.Sockets;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using IRXR.Utilities;


namespace IRXR.Node
{

	public static class UnityPortSet
	{
		public static readonly int DISCOVERY = 7720;
		public static readonly int SERVICE = 7730;
		public static readonly int TOPIC = 7731;
	}

	public class NodeInfo
	{
		public string name;
		public string nodeID;
		public string ip;
		public string type;
		public int servicePort;
		public int topicPort;
		public List<string> serviceList = new();
		public List<string> topicList = new();
	}



	public class NodeInfoManager
	{
		private Dictionary<string, NodeInfo> _nodesInfo;
		private NodeInfo _localInfo;
		// Unity node is not master node
		private NodeInfo _serverInfo;
		private string _nodeId;

		public NodeInfoManager(NodeInfo localInfo)
		{
			_localInfo = localInfo;
			_nodeId = localInfo.nodeID;
			_nodesInfo = new Dictionary<string, NodeInfo>
			{
				{ localInfo.nodeID, localInfo }
			};
		}

		public void UpdateNodesInfo(Dictionary<string, NodeInfo> nodesInfoDict, IRXRNetManager node)
		{
			_nodesInfo = nodesInfoDict;
			_localInfo = _nodesInfo[_nodeId];
			node.localInfo = _localInfo;
		}

		public byte[] GetNodesInfoMessage()
		{
			string json = JsonConvert.SerializeObject(_nodesInfo);
			return Encoding.UTF8.GetBytes(json);
		}

		public bool CheckNode(string nodeId)
		{
			return _nodesInfo.ContainsKey(nodeId);
		}

		public NodeInfo GetNode(string nodeId)
		{
			if (_nodesInfo.ContainsKey(nodeId))
			{
				return _nodesInfo[nodeId];
			}
			return null;
		}

		public NodeInfo CheckService(string serviceName)
		{
			foreach (var info in _nodesInfo.Values)
			{
				if (info.serviceList.Contains(serviceName))
				{
					return info;
				}
			}
			return null;
		}

		public NodeInfo CheckTopic(string topicName)
		{
			foreach (var info in _nodesInfo.Values)
			{
				if (info.topicList.Contains(topicName))
				{
					return info;
				}
			}
			return null;
		}

	}


	public class IRXRNetManager : MonoBehaviour
	{
		// Singleton instance
		public static IRXRNetManager Instance { get; private set; }
		// Node information
		public NodeInfo localInfo { get; set; }
		public NodeInfoManager nodeInfoManager { get; private set; }
		// UDP Task management
		private CancellationTokenSource cancellationTokenSource;
		private Task nodeTask;
		public Action OnConnectionStart;
		public Action OnDisconnected;
		// ZMQ Sockets for communication, in this stage, we run them in the main thread
		// publisher socket for sending messages to other nodes
		public PublisherSocket _pubSocket;
		// response socket for service running in the local node
		public ResponseSocket _resSocket;
		public Dictionary<string, Func<string, string>> serviceCallbacks { get; private set; }
		// TODO: subscriber socket for receiving messages from only master node
		private SubscriberSocket _subSocket;
		public Dictionary<string, Action<byte[]>> subscribeCallbacks { get; private set; }
		// TODO: Request socket for sending service request to only master node
		private RequestSocket _reqSocket;
		private List<NetMQSocket> _sockets;
		public Action ConnectionSpin;
		// Status flags
		private bool isRunning = false;
		private bool isConnected = false;

		// Constants
		private const int HEARTBEAT_INTERVAL = 500;

		private void Awake()
		{
			AsyncIO.ForceDotNet.Force();
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			DontDestroyOnLoad(gameObject);
			// Initialize local node info
			localInfo = new NodeInfo
			{
				name = "UnityNode",
				nodeID = Guid.NewGuid().ToString(),
				ip = null,
				type = "UnityNode",
				servicePort = UnityPortSet.SERVICE,
				topicPort = UnityPortSet.TOPIC,
				serviceList = new List<string>(),
				topicList = new List<string>()
			};
			nodeInfoManager = new NodeInfoManager(localInfo);
			// Default host name
			if (PlayerPrefs.HasKey("HostName"))
			{
				// The key exists, proceed to get the value
				string savedHostName = PlayerPrefs.GetString("HostName");
				localInfo.name = savedHostName;
				Debug.Log($"Find Host Name: {localInfo.name}");
			}
			else
			{
				// The key does not exist, handle it accordingly
				localInfo.name = "UnityNode";
				Debug.Log($"Host Name not found, using default name {localInfo.name}");
			}
			// NOTE: Since the NetZMQ setting is initialized in "AsyncIO.ForceDotNet.Force();"
			// NOTE: we should initialize the sockets after that
			_pubSocket = new PublisherSocket();
			_resSocket = new ResponseSocket();
			_subSocket = new SubscriberSocket();
			_reqSocket = new RequestSocket();
			_sockets = new List<NetMQSocket>() { _pubSocket, _resSocket, _subSocket, _reqSocket };
			serviceCallbacks = new Dictionary<string, Func<string, string>>();
			subscribeCallbacks = new Dictionary<string, Action<byte[]>>();
			cancellationTokenSource = new CancellationTokenSource();
			// Action setting
			OnConnectionStart += () => { isConnected = true; };
			OnDisconnected += () => { isConnected = false; };
		}

		private void Start()
		{
			// Start tasks
			isRunning = true;
			nodeTask = Task.Run(async () => await NodeTask(), cancellationTokenSource.Token);
		}

		private void Update() {
			ConnectionSpin?.Invoke();
		}

		private void StopTask()
		{
			Debug.Log("Stopping task...");
			isConnected = false;
			isRunning = false;
			if (cancellationTokenSource != null)
			{
				cancellationTokenSource.Cancel();
				nodeTask?.Wait();
				cancellationTokenSource.Dispose();
				Debug.Log("Task has been stopped safely.");
			}
		}

		private void OnDestroy()
		{
			StopTask();
		}

		private void OnApplicationQuit()
		{
			Debug.Log("Net Manager is being destroyed.");
			foreach (var sock in _sockets)
			{
				// sock.Close();
				sock?.Dispose();
			}
			NetMQConfig.Cleanup();
		}

	public void StartConnection() {
		if (isConnected) StopConnection();
		_subSocket.Connect($"tcp://{serverInfo.addr.ip}:{serverInfo.topicPort}");
		_subSocket.Subscribe("");
		ConnectionSpin += SubscriptionSpin;
		CheckService(serverInfo);
		// Debug.Log($"Connected topic to {serverInfo.ip}:{serverInfo.topicPort}");
		_resSocket.Bind($"tcp://{localInfo.addr.ip}:{UnityPortSet.SERVICE}");
		Debug.Log($"Starting service connection at {localInfo.addr.ip}:{UnityPortSet.SERVICE}");
		ConnectionSpin += ServiceRespondSpin;
		// _reqSocket.Connect($"tcp://{serverInfo.ip}:{serverInfo.servicePort}");
		// Debug.Log($"Starting service connection to {serverInfo.ip}:{serverInfo.servicePort}");
		_pubSocket.Bind($"tcp://{localInfo.addr.ip}:{UnityPortSet.TOPIC}");
		Debug.Log($"Starting publish topic at {localInfo.addr.ip}:{UnityPortSet.TOPIC}");
		// CaculateTimestampOffset();
	}

	public void StopConnection() {
		if (isConnected) {
		// _subSocket.Disconnect($"tcp://{serverInfo.ip}:{serverInfo.topicPort}");
		while (_subSocket.HasIn) _subSocket.SkipFrame();
		}
		ConnectionSpin -= TopicUpdateSpin;
		// It is not necessary to clear the topics callbacks
		// _topicsCallbacks.Clear();
		_resSocket.Unbind($"tcp://{_localInfo.ip}:{(int)ClientPort.Service}");
		ConnectionSpin -= ServiceRespondSpin;
		_pubSocket.Unbind($"tcp://{_localInfo.ip}:{(int)ClientPort.Topic}");
		_reqSocket.Disconnect($"tcp://{_serverInfo.ip}:{_serverInfo.servicePort}");
		isConnected = false;
		Debug.Log("Disconnected");
	}

		public async Task NodeTask()
		{
			Debug.Log("Node task starts and listening for master node...");
			while (isRunning)
			{
				try
				{
					var masterAddress = await SearchForMasterNode();
					if (masterAddress is not null)
					{
						Debug.Log("Master node found and ready to send heartbeat.");
						IPEndPoint masterEndPoint = new IPEndPoint(IPAddress.Parse(masterAddress.ip), masterAddress.port);
						await HeartbeatLoop(masterEndPoint);
					}
					await Task.Delay(500);
				}
				catch (Exception e)
				{
					Debug.LogError($"Error in NodeTask: {e.Message}");
					break;
				}
			}
			updateInfoTask?.Wait();
			Debug.Log("Node task stopped.");
		}

		public async Task<NodeInfo> SearchForMasterNode(int timeout = 500)
		{
			Debug.Log("Searching for the master node...");
			try
			{
				while (isRunning)
				{
					UdpClient udpClient = new UdpClient();
					udpClient.EnableBroadcast = true;
					udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, UnityPortSet.DISCOVERY));
					Debug.Log("Sending ping message..." + udpClient.Available);
					byte[] pingMessage = EchoHeader.PING;
					// await send so we don't need to worry about broadcasting in the loop by mistake
					// udpClient.Send(pingMessage, pingMessage.Length, new IPEndPoint(IPAddress.Broadcast, UnityPortSet.DISCOVERY));
					// waiting for receive of ping
					var receiveTask = udpClient.ReceiveAsync();
					if (await Task.WhenAny(receiveTask, Task.Delay(timeout)) == receiveTask)
					{
						Debug.Log("Received ping response.");
						var response = receiveTask.Result;
						byte[][] responseMessage = MsgUtils.SplitByte(response.Buffer);
						NodeInfo nodesInfo = MsgUtils.Deserialize2Object<NodeInfo>(receiveTask.Result);
						Debug.Log($"Found master node at {masterAddress.ip}:{masterAddress.port}");
						// pubSocket.Bind($"tcp://{masterAddress.ip}:{UnityPortSet.TOPIC}");
						OnConnectionStart?.Invoke();
						udpClient.Close();
						break;
					}
					udpClient.Close();
				}
			}
			catch (SocketException)
			{
				Debug.Log("No response, retrying...");
			}
			catch (Exception e)
			{
				Debug.LogError($"Error during master node discovery: {e.StackTrace}");
			}
			return masterAddress;
		}

		public async Task HeartbeatLoop(IPEndPoint masterAddress)
		{
			UdpClient udpClient = new UdpClient();
			udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, UnityPortSet.HEARTBEAT));
			// Start the update info loop
			CancellationTokenSource cts = new CancellationTokenSource();
			updateInfoTask = Task.Run(async () => await UpdateInfoLoop(udpClient, 3 * HEARTBEAT_INTERVAL), cancellationTokenSource.Token);
			Debug.Log($"The Net Manager starts heartbeat at {localInfo.addr.ip}:{localInfo.addr.port}");
			while (isConnected)
			{
				try
				{
					byte[] msg = MsgUtils.GenerateNodeMsg(localInfo);
					Debug.Log($"Sending heartbeat to {masterAddress}");
					await udpClient.SendAsync(msg, msg.Length, masterAddress);
					await Task.Delay(HEARTBEAT_INTERVAL);
				}
				catch (SocketException ex)
				{
					Debug.Log($"SocketException: {ex.Message}");
					OnDisconnected?.Invoke();
					break;
				}
				catch (Exception e)
				{
					Debug.LogError($"Failed to send heartbeat: {e.StackTrace}");
				}
			}
			updateInfoTask?.Wait();
			udpClient.Close();
			Debug.Log("Heartbeat loop has been stopped, waiting for other master node.");
		}

		public async Task UpdateInfoLoop(UdpClient udpClient, int timeout)
		{
			Debug.Log("Start update info loop");
			while (isConnected)
			{
				try
				{
					Debug.Log("Updating node info...");
					var receiveTask = udpClient.ReceiveAsync();
					if (await Task.WhenAny(receiveTask, Task.Delay(timeout)) == receiveTask)
					{
						var result = await receiveTask;
						Debug.Log("Received node info." + Encoding.UTF8.GetString(result.Buffer));
						Dictionary<string, NodeInfo> nodesInfo = MsgUtils.Deserialize2Object<Dictionary<string, NodeInfo>>(result.Buffer);
						nodeInfoManager.UpdateNodesInfo(nodesInfo, this);
					}
					else
					{
						Debug.LogWarning("Timeout: The master node is offline");
						OnDisconnected?.Invoke();
					}
				}
				catch (SocketException ex)
				{
					Debug.Log($"SocketException: {ex.Message}");
					OnDisconnected?.Invoke();
					break;
				}
				catch (Exception e)
				{
					Debug.LogError($"Error occurred in update info loop: {e.StackTrace}");
				}
			}
		}

		public void SubscriptionSpin()
		{
			// Only process the latest message of each topic
			Dictionary<string, byte[]> messageProcessed = new();
			while (_subSocket.HasIn)
			{
				byte[][] msgSeparated = MsgUtils.SplitByte(_subSocket.ReceiveFrameBytes());
				string topic = Encoding.UTF8.GetString(msgSeparated[0]);
				if (subscribeCallbacks.ContainsKey(topic))
				{
					messageProcessed[topic] = msgSeparated[1];
				}
			}
			foreach (var (topic, msg) in messageProcessed)
			{
				subscribeCallbacks[topic](msg);
			}
		}

		public void ServiceRespondSpin()
		{
			if (!_resSocket.HasIn) return;
			// TODO: make it as a byte array
			// TODO: make it running in the sub thread
			// now we need to carefully handle the service request
			// make sure that it would not block the main thread
			string messageReceived = _resSocket.ReceiveFrameString();
			string[] messageSplit = messageReceived.Split(MsgUtils.SEPARATOR, 2);
			Debug.Log($"Received service request {messageSplit[0]}");
			if (serviceCallbacks.ContainsKey(messageSplit[0]))
			{
				string response = serviceCallbacks[messageSplit[0]](messageSplit[1]);
				_resSocket.SendFrame(response);
			}
			else
			{
				Debug.LogWarning($"Service {messageSplit[0]} not found");
				_resSocket.SendFrame(MSG.SERVICE_ERROR);
			}
		}

		// TODO: make it as a generic request type
		public byte[] CallBytesService(string service_name, string request)
		{
			_reqSocket.SendFrame($"{service_name}:{request}");
			if (!_reqSocket.TryReceiveFrameBytes(TimeSpan.FromMilliseconds(10000), out byte[] bytes, out bool more)) {
				Debug.LogWarning($"Request Timeout");
				return new byte[] { };
			}

			List<byte> result = new List<byte>(bytes);
			result.AddRange(bytes);
			while (more) result.AddRange(_reqSocket.ReceiveFrameBytes(out more));
			return result.ToArray();
		}

		public RequestSocket CreateRequestSocket()
		{
			RequestSocket requestSocket = new RequestSocket();
			_sockets.Add(requestSocket);
			return requestSocket;
		}

		public SubscriberSocket CreateSubscriberSocket()
		{
			SubscriberSocket subscriberSocket = new SubscriberSocket();
			_sockets.Add(subscriberSocket);
			return subscriberSocket;
		}

	}


}
