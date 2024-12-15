using NetMQ;
using NetMQ.Sockets;
using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using IRXR.Utilities;
using UnityEditor.Experimental.GraphView;


namespace IRXR.Node
{
	public class Publisher<MsgType>
	{
		protected PublisherSocket _pubSocket;
		protected string _topic;

		public Publisher(string topic)
		{
			IRXRNetManager _netManager = IRXRNetManager.Instance;
			string hostName = _netManager.localInfo.name;
			_topic = $"{hostName}/{topic}";
			_pubSocket = _netManager._pubSocket;
			if (!_netManager.localInfo.topicList.Contains(_topic))
			{
				_netManager.localInfo.topicList.Add(_topic);
			}
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

		public Subscriber(string topic, Action<MsgType> receiveAction)
		{
            _topic = topic;
			_receiveAction = receiveAction;
		}

		public void StartSubscription()
		{
            IRXRNetManager _netManager = IRXRNetManager.Instance;
			if (_netManager.masterInfo.topicList.Contains(_topic))
			{
				Debug.Log($"Start subscribing to topic {_topic}");
				_netManager.subscribeCallbacks[_topic] = OnReceive;
			}
			else
			{
				Debug.LogWarning($"Topic {_topic} is not found in the master node");
			}
		}

		public void OnByteReceive(byte[] byteMessage)
		{
			_receiveAction((MsgType)(object)byteMessage);
		}

		public void OnReceive(byte[] byteMessage)
		{
			string jsonString = Encoding.UTF8.GetString(byteMessage);
			MsgType msg = JsonConvert.DeserializeObject<MsgType>(jsonString);
			_receiveAction(msg);
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
        private Func<RequestType, ResponseType> _onRequest;

        public Service(string serviceName, Func<RequestType, ResponseType> onRequest, bool globalNameSpace = false)
        {
            IRXRNetManager _netManager = IRXRNetManager.Instance;
            string hostName = _netManager.localInfo.name;
            if (globalNameSpace)
            {
                _serviceName = serviceName;
            }
            else
            {
                _serviceName = $"{hostName}/{serviceName}";
            }
			if (_netManager.localInfo.topicList.Contains(_serviceName))
			{
				throw new ArgumentException($"Service {_serviceName} is already registered");
			}
            _onRequest = onRequest;
			_netManager.localInfo.serviceList.Add(_serviceName);
			_netManager.serviceCallbacks[_serviceName] = BytesCallback;
            Debug.Log($"Service {_serviceName} is registered");
        }

		private byte[] BytesCallback(byte[] bytes)
		{
			RequestType req = MsgUtils.BytesDeserialize2Object<RequestType>(bytes);
			return MsgUtils.ObjectSerialize2Bytes(_onRequest(req));
		}

    }

}