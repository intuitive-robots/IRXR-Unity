// using Anaglyph.XRTemplate.DepthKit;
// using System.Collections.Generic;
// using UnityEngine;
// using Anaglyph.BarCodes;
// using Anaglyph.DisplayCapture;
// using Newtonsoft.Json;

// public class MQ3QRSceneAlignment : QRSceneAlignment
// {
// 	[SerializeField] private BarCodeReader barCodeReader;

// 	[SerializeField] private float horizontalFieldOfViewDegrees = 82f;
// 	public float Fov => horizontalFieldOfViewDegrees;
// 	private Matrix4x4 displayCaptureProjection;
// 	private void Awake()
// 	{
// 		Vector2Int size = DisplayCaptureManager.Instance.Size;
// 		float aspect = size.x / (float)size.y;
// 		displayCaptureProjection = Matrix4x4.Perspective(Fov, aspect, 1, 100f);
// 	}

// 	private void Start() {
// 		IRXRNetManager.Instance.RegisterServiceCallback("AlignQR", StartQRTracingCallback);
// 	}

// 	private void OnDestroy()
// 	{
// 		if(barCodeReader != null)
// 			barCodeReader.OnReadBarCodes -= OnReadBarCodes;
// 	}

// 	public string StartQRTracingCallback(string jsonData) {
// 		Debug.Log("called qr-callback");
// 		QRSceneAlignmentData _data = JsonConvert.DeserializeObject<QRSceneAlignmentData>(jsonData);
// 		StartQRTracing(_data);
// 		return "Started QR-Tracing";
// 	}

// 	override public void StartQRTracing(QRSceneAlignmentData data)
// 	{
// 		base.StartQRTracing(data);
// 		if(barCodeReader != null)
// 			barCodeReader.OnReadBarCodes += OnReadBarCodes;
// 	}

// 	override public void StopQRTracing()
// 	{
// 		base.StopQRTracing();
// 		if(barCodeReader != null)
// 			barCodeReader.OnReadBarCodes -= OnReadBarCodes;
// 	}

// 	private void OnReadBarCodes(IEnumerable<BarCodeReader.Result> barcodeResults)
// 	{
// 		foreach (BarCodeReader.Result barcodeResult in barcodeResults)
// 		{
// 			if (_data == null) break;
// 			// continue if the barcode is not the one we are looking for
// 			if (_data.qrText is not null && barcodeResult.text != _data.qrText) continue;
// 			// get the head pose at the time the barcode was read
// 			float timestampInSeconds = barcodeResult.timestamp * 0.000000001f;
// 			OVRPlugin.PoseStatef headPoseState = OVRPlugin.GetNodePoseStateAtTime(timestampInSeconds, OVRPlugin.Node.Head);
// 			OVRPose headPose = headPoseState.Pose.ToOVRPose();
// 			Matrix4x4 headTransform = Matrix4x4.TRS(headPose.position, headPose.orientation, Vector3.one);
// 			// get the points of the barcode corners in the world frame
// 			Vector3[] worldPoints = new Vector3[4];
// 			for (int i = 0; i < 4; i++)
// 			{
// 				BarCodeReader.Point pixel = barcodeResult.points[i];
// 				Vector2Int size = DisplayCaptureManager.Instance.Size;
// 				Vector2 uv = new Vector2(pixel.x / size.x, 1f - pixel.y / size.y);
// 				Vector3 worldPos = ProjectionToWorld(displayCaptureProjection, uv);
// 				worldPos.z = -worldPos.z;
// 				worldPos = headTransform.MultiplyPoint(worldPos);
// 				worldPoints[i] = worldPos;
// 			}
// 			DepthToWorld.SampleWorld(worldPoints, out Vector3[] corners);
// 			Vector3 up = (corners[1] - corners[0]).normalized;
// 			Vector3 right = (corners[2] - corners[1]).normalized;
// 			Vector3 forward = -Vector3.Cross(up, right).normalized;
// 			// the top left corner as the center
// 			Vector3 center = corners[1];
// 			SetSceneOrigin(new Pose(center, Quaternion.LookRotation(forward, up)));
// 		}
// 	}

// 	private static Vector3 ProjectionToWorld(Matrix4x4 projection, Vector2 uv)
// 	{
// 		Vector2 v = 2f * uv - Vector2.one;
// 		var p = new Vector4(v.x, v.y, 0.1f, 1f);
// 		p = projection.inverse * p;
// 		return new Vector3(p.x, p.y, p.z) / p.w;
// 	}
// }
