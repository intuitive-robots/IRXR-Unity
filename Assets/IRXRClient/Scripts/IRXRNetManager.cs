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
using Unity.VisualScripting;

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
		private List<IPublisher> _publishers;
		private List<ISubscriber> _subscribers;
		private List<IService> _services;
		
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
			localInfo = new NodeInfo()
			{
				name = "UnityNode",
				nodeID = Guid.NewGuid().ToString(),
				addr = new NetAddress("127.0.0.1", 0),
				type = "UnityNode",
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
			_reqSocket = new RequestSocket();
			
			_sockets = new List<NetMQSocket>() { _pubSocket, _resSocket, _subSocket, _reqSocket };
			serviceCallbacks = new();
			subscribeCallbacks = new();
			cancellationTokenSource = new CancellationTokenSource();
			// Action setting
			// OnConnectionStart += () => RunOnMainThread(() => StartConnection());
			OnConnectionStart += StartConnection;
			// OnDisconnected += () => RunOnMainThread(() => StopConnection());
			OnDisconnected += StopConnection;
			// Initialize the service callbacks
			renameService = new Service<string, string>("Rename", Rename, true);
		}

		private void Start()
		{
			// Start tasks
			Debug.Log("Starting node task...");
			isRunning = true;
			nodeTask = Task.Run(async () => await NodeTask(cancellationTokenSource.Token));
		}

		private void Update()
		{
			if (Monitor.TryEnter(updateActionLock))
			{
				try
				{
					ConnectionSpin?.Invoke();
				}
				finally
				{
					Monitor.Exit(updateActionLock);
				}
			}
		}

		private void OnApplicationQuit() {
			if (isConnected)
			{
				Debug.Log("Application is quitting, stop connection");
				CallService<string, string>("NodeOffline", localInfo.nodeID);
			};
		}

		private void OnDestroy()
		{
			isConnected = false;
			isRunning = false;
			if (cancellationTokenSource != null)
			{
				cancellationTokenSource.Cancel();
				cancellationTokenSource.Dispose();
			}
			nodeTask?.Wait();
			StopConnection();
			foreach (var sock in _sockets)
			{
				sock?.Dispose();
			}
			NetMQConfig.Cleanup();
			Debug.Log("IRXR has been stopped safely.");
		}

		public void StartConnection()
		{
			if (isConnected) StopConnection();
			isConnected = true;
			lock (updateActionLock)
			{
				// subscription
				foreach (var subscriber in _subscribers)
				{
					subscriber.Connect();
				}
				_subSocket.Connect($"tcp://{masterInfo.addr.ip}:{masterInfo.topicPort}");
				_subSocket.Subscribe("");
				Debug.Log($"Start subscribing to {masterInfo.addr.ip}:{masterInfo.topicPort}");
				// local service
				_resSocket.Bind($"tcp://{localInfo.addr.ip}:{UnityPortSet.SERVICE}");
				ConnectionSpin += SubscriptionSpin;
				ConnectionSpin += ServiceRespondSpin;
				Debug.Log($"Starting local service at {localInfo.addr.ip}:{UnityPortSet.SERVICE}");
				// request to master node
				_reqSocket.Connect($"tcp://{masterInfo.addr.ip}:{masterInfo.servicePort}");
				Debug.Log($"Starting connecting to server at {masterInfo.addr.ip}:{masterInfo.servicePort}");
				// local publish
				_pubSocket.Bind($"tcp://{localInfo.addr.ip}:{UnityPortSet.TOPIC}");
				Debug.Log($"Starting publish topic at {localInfo.addr.ip}:{UnityPortSet.TOPIC}");
				// CalculateTimestampOffset();
				CallService<NodeInfo, string>("RegisterNode", localInfo);
			}
		}

		public void StopConnection()
		{
			lock (updateActionLock)
			{
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
			Debug.Log("Stop connection");
			isConnected = false;
		}

		public async Task NodeTask(CancellationToken token)
		{
			Debug.Log("Node task starts and is searching for master node...");
			UdpClient udpClient = NetworkUtils.CreateUDPClient(UnityPortSet.DISCOVERY);
			IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
			while (isRunning)
			{
				try
				{
					if (udpClient.Available == 0) continue;
					byte[] result = udpClient.Receive(ref endPoint);
					string message = Encoding.UTF8.GetString(result);
					if (!message.StartsWith("SimPub")) continue;
					string[] split = message.Split(MsgUtils.SEPARATOR, 2);
					NodeInfo info = MsgUtils.StringDeserialize2Object<NodeInfo>(split[1]);
					if (masterInfo == null || masterInfo.nodeID != info.nodeID)
					{
						if (isConnected) OnDisconnected?.Invoke();
						masterInfo = info;
						localInfo.addr.ip = NetworkUtils.GetLocalIPsInSameSubnet(masterInfo.addr.ip);
						Debug.Log($"Discovered server at {masterInfo.addr.ip} with local IP {localInfo.addr.ip}");
						OnConnectionStart?.Invoke();
					}
					await Task.Delay(50, token);
				}
				catch (TaskCanceledException)
				{
					Debug.Log("Task is canceled by user");
					break;
				}
				catch (Exception e)
				{
					Debug.LogWarning(e.StackTrace);
				}
			}
			udpClient.Close();
			Debug.Log("Node task ends");
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
			if (!_reqSocket.TryReceiveFrameBytes(TimeSpan.FromMilliseconds(10000), out byte[] bytes, out bool more))
			{
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
			byte[] requestBytes;
			if (typeof(RequestType) == typeof(string))
			{
				requestBytes = MsgUtils.String2Bytes((string)(object)request);
			}
			else
			{
				requestBytes = MsgUtils.Serialize2Byte(request);
			}
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
