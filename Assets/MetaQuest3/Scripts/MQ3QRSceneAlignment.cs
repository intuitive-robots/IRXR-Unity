using UnityEngine;
using IRXR.Node;
using System;
using ZXing;
using ZXing.QrCode;
using ZXing.Common;
using Meta.XR;
using NUnit.Framework;

public class MQ3QRSceneAlignment : QRSceneAlignment
{
	private static string SPATIAL_PERMISSION = "com.oculus.permission.USE_SCENE";

	[SerializeField] private bool startTrackingOnStart = true;
	[SerializeField] private TRACKING_STYLE trackingStyle = TRACKING_STYLE.QR;

	[Header("QR-specific fields")]
	[SerializeField] private bool calculateForwardDirFromQR = true;

	[Tooltip(@"Recommend using POINTCLOUD or WORLD for most cases.
	 - POINTCLOUD uses the normals of the point cloud to determine the up direction.
	 - QR uses the QR code to determine the up direction.
	 - WORLD uses the world up direction (0, 1, 0).")]
	[SerializeField] private UPDirection upDirection = UPDirection.POINTCLOUD;

	private WebCamTextureManager webCamTextureManager;
	private WebCamTexture texture;
	private EnvironmentRaycastManager raycastManager;
	
	private bool isTracking = false;
	private bool hasPermission = false;

    private void Start()
	{
		if (startTrackingOnStart)
		{
			StartQRTracking(new QRSceneAlignmentData());
		}
	}

    private void Update()
	{
		if (isTracking)
		{
			switch (trackingStyle)
			{
				case TRACKING_STYLE.MOTION_CONTROLLER:
					OnTrackingMotionController();
					break;
				case TRACKING_STYLE.QR:
					OnTrackingQR();
					break;
				default:
					break;
			}
		}
	}

	public override void StartQRTracking(QRSceneAlignmentData data)
	{
		isTracking = true;
		if (trackingStyle == TRACKING_STYLE.QR) StartQRTracking();
		ApplyOffset();
	}


	public override void StopQRTracking()
	{
		isTracking = false;

		// TODO: Seperate the logic for stopping tracking based on the style
		if (trackingStyle == TRACKING_STYLE.QR && webCamTextureManager != null)
		{
			webCamTextureManager.StopRecording();
		}
	}


	private void OnTrackingMotionController()
	{
		var activeController = OVRInput.GetActiveController();
		if (activeController != OVRInput.Controller.Touch) return;
		Vector3 leftPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
		Vector3 rightPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
		transform.position = (leftPos + rightPos) / 2;
		Quaternion leftRot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch);
		Quaternion rightRot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);
		Vector3 euler = Quaternion.Slerp(leftRot, rightRot, 0.5f).eulerAngles;
		transform.rotation = Quaternion.Euler(0, euler.y, 0);
	}

	private void StartQRTracking()
	{
		if (!EnvironmentRaycastManager.IsSupported)
		{
			Debug.LogError("QR: EnvironmentRaycastManager is not supported: please read the official documentation to get more details. (https://developers.meta.com/horizon/documentation/unity/unity-depthapi-overview/)");
			return;
		}

		if (raycastManager == null)
		{
			raycastManager = FindAnyObjectByType<EnvironmentRaycastManager>();
			if (raycastManager == null)
			{
				Debug.LogError("QR: EnvironmentRaycastManager not found in the scene: please read the official documentation to get more details. (https://developers.meta.com/horizon/documentation/unity/unity-depthapi-overview/)");
				return;
			}
		}

		if (webCamTextureManager == null)
		{
			webCamTextureManager = FindAnyObjectByType<WebCamTextureManager>();
			if (webCamTextureManager == null)
			{
				Debug.LogError("WebCamTextureManager not found in the scene: please read the official documentation to get more details. (https://developers.meta.com/horizon/documentation/unity/unity-pca-documentation/)");
				return;
			}
		}
		webCamTextureManager.StartRecording();
	}


	private void OnTrackingQR()
	{
		if (webCamTextureManager.WebCamTexture == null) return;
		if (!hasPermission) 
		{
			hasPermission = HasScenePermission();
			if (!hasPermission)
			{
				Debug.LogError("QR: Camera permission not granted. Please request permission before starting QR tracking.");
				return;
			}
		}

		texture = webCamTextureManager.WebCamTexture;
		LuminanceSource luminanceSource = getLuminanceSource(texture);
		Result result = DecodeQr(luminanceSource);


		if (result != null)
		{
			Debug.Log($"QR Code detected: {result.Text}");

			Vector3[] positions = new Vector3[3];
			Vector3[] normals = new Vector3[3];
			GetPosesOfCorners(result, positions, normals);

			SetPoseOfGO(positions, normals);
		}
		else
		{
			Debug.Log("No QR Code detected");
		}
	}


	private void GetPosesOfCorners(Result result, Vector3[] positions, Vector3[] normals)
	{
		// the order is: bottom-left, top-left, top-right
		for (int i = 0; i < 3; i++)
		{
			var point = result.ResultPoints[i];
			var ray = PassthroughCameraUtils.ScreenPointToRayInWorld(webCamTextureManager.Eye, new Vector2Int((int)point.X, (int)point.Y));
			(Vector3? position, Vector3? normal) = PlaceGameObjectByScreenPosAndRot(ray);
			UnityEngine.Assertions.Assert.IsTrue(position != null, $"Position is null for point {i}");
			UnityEngine.Assertions.Assert.IsTrue(normal != null, $"Normal is null for point {i}");
			positions[i] = position.Value;
			normals[i] = normal.Value;
		}
	}


	private LuminanceSource getLuminanceSource(WebCamTexture texture)
	{
		var pixels = texture.GetPixels32();
		// way 1 custom luminance source
		// return new Color32LuminanceSource(pixels, texture.width, texture.height);

		// way 2 ZXing luminance source
		byte[] rawBytes = new byte[pixels.Length * 3];   // for RGB24
		for (int i = 0; i < pixels.Length; i++)
		{
			rawBytes[i * 3 + 0] = pixels[i].r;
			rawBytes[i * 3 + 1] = pixels[i].g;
			rawBytes[i * 3 + 2] = pixels[i].b;
		}
		return new RGBLuminanceSource(rawBytes, texture.width, texture.height);
	}

	private void SetPoseOfGO(Vector3[] positions, Vector3[] normals)
	{
		Vector3 forward = calculateForwardDirFromQR ? (positions[0] - positions[1]).normalized : Vector3.forward;
		Vector3 right = (positions[2] - positions[1]).normalized;
		Vector3 up = Vector3.up;
		switch (upDirection)
		{
			case UPDirection.POINTCLOUD:
				for (int i = 0; i < normals.Length; i++)
				{
					up += normals[i];
				}
				up = up.normalized;
				break;

			case UPDirection.QR:
				up = -Vector3.Cross(forward, right).normalized;
				break;
			case UPDirection.WORLD:
				up = Vector3.up;
				break;
		}
		// forward-vector projected onto plane defined by up-vector https://en.wikipedia.org/wiki/Vector_projection
		Vector3 rejForward = forward - Vector3.Project(forward, up);		
		Quaternion rotation = Quaternion.LookRotation(rejForward, up);
		Vector3 pos = (positions[0] + positions[1] + positions[2]) / 3;
		transform.SetPositionAndRotation(pos, rotation);
    }


	private static Result DecodeQr(LuminanceSource luminanceSource)
	{
		// way 1 ZXing barcode reader
		// var barcodeReader = new BarcodeReaderGeneric { AutoRotate = false, Options = new ZXing.Common.DecodingOptions { TryHarder = false } };
		// var result = barcodeReader.Decode(luminanceSource);

		// way 2 Zxing QRCodeReader
		var qrReader = new QRCodeReader();
		var binarizer = new HybridBinarizer(luminanceSource);
		var binaryBitmap = new BinaryBitmap(binarizer);
		var result = qrReader.decode(binaryBitmap);

		return result;
	}

	private bool HasScenePermission()
	{
#if UNITY_ANDROID
		return UnityEngine.Android.Permission.HasUserAuthorizedPermission(SPATIAL_PERMISSION);
#else
		return true;
#endif
	}

	private (Vector3?, Vector3?) PlaceGameObjectByScreenPosAndRot(Ray ray)
        {
            if (EnvironmentRaycastManager.IsSupported)
            {
                if (raycastManager.Raycast(ray, out var hitInfo))
                {
                    return (hitInfo.point, hitInfo.normal);
                }
                else
                {
                    Debug.Log("RaycastManager failed");
                    return (null, null);
                }
            }
            else
            {
                Debug.LogError("EnvironmentRaycastManager is not supported");
                return (null, null);
            }
        }

	private enum TRACKING_STYLE
	{
		MOTION_CONTROLLER,
		QR
	}


	private enum UPDirection
	{
		POINTCLOUD,
		QR,
		WORLD
	}

}
