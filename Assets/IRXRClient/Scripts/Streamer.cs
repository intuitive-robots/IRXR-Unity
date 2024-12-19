using UnityEngine;

namespace IRXR.Node
{

	public class Streamer : MonoBehaviour
	{

		protected string _topic;
		protected Publisher<string> _publisher;

		void Start()
		{
			SetupTopic();
			_publisher = new Publisher<string>(_topic);
			Initialize();
		}
		protected virtual void SetupTopic() { }
		protected virtual void Initialize() { }
	}
}