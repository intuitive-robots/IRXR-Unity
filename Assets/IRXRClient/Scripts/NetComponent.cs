using NetMQ;
using NetMQ.Sockets;
using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using IRXR.Utilities;
using UnityEditor.Networking.PlayerConnection;


namespace IRXR.Node
{

	public interface IPublisher
	{
		void Bind(string ip, int port);
	}

	public class Publisher<MsgType> : IPublisher
	{
		protected PublisherSocket _pubSocket;
		protected string _topic;

		public Publisher(string topic, bool globalNameSpace = false)
		{
			IRXRNetManager netManager = IRXRNetManager.Instance;
			_pubSocket = new PublisherSocket();
			if (globalNameSpace)
			{
				_topic = topic;
			}
			else
			{
				_topic = $"{netManager.localInfo.name}/{topic}";
			}
			_pubSocket = netManager._pubSocket;
			if (!netManager.localInfo.topics.ContainsKey(_topic))
			{
				Debug.LogWarning($"Publisher for topic {_topic} is already created");
			}
			Debug.Log($"Publisher for topic {_topic} is created");
		}

		public void Bind(string ip, int port = 0)
		{
			IRXRNetManager netManager = IRXRNetManager.Instance;
			_pubSocket.Bind($"tcp://{ip}:{port}");
			port = NetworkUtils.GetZMQSocketPort(_pubSocket);
			netManager.localInfo.topics[_topic] = new NetComponentInfo(
				_topic, "Publisher", netManager.localInfo.nodeID, new NetAddress(ip, port)
			);
		}


		public void Publish(string data)
		{
			// Combine topic and message
			string msg = MsgUtils.CombineHeaderWithMessage(_topic, data);
			// Send the message
			TryPublish(MsgUtils.String2Bytes(msg));
		}

		public void Publish(byte[] data)
		{
			TryPublish(MsgUtils.CombineHeaderWithMessage(_topic, data));
		}

		public void Publish(MsgType data)
		{
			string msg = MsgUtils.CombineHeaderWithMessage(_topic, JsonConvert.SerializeObject(data));
			TryPublish(MsgUtils.String2Bytes(msg));
		}

		private void TryPublish(byte[] msg)
		{
			try
			{
				_pubSocket.SendFrame(msg);
			}
			catch (TerminatingException ex)
			{
				Debug.LogWarning($"Publish failed: NetMQ context terminated. Error: {ex.Message}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Publish failed: Unexpected error occurred. Error: {ex.Message}");
			}
		}

	}

	public interface ISubscriber
	{
		void Connect(string ip, int port);
		void StartSubscription();
		void Unsubscribe();
	}

	public class Subscriber<MsgType> : ISubscriber
	{
		private string _topic;
		private SubscriberSocket _subSocket;
		private Action<MsgType> _receiveAction;
		private Func<byte[], MsgType> _onProcessMsg;

		public Subscriber(string topic, Action<MsgType> receiveAction)
		{
            _topic = topic;
			_subSocket = new SubscriberSocket();
			_receiveAction = receiveAction;
		}

		public void Connect(string ip, int port)
		{
			IRXRNetManager netManager = IRXRNetManager.Instance;
			_subSocket.Connect($"tcp://{ip}:{port}");
			_subSocket.Subscribe("");
			netManager.localInfo.topics[_topic] = new NetComponentInfo(
				_topic, "Subscriber", netManager.localInfo.nodeID, new NetAddress(ip, 0)
			);
		}

		public void StartSubscription()
		{
			IRXRNetManager netManager = IRXRNetManager.Instance;
			if (typeof(MsgType) == typeof(string))
			{
				_onProcessMsg = OnReceiveAsString;
			}
			else if (typeof(MsgType) == typeof(byte[]))
			{
				_onProcessMsg = OnReceiveAsBytes;
			}
			else
			{
				_onProcessMsg = OnReceiveAsJson;
			}
			netManager.subscribeCallbacks[_topic] = OnReceive;
			Debug.Log($"Subscribed to topic {_topic}");
		}

		public static MsgType OnReceiveAsString(byte[] message)
		{
			if (typeof(MsgType) != typeof(string))
			{
				throw new InvalidOperationException($"Type mismatch: Expected {typeof(MsgType)}, but got string.");
			}

			string result = Encoding.UTF8.GetString(message);
			return (MsgType)(object)result;
		}

		public static MsgType OnReceiveAsBytes(byte[] message)
		{
			if (typeof(MsgType) != typeof(byte[]))
			{
				throw new InvalidOperationException($"Type mismatch: Expected {typeof(MsgType)}, but got byte[].");
			}

			return (MsgType)(object)message;
		}

		public static MsgType OnReceiveAsJson(byte[] message)
		{
			string jsonString = Encoding.UTF8.GetString(message);
			return JsonConvert.DeserializeObject<MsgType>(jsonString);
		}

		public void OnReceive(byte[] byteMessage)
		{
			try
			{
				MsgType msg = _onProcessMsg(byteMessage);
				_receiveAction(msg);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Error processing message for topic {_topic}: {ex.Message}");
			}
		}


		public void Unsubscribe()
		{
            IRXRNetManager netManager = IRXRNetManager.Instance;
			if (netManager.masterInfo.topics.ContainsKey(_topic))
			{
				netManager.subscribeCallbacks.Remove(_topic);
				Debug.Log($"Unsubscribe from topic {_topic}");
			}
		}

	}


	public interface IService
	{
		void Bind(string ip, int port);
	}

    // Service class: Since it is running in the main thread, 
	// so we don't need to destroy it
	public class Service<RequestType, ResponseType>
	{
		private string _serviceName;
		private ResponseSocket _resSocket;
		private readonly Func<RequestType, ResponseType> _onRequest;
		private Func<byte[], RequestType> ProcessRequestFunc;
		private Func<ResponseType, byte[]> ProcessResponseFunc;

		public Service(string serviceName, Func<RequestType, ResponseType> onRequest, bool globalNameSpace = false)
		{
			IRXRNetManager netManager = IRXRNetManager.Instance;
			string hostName = netManager.localInfo.name;
			_serviceName = globalNameSpace ? serviceName : $"{hostName}/{serviceName}";
			if (netManager.localInfo.services.ContainsKey(_serviceName))
			{
				throw new ArgumentException($"Service {_serviceName} is already registered");
			}
			_resSocket = new ResponseSocket();
			netManager.serviceCallbacks[_serviceName] = BytesCallback;
			Debug.Log($"Service {_serviceName} is registered");
			_onRequest = onRequest ?? throw new ArgumentNullException(nameof(onRequest));
			// Initialize Request Processor
			if (typeof(RequestType) == typeof(string))
			{
				ProcessRequestFunc = bytes => (RequestType)(object)MsgUtils.Bytes2String(bytes);
			}
			else if (typeof(RequestType) == typeof(byte[]))
			{
				ProcessRequestFunc = bytes => (RequestType)(object)bytes;
			}
			else
			{
				ProcessRequestFunc = bytes => MsgUtils.BytesDeserialize2Object<RequestType>(bytes);
			}

			// Initialize Response Processor
			if (typeof(ResponseType) == typeof(string))
			{
				ProcessResponseFunc = response => MsgUtils.String2Bytes((string)(object)response);
			}
			else if (typeof(ResponseType) == typeof(byte[]))
			{
				ProcessResponseFunc = response => (byte[])(object)response;
			}
			else
			{
				ProcessResponseFunc = response => MsgUtils.ObjectSerialize2Bytes(response);
			}
		}

		public void Bind(string ip, int port = 0)
		{
			IRXRNetManager netManager = IRXRNetManager.Instance;
			_resSocket.Bind($"tcp://{ip}:{port}");
			port = NetworkUtils.GetZMQSocketPort(_resSocket);
			netManager.localInfo.services[_serviceName] = new NetComponentInfo(
				_serviceName, "Service", netManager.localInfo.nodeID, new NetAddress(ip, port)
			);
		}

		private byte[] BytesCallback(byte[] bytes)
		{
			try
			{
				RequestType request = ProcessRequestFunc(bytes);
				ResponseType response = _onRequest(request);
				return ProcessResponseFunc(response);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Error processing request for service {_serviceName}: {ex.Message}");
				return HandleErrorResponse(ex);
			}
		}

		private byte[] HandleErrorResponse(Exception ex)
		{
			string errorMessage = $"Error: {ex.Message}";
			if (typeof(ResponseType) == typeof(string))
			{
				return MsgUtils.ObjectSerialize2Bytes((ResponseType)(object)errorMessage);
			}
			Debug.LogWarning($"Unsupported error response type for {_serviceName}, returning default.");
			return new byte[0];
		}

		public void Unregister()
		{
			IRXRNetManager netManager = IRXRNetManager.Instance;

			if (netManager.localInfo.services.ContainsKey(_serviceName))
			{
				netManager.localInfo.services.Remove(_serviceName);
				netManager.serviceCallbacks.Remove(_serviceName);
				Debug.Log($"Service {_serviceName} is unregistered");
			}
			else
			{
				Debug.LogWarning($"Service {_serviceName} is not registered");
			}
		}

		public static string BytesToString(byte[] bytes)
		{
			return Encoding.UTF8.GetString(bytes);
		}

		public static byte[] StringToBytes(string str)
		{
			return Encoding.UTF8.GetBytes(str);
		}

		public static RequestType BytesToRequest(byte[] bytes)
		{
			return MsgUtils.BytesDeserialize2Object<RequestType>(bytes);
		}

		public static byte[] RequestToBytes(RequestType request)
		{
			return MsgUtils.ObjectSerialize2Bytes(request);
		}

		public static ResponseType BytesToResponse(byte[] bytes)
		{
			return MsgUtils.BytesDeserialize2Object<ResponseType>(bytes);
		}

		public static byte[] ResponseToBytes(ResponseType response)
		{
			return MsgUtils.ObjectSerialize2Bytes(response);
		}
	}


}