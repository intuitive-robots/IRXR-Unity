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

using UnityEditor.UI;
using UnityEditor.Experimental.GraphView;

namespace IRXRNode
{


	public static class UnityPortSet
	{
		public static readonly int DISCOVERY = 7720;
		public static readonly int HEARTBEAT = 7729;
		public static readonly int SERVICE = 7730;
		public static readonly int TOPIC = 7731;
	}


	[Serializable]
	public class Address
	{
		public string ip;
		public int port;
		public Address(string ip, int port)
		{
			this.ip = ip;
			this.port = port;
		}
	}


	public class NodeInfo
	{
		public string name;
		public string nodeID;
		public Address addr;
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
			return System.Text.Encoding.UTF8.GetBytes(json);
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

		public Address CheckService(string serviceName)
		{
			foreach (var info in _nodesInfo.Values)
			{
				if (info.serviceList.Contains(serviceName))
				{
					return new Address(info.addr.ip, info.servicePort);
				}
			}
			return null;
		}

		public Address CheckTopic(string topicName)
		{
			foreach (var info in _nodesInfo.Values)
			{
				if (info.topicList.Contains(topicName))
				{
					return new Address(info.addr.ip, info.topicPort);
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
		private NodeInfoManager _nodeInfoManager;

		// ZMQ Sockets
		public PublisherSocket pubSocket;
		public ResponseSocket serviceSocket;
		private List<NetMQSocket> _sockets;
		// Task management
		private CancellationTokenSource cancellationTokenSource;
		private Task nodeTask;
		private Task serviceTask;
		private Task updateInfoTask;

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
				addr = new Address(null, UnityPortSet.HEARTBEAT),
				type = "UnityNode",
				servicePort = UnityPortSet.SERVICE,
				topicPort = UnityPortSet.TOPIC,
				serviceList = new List<string>(),
				topicList = new List<string>()
			};
			_nodeInfoManager = new NodeInfoManager(localInfo);
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
			pubSocket = new PublisherSocket();
			serviceSocket = new ResponseSocket();
			_sockets = new List<NetMQSocket>() { pubSocket, serviceSocket };
			// serviceSocket.Bind("tcp://*:5556");
			cancellationTokenSource = new CancellationTokenSource();
		}

		private void Start()
		{
			// Start tasks
			isRunning = true;
			nodeTask = Task.Run(async () => await NodeTask(), cancellationTokenSource.Token);
			// serviceTask = Task.Run(() => ServiceLoop(cancellationTokenSource.Token));
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
				updateInfoTask?.Wait();
				cancellationTokenSource.Dispose();
				Debug.Log("Task has been stopped safely.");
			}
		}

		private void OnDestroy() {
			StopTask();
		}


		private void OnApplicationQuit()
		{
			Debug.Log("Net Manager is being destroyed.");
			foreach (var sock in _sockets)
			{
				// sock.Close();
				sock.Dispose();
			}
			NetMQConfig.Cleanup();
		}


		public async Task NodeTask()
		{
			Debug.Log("Node task started");
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

		private async Task ServiceLoop(CancellationToken token)
		{
			Debug.Log("Service loop started.");

			while (isRunning)
			{
				try
				{
					// Wait for and process incoming service requests
					if (serviceSocket.TryReceiveFrameString(out string request))
					{
						Debug.Log($"Received service request: {request}");
						// string response = ProcessServiceRequest(request);
						// serviceSocket.SendFrame(response);
						// Debug.Log($"Sent response: {response}");
					}
					await Task.Delay(500);
				}
				catch (Exception ex)
				{
					Debug.LogError($"Error in ServiceLoop: {ex.Message}");
					break;
				}
			}
			Debug.Log("Service loop stopped.");
		}

		public async Task<Address> SearchForMasterNode(int timeout = 500)
		{
			Address masterAddress = null;
			Debug.Log("Searching for the master node...");
			try
			{
				while (isRunning)
				{
					UdpClient udpClient = new UdpClient();
					udpClient.EnableBroadcast = true;
					udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, UnityPortSet.HEARTBEAT));
					Debug.Log("Sending ping message..." + udpClient.Available);
					byte[] pingMessage = EchoHeader.PING;
					// await send so we don't need to worry about broadcasting in the loop by mistake
					await udpClient.SendAsync(pingMessage, pingMessage.Length, new IPEndPoint(IPAddress.Broadcast, UnityPortSet.DISCOVERY));
					// udpClient.Send(pingMessage, pingMessage.Length, new IPEndPoint(IPAddress.Broadcast, UnityPortSet.DISCOVERY));
					// waiting for receive of ping
					var receiveTask = udpClient.ReceiveAsync();
					if (await Task.WhenAny(receiveTask, Task.Delay(timeout)) == receiveTask)
					{
						Debug.Log("Received ping response.");
						var response = receiveTask.Result;
						byte[][] responseMessage = MsgUtils.SplitByte(response.Buffer);
						masterAddress = MsgUtils.Deserialize2Object<Address>(responseMessage[0]);
						Debug.Log($"Found master node at {masterAddress.ip}:{masterAddress.port}");
						// pubSocket.Bind($"tcp://{masterAddress.ip}:{UnityPortSet.TOPIC}");
						isConnected = true;
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
				Debug.LogError($"Error during master node discovery: {e.Message}");
			}
			return masterAddress;
		}

		public async Task HeartbeatLoop(IPEndPoint masterAddress)
		{
			UdpClient udpClient = new UdpClient();
			// udpClient.EnableBroadcast = true;
			udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, UnityPortSet.HEARTBEAT));
			try
			{
				// Start the update info loop
				updateInfoTask = Task.Run(async () => await UpdateInfoLoop(udpClient, 3 * HEARTBEAT_INTERVAL), cancellationTokenSource.Token);
			}
			catch (Exception e)
			{
				Debug.LogError($"Failed to create UDP socket: {e}");
				throw new Exception("Failed to create UDP socket");
			}
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
				catch (Exception e)
				{
					Debug.LogError($"Failed to send heartbeat: {e}");
				}
			}
			updateInfoTask?.Wait();
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
						_nodeInfoManager.UpdateNodesInfo(nodesInfo, this);
					}
					else
					{
						Debug.LogWarning("Timeout: The master node is offline");
						isConnected = false;
					}
				}
				catch (Exception e)
				{
					Debug.LogError($"Error occurred in update info loop: {e}");
				}
			}
		}

		public void RegisterLocalService(string serviceName)
		{
			if (_nodeInfoManager.CheckService(serviceName) == null)
			{
				localInfo.serviceList.Add(serviceName);
			}
		}

		public void RegisterLocalTopic(string topicName)
		{
			if (_nodeInfoManager.CheckTopic(topicName) == null)
			{
				localInfo.topicList.Add(topicName);
			}
		}

		public void RemoveLocalService(string serviceName)
		{
			if (localInfo.serviceList.Contains(serviceName))
			{
				localInfo.serviceList.Remove(serviceName);
			}
		}

		public void RemoveLocalTopic(string topicName)
		{
			if (localInfo.topicList.Contains(topicName))
			{
				localInfo.topicList.Remove(topicName);
			}
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
