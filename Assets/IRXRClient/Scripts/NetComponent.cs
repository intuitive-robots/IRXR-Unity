using NetMQ;
using NetMQ.Sockets;
using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using IRXR.Utilities;


namespace IRXR.Node
{
	public class Publisher<MsgType>
	{
		protected PublisherSocket _pubSocket;
		protected string _topic;

		public Publisher(string topic, bool globalNameSpace = true)
		{
			IRXRNetManager _netManager = IRXRNetManager.Instance;
			if (globalNameSpace)
			{
				_topic = topic;
			}
			else
			{
				_topic = $"{_netManager.localInfo.name}/{topic}";
			}
			_pubSocket = _netManager._pubSocket;
			if (!_netManager.localInfo.topicList.Contains(_topic))
			{
				_netManager.localInfo.topicList.Add(_topic);
			}
			Debug.Log($"Publisher for topic {_topic} is created");
		}

		public void Publish(string data)
		{
			string msg = MsgUtils.CombineHeaderWithMessage(_topic, data);
			_pubSocket.SendFrame(MsgUtils.String2Bytes(msg));
		}

		public void Publish(byte[] data)
		{
			_pubSocket.SendFrame(MsgUtils.CombineHeaderWithMessage(_topic, data));
		}

		public void Publish(MsgType data)
		{
			string msg = MsgUtils.CombineHeaderWithMessage(_topic, JsonConvert.SerializeObject(data));
			_pubSocket.SendFrame(MsgUtils.String2Bytes(msg));
		}
	}

	public class Subscriber<MsgType>
	{
		protected string _topic;
		private Action<MsgType> _receiveAction;
		private Func<byte[], MsgType> _onProcessMsg;

		public Subscriber(string topic, Action<MsgType> receiveAction)
		{
            _topic = topic;
			_receiveAction = receiveAction;
		}

		public void StartSubscription()
		{
			IRXRNetManager _netManager = IRXRNetManager.Instance;

			if (!_netManager.masterInfo.topicList.Contains(_topic))
			{
				Debug.LogWarning($"Topic {_topic} is not found in the master node");
				return;
			}

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
			// else if (typeof(MsgType).IsSerializable)
			// {
			// 	_onProcessMsg = OnReceiveAsJson;
			// }
			// else
			// {
			// 	throw new NotSupportedException($"Type {typeof(MsgType)} is not supported for subscription.");
			// }
			_netManager.subscribeCallbacks[_topic] = OnReceive;
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
				Debug.LogError($"Error processing message for topic {_topic}: {ex.Message}");
			}
		}


		public void Unsubscribe()
		{
            IRXRNetManager _netManager = IRXRNetManager.Instance;
			if (_netManager.masterInfo.topicList.Contains(_topic))
			{
				_netManager.subscribeCallbacks.Remove(_topic);
				Debug.Log($"Unsubscribe from topic {_topic}");
			}
		}

	}

    // Service class: Since it is running in the main thread, 
	// so we don't need to destroy it
	public class Service<RequestType, ResponseType>
	{
		protected string _serviceName;
		private readonly Func<RequestType, ResponseType> _onRequest;
		private Func<byte[], RequestType> ProcessRequestFunc;
		private Func<ResponseType, byte[]> ProcessResponseFunc;

		public Service(string serviceName, Func<RequestType, ResponseType> onRequest, bool globalNameSpace = false)
		{
			IRXRNetManager netManager = IRXRNetManager.Instance;
			string hostName = netManager.localInfo.name;
			_serviceName = globalNameSpace ? serviceName : $"{hostName}/{serviceName}";
			if (netManager.localInfo.serviceList.Contains(_serviceName))
			{
				throw new ArgumentException($"Service {_serviceName} is already registered");
			}
			netManager.localInfo.serviceList.Add(_serviceName);
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
			// else if (typeof(RequestType).IsSerializable)
			// {
			// 	ProcessRequestFunc = bytes => MsgUtils.BytesDeserialize2Object<RequestType>(bytes);
			// }
			// else
			// {
			// 	throw new NotSupportedException($"Type {typeof(RequestType)} is not supported for service request.");
			// }

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
			// else if (typeof(ResponseType).IsSerializable)
			// {
			// 	ProcessResponseFunc = response => MsgUtils.ObjectSerialize2Bytes(response);
			// }
			// else
			// {
			// 	throw new NotSupportedException($"Type {typeof(ResponseType)} is not supported for service response.");
			// }
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
				Debug.LogError($"Error processing request for service {_serviceName}: {ex.Message}");
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

			if (netManager.localInfo.serviceList.Contains(_serviceName))
			{
				netManager.localInfo.serviceList.Remove(_serviceName);
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