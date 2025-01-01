using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using IRXR.Utilities;

namespace IRXR.Node
{

	public class IRXRNetManager : MonoBehaviour
	{
		// Singleton instance
		public static IRXRNetManager Instance { get; private set; }
		// Node information
		public NodeInfo localInfo { get; set; }
		public NodeInfo masterInfo { get; set; }
		// public NodeInfoManager nodeInfoManager { get; private set; }
		// UDP Task management
		private CancellationTokenSource cancellationTokenSource;
		private Task nodeTask;
		// Lock for updating action in the main thread
        private object updateActionLock = new();
        private Action updateAction;
		public Action OnConnectionStart;
		public Action OnDisconnected;
		// ZMQ Sockets for communication, in this stage, we run them in the main thread
		// publisher socket for sending messages to other nodes
		public PublisherSocket _pubSocket;
		// response socket for service running in the local node
		public ResponseSocket _resSocket;
		public Dictionary<string, Func<byte[], byte[]>> serviceCallbacks { get; private set; }
		// subscriber socket for receiving messages from only master node
		private SubscriberSocket _subSocket;
		public Dictionary<string, Action<byte[]>> subscribeCallbacks { get; private set; }
		// Request socket for sending service request to only master node
		private RequestSocket _reqSocket;
		private List<NetMQSocket> _sockets;
		public Action ConnectionSpin;
		// Status flags
		private bool isRunning = false;
		private bool isConnected = false;
		// Constants
		private const int HEARTBEAT_INTERVAL = 500;
		// Rename Service
		private Service<string, string> renameService;

		private void Awake()
		{
			// Singleton pattern
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			DontDestroyOnLoad(gameObject);
			// Force to use .NET implementation of NetMQ
			AsyncIO.ForceDotNet.Force();
			// Initialize local node info
			localInfo = new NodeInfo
			{
				name = "UnityNode",
				nodeID = Guid.NewGuid().ToString(),
				addr = null,
				type = "UnityNode",
				servicePort = UnityPortSet.SERVICE,
				topicPort = UnityPortSet.TOPIC,
				serviceList = new List<string>(),
				topicList = new List<string>()
			};
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
			serviceCallbacks = new ();
			subscribeCallbacks = new ();
			cancellationTokenSource = new CancellationTokenSource();
			// Action setting
			OnConnectionStart += () => RunOnMainThread(() => StartConnection());
			OnDisconnected += () => RunOnMainThread(() => StopConnection());
			// Initialize the service callbacks
			renameService = new Service<string, string>("Rename", Rename, true);
		}

		private void Start()
		{
			// Start tasks
			isRunning = true;
			nodeTask = Task.Run(async () => await NodeTask(cancellationTokenSource.Token));
		}

		private void Update() {
			ConnectionSpin?.Invoke();
			lock (updateActionLock) {
				updateAction?.Invoke();
				updateAction = null;
			}
		}

        void RunOnMainThread(Action action)
        {
            lock (updateActionLock)
            {
                updateAction += action;
            }
        }

		private void OnDestroy()
		{
			isRunning = false;
			isConnected = false;
			if (cancellationTokenSource != null)
			{
				cancellationTokenSource.Cancel();
				cancellationTokenSource.Dispose();
			}
			nodeTask?.Wait();
			Debug.Log("Task has been stopped safely.");
		}

		private void OnApplicationQuit()
		{
			Debug.Log("On Application Quit");
			StopConnection();
			foreach (var sock in _sockets)
			{
				sock?.Dispose();
			}
			NetMQConfig.Cleanup();
		}

	public void StartConnection() {
		// if (isConnected) StopConnection();
		// subscription
		_subSocket.Connect($"tcp://{masterInfo.addr.ip}:{masterInfo.topicPort}");
		_subSocket.Subscribe("");
		ConnectionSpin += SubscriptionSpin;
		Debug.Log($"Start subscribing to {masterInfo.addr.ip}:{masterInfo.topicPort}");
		// local service
		_resSocket.Bind($"tcp://{localInfo.addr.ip}:{UnityPortSet.SERVICE}");
		ConnectionSpin += ServiceRespondSpin;
		Debug.Log($"Starting local service at {localInfo.addr.ip}:{UnityPortSet.SERVICE}");
		// request to master node
		_reqSocket.Connect($"tcp://{masterInfo.addr.ip}:{masterInfo.servicePort}");
		Debug.Log($"Starting connecting to server at {masterInfo.addr.ip}:{masterInfo.servicePort}");
		// local publish
		_pubSocket.Bind($"tcp://{localInfo.addr.ip}:{UnityPortSet.TOPIC}");
		Debug.Log($"Starting publish topic at {localInfo.addr.ip}:{UnityPortSet.TOPIC}");
		// CalculateTimestampOffset();
	}

	public void StopConnection() {
		while (_subSocket.HasIn) _subSocket.SkipFrame();
		ConnectionSpin = () => { };
		// It is not necessary to clear the topics callbacks
		// _topicsCallbacks.Clear();
		if (!isConnected) return;
		_resSocket.Unbind($"tcp://{localInfo.addr.ip}:{UnityPortSet.SERVICE}");
		_pubSocket.Unbind($"tcp://{localInfo.addr.ip}:{UnityPortSet.TOPIC}");
		_reqSocket.Disconnect($"tcp://{masterInfo.addr.ip}:{masterInfo.servicePort}");
		_subSocket.Disconnect($"tcp://{masterInfo.addr.ip}:{masterInfo.topicPort}");
	}

		public async Task NodeTask(CancellationToken token)
		{
			Debug.Log("Node task starts and listening for master node...");
			while (isRunning)
			{
				try
				{
					var nodeInfo = await SearchForMasterNode(token, 500);
					if (nodeInfo is not null)
					{
						masterInfo = nodeInfo;
						OnConnectionStart?.Invoke();
						isConnected = true;
						Debug.Log("Master node found and ready to send heartbeat.");
						await HeartbeatLoop(token, 200);
					}
					await Task.Delay(500, token);
				}
				catch (TaskCanceledException)
				{
					Debug.Log("Task was cancelled via exception");
				}
				catch (Exception e)
				{
					Debug.LogWarning($"Error in NodeTask: {e.StackTrace}");
					break;
				}
			}
			Debug.Log("Node task stopped.");
		}

		public async Task<NodeInfo> SearchForMasterNode(CancellationToken token, int timeout)
		{
			NodeInfo nodeInfo = null;
			Debug.Log("Searching for the master node...");
			try
			{
				while (isRunning)
				{
					// Now sure why we need to create a new udp client every time
					// If not it wouldn't receive the response when the master node started later
					UdpClient udpClient = NetworkUtils.CreateUDPClient(UnityPortSet.HEARTBEAT);
					udpClient.EnableBroadcast = true;
					// Debug.Log("Sending ping message...");
					byte[] pingMessage = EchoHeader.PING;
					// await send so we don't need to worry about broadcasting in the loop by mistake
					await udpClient.SendAsync(EchoHeader.PING, 1, new IPEndPoint(IPAddress.Broadcast, UnityPortSet.DISCOVERY));
					// waiting for receive of ping
					var receiveTask = udpClient.ReceiveAsync();
					if (await Task.WhenAny(receiveTask, Task.Delay(timeout, token)) == receiveTask)
					{
						var response = receiveTask.Result;
						byte[][] msgSeparated = MsgUtils.SplitByte(response.Buffer);
						if (msgSeparated[1] == null)
						{
							continue;
						}
						nodeInfo = MsgUtils.BytesDeserialize2Object<NodeInfo>(msgSeparated[0]);
						string localIP = NetworkUtils.GetLocalIPsInSameSubnet(nodeInfo.addr.ip);
						if (localIP == null)
						{
							continue;
						}
						localInfo.addr = new NodeAddress(localIP, UnityPortSet.HEARTBEAT);
						Debug.Log($"Found master node at {nodeInfo.addr.ip}:{nodeInfo.addr.port}");
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
			catch (TaskCanceledException)
			{
				Debug.Log("Task was cancelled via exception");
			}
			catch (Exception e)
			{
				Debug.LogWarning($"Error during master node discovery: {e.StackTrace}");
			}
			return nodeInfo;
		}

		public async Task HeartbeatLoop(CancellationToken token, int timeout)
		{
			UdpClient udpClient = NetworkUtils.CreateUDPClient(new IPEndPoint(IPAddress.Any, UnityPortSet.HEARTBEAT));
			IPEndPoint masterEndPoint = new IPEndPoint(IPAddress.Parse(masterInfo.addr.ip), masterInfo.addr.port);
			// Start the update info loop
			Debug.Log($"The Net Manager starts heartbeat at {localInfo.addr.ip}:{localInfo.addr.port}");
			while (isConnected)
			{
				try
				{
					byte[] heartbeatMessage = MsgUtils.GenerateHeartbeat(localInfo);
					await udpClient.SendAsync(heartbeatMessage, heartbeatMessage.Length, masterEndPoint);
					var receiveTask = udpClient.ReceiveAsync();
					// If didn't receive the heartbeat response within timeout
					if (await Task.WhenAny(receiveTask, Task.Delay(timeout, token)) != receiveTask)
					{
						Debug.LogWarning("Timeout: The master node is offline");
						throw new SocketException();
					}
					var result = await receiveTask;
					NodeInfo nodeInfo = MsgUtils.BytesDeserialize2Object<NodeInfo>(result.Buffer);
					if (nodeInfo.nodeID != masterInfo.nodeID)
					{
						Debug.Log("The master node has been changed, restarting a new connection");
						throw new SocketException();
					}
					// Debug.Log($"Sending heartbeat to {masterInfo.addr.ip}:{masterInfo.addr.port}");
					await Task.Delay(HEARTBEAT_INTERVAL);
				}
				catch (SocketException ex)
				{
					Debug.Log($"SocketException: {ex.Message}");
					OnDisconnected?.Invoke();
					isConnected = false;
					break;
				}
				catch (TaskCanceledException)
				{
					Debug.Log("Task was cancelled via exception");
					OnDisconnected?.Invoke();
					isConnected = false;
					break;
				}
				catch (Exception e)
				{
					Debug.LogWarning($"Failed to send heartbeat: {e.StackTrace}");
				}
			}
			udpClient.Close();
			Debug.Log("Heartbeat loop has been stopped, waiting for other master node.");
		}

		public void SubscriptionSpin()
		{
			// Only process the latest message of each topic
			Dictionary<string, byte[]> messageProcessed = new();
			while (_subSocket.HasIn)
			{
				byte[][] msgSeparated = MsgUtils.SplitByte(_subSocket.ReceiveFrameBytes());
				string topic_name = MsgUtils.Bytes2String(msgSeparated[0]);
				if (subscribeCallbacks.ContainsKey(topic_name))
				{
					// Debug.Log($"Received message from {topic_name}");
					messageProcessed[topic_name] = msgSeparated[1];
				}
			}
			foreach (var (topic_name, msg) in messageProcessed)
			{
				subscribeCallbacks[topic_name](msg);
			}
		}

		public void ServiceRespondSpin()
		{
			if (!_resSocket.HasIn) return;
			// TODO: make it as a byte array
			// TODO: make it running in the sub thread
			// now we need to carefully handle the service request
			// make sure that it would not block the main thread
			byte[] messageReceived = _resSocket.ReceiveFrameBytes();
			byte[][] messageSplit = MsgUtils.SplitByte(messageReceived);
			string serviceName = MsgUtils.Bytes2String(messageSplit[0]);
			if (serviceCallbacks.ContainsKey(serviceName))
			{
				byte[] response = serviceCallbacks[serviceName](messageSplit[1]);
				_resSocket.SendFrame(response);
			}
			else
			{
				Debug.LogWarning($"Service {serviceName} not found");
				_resSocket.SendFrame(IRXRSignal.NOSERVICE);
			}
		}

		// TODO: make it as a generic request type
		public byte[] CallBytesService(string service_name, string request)
		{
			_reqSocket.SendFrame($"{service_name}{MsgUtils.SEPARATOR}{request}");
			if (!_reqSocket.TryReceiveFrameBytes(TimeSpan.FromMilliseconds(10000), out byte[] bytes, out bool more)) {
				Debug.LogWarning($"Request Timeout");
				return new byte[] { };
			}
			List<byte> result = new List<byte>(bytes);
			result.AddRange(bytes);
			while (more) result.AddRange(_reqSocket.ReceiveFrameBytes(out more));
			return result.ToArray();
		}

		public ResponseType CallService<RequestType, ResponseType>(string serviceName, RequestType request)
		{
			byte[] requestBytes = MsgUtils.Serialize2Byte(request);
			byte[] responseBytes = CallBytesService(serviceName, Encoding.UTF8.GetString(requestBytes));
			if (typeof(ResponseType) == typeof(string))
			{
				string result = Encoding.UTF8.GetString(responseBytes);
				return (ResponseType)(object)result;
			}
			return MsgUtils.BytesDeserialize2Object<ResponseType>(responseBytes);
		}

		// TODO: make it as a generic request type
		public string Rename(string newName)
		{
			localInfo.name = newName;
			PlayerPrefs.SetString("HostName", localInfo.name);
			Debug.Log($"Change Host Name to {localInfo.name}");
			PlayerPrefs.Save();
			return IRXRSignal.SUCCESS;
		}
	}
}
