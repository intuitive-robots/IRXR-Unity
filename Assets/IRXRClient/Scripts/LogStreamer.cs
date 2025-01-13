using UnityEngine;

namespace IRXR.Node
{

    public class LogStreamer : MonoBehaviour
    {

		protected string _topic;
		protected Publisher<string> _publisher;

		void Start()
		{
			_publisher = new Publisher<string>("Log", false);
            Application.logMessageReceived += HandleLog;
            timer = Time.realtimeSinceStartup;
		}

        private int frameCounter = 0;
        private float timer = 0;

        void HandleLog(string logString, string stackTrace, LogType type)
        {
            _publisher.Publish(logString);
        }

        private void OnApplicationQuit() {
            Application.logMessageReceived -= HandleLog;
        }

        void Update()
        {
            frameCounter += 1;
            float totalTime = Time.realtimeSinceStartup - timer;
            if (totalTime > 5.0f)
            {
                float fps = frameCounter / totalTime;
                HandleLog("Average FPS in the last 5s: " + fps, null, LogType.Log);
                timer = Time.realtimeSinceStartup;
                frameCounter = 0;
            }
        }

    }
}