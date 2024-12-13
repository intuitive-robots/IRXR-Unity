// using NetMQ;
// using NetMQ.Sockets;
// using System;
// using System.Text;
// using Newtonsoft.Json;


// namespace IRXRNode
// {
// 	public class Publisher
// 	{
// 		protected PublisherSocket _pubSocket;
// 		protected string _topic;

// 		public Publisher(string topic)
// 		{
// 			IRXRNetManager _netManager = IRXRNetManager.Instance;
// 			string hostName = _netManager.localInfo.name;
// 			_topic = $"{hostName}/{topic}";
// 			_pubSocket = _netManager.pubSocket;
// 			_netManager.RegisterLocalTopic(_topic);
// 		}

// 		public void Publish(object data)
// 		{
// 			string msg = JsonConvert.SerializeObject(data);
// 			msg = _topic + ":" + msg;
// 			_pubSocket.SendFrame(msg);
// 		}
// 	}

// 	public class Subscriber<MsgType>
// 	{
// 		protected string _topic;
// 		private SubscriberSocket _subSocket;
// 		private Action<MsgType> _receiveAction;

// 		public Subscriber(string topic, Action<MsgType> receiveAction)
// 		{
// 			_topic = topic;
// 			_receiveAction = receiveAction;
// 		}

// 		public void StartSubscription()
// 		{
// 			_subSocket = IRXRNetManager.Instance.CreateSubscriberSocket();
// 			// if (typeof(MsgType) == typeof(byte[]))
// 			// {
// 			// 	;
// 			// }
// 			// else
// 			// {
// 			// 	IRXRNetManager.Instance.SubscribeTopic(_topic, OnReceive);
// 			// }
// 		}

// 		public void OnByteReceive(byte[] byteMessage)
// 		{
// 			_receiveAction((MsgType)(object)byteMessage);
// 		}

// 		public void OnReceive(byte[] byteMessage)
// 		{
// 			string jsonString = Encoding.UTF8.GetString(byteMessage);
// 			MsgType msg = JsonConvert.DeserializeObject<MsgType>(jsonString);
// 			_receiveAction(msg);
// 		}

// 		public void Unsubscribe()
// 		{
// 			// IRXRNetManager.Instance;
// 		}

// 	}
// }