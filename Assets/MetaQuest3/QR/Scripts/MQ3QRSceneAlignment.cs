using UnityEngine;

public class MQ3QRSceneAlignment : QRSceneAlignment
{

	[SerializeField] private bool startTrackingOnStart = true;
	[SerializeField] private QRTackingManager trackingManager;
	// [SerializeField] private TRACKING_STYLE trackingStyle = TRACKING_STYLE.QR;
	private bool isTracking = false;


    private void Start()
	{
		if (startTrackingOnStart)
		{
			StartQRTracking(new QRSceneAlignmentData());
		}
	}

    private void Update()
	{
		if (isTracking) trackingManager.OnTrackingQR();
	}

	public override void StartQRTracking(QRSceneAlignmentData data)
	{
		// if (trackingStyle == TRACKING_STYLE.QR) trackingManager.StartQRTracking();
		if (trackingManager == null) { 
			Debug.LogError("MQ3QRSceneAlignment: trackingManager is not assigned. Please assign it in the inspector.");
			return;
		}
		isTracking = true;
		ApplyOffset();
		}


		public override void StopQRTracking()
		{
			isTracking = false;

			// TODO: Seperate the logic for stopping tracking based on the style
			// if (trackingStyle == TRACKING_STYLE.QR)
			trackingManager.StopQRTracking();
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


	private enum TRACKING_STYLE
	{
		MOTION_CONTROLLER,
		QR
	}



}
