using System;
using System.Collections.Generic;
using UnityEngine;

namespace Anaglyph.BarCodes
{
	public class BarCodeReader : MonoBehaviour
	{
		[Serializable]
		private struct Results
		{
			public Result[] results;
		}

		[Serializable]
		public struct Result
		{
			public string text;
			public Point[] points;
			public long timestamp;
		}

		[Serializable]
		public struct Point
		{
			public float x, y;
		}

		private class AndroidInterface
		{
			private AndroidJavaClass androidClass;
			private AndroidJavaObject androidInstance;

			public AndroidInterface(GameObject messageReceiver)
			{
				androidClass = new AndroidJavaClass("com.trev3d.DisplayCapture.BarCodeReader");
				androidInstance = androidClass.CallStatic<AndroidJavaObject>("getInstance");
				androidInstance.Call("setup", messageReceiver.name);
			}

			public void SetEnabled(bool enabled) => androidInstance.Call("setEnabled", enabled);
		}

		public event Action<IEnumerable<Result>> OnReadBarCodes = delegate { };

		private AndroidInterface androidInterface;

		private void Awake()
		{
			androidInterface = new AndroidInterface(gameObject);
		}

		private void OnEnable()
		{
			androidInterface.SetEnabled(true);
		}

		private void OnDisable()
		{
			androidInterface.SetEnabled(false);
		}

		private void OnDestroy()
		{
			OnReadBarCodes = delegate { };
		}

		// Called by Android 

#pragma warning disable IDE0051 // Remove unused private members
		private void OnBarCodeResults(string json)
		{
			Results results = JsonUtility.FromJson<Results>(json);
			OnReadBarCodes.Invoke(results.results);
		}
#pragma warning restore IDE0051 // Remove unused private members
	}
}