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

		public Publisher(string topic)
		{
			IRXRNetManager _netManager = IRXRNetManager.Instance;
			string hostName = _netManager.localInfo.name;
			_topic = $"{hostName}/{topic}";
			_pubSocket = _netManager._pubSocket;
			if (_netManager.nodeInfoManager.CheckTopic(_topic) == null)
			{
				_netManager.localInfo.topicList.Add(_topic);
			}
		}

		public void Publish(MsgType data)
		{
			string msg = MsgUtils.CombineHeaderWithMessage(_topic, JsonConvert.SerializeObject(data));
			_pubSocket.SendFrame(Encoding.UTF8.GetBytes(msg));
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
			if (_netManager.nodeInfoManager.CheckTopic(_topic) != null)
			{
				_netManager.subscribeCallbacks[_topic] = OnReceive;
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
			if (_netManager.nodeInfoManager.CheckTopic(_topic) != null)
			{
				_netManager.subscribeCallbacks.Remove(_topic);
			}
		}

	}

    // Service class: Since it is running in the main thread, so we don't need to have a deconstructor
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
            _onRequest = onRequest;
			if (_netManager.nodeInfoManager.CheckService(_serviceName) == null)
			{
				_netManager.localInfo.serviceList.Add(_serviceName);
				_netManager.serviceCallbacks[serviceName] = ServiceCallback;
			}
            Debug.Log($"Service {_serviceName} is registered");
        }

        public string ServiceCallback(string request)
        {
            RequestType req = JsonConvert.DeserializeObject<RequestType>(request);
            ResponseType response = _onRequest(req);
            return MsgUtils.CombineHeaderWithMessage(_serviceName, JsonConvert.SerializeObject(response));
        }

    }

}