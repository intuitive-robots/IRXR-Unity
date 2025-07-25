using UnityEngine;

public class MQ3QRSceneAlignment : QRSceneAlignment
{

	[SerializeField] private bool startTrackingOnStart = false;
	[SerializeField] private QRTackingManager trackingManager;
	// [SerializeField] private TRACKING_STYLE trackingStyle = TRACKING_STYLE.QR;


    private void Start()
	{
		// if (trackingStyle == TRACKING_STYLE.QR) trackingManager.StartQRTracking();
		if (trackingManager == null) { 
			Debug.LogError("MQ3QRSceneAlignment: trackingManager is not assigned. Please assign it in the inspector.");
			return;
		}

		startAlignmentService = new("StartQRAlignment", StartQRAlignment);
        stopAlignmentService = new("StopQRAlignment", StopQRAlignment);

		if (startTrackingOnStart)
		{
			StartQRAlignment(new QRSceneAlignmentData());
		}
	}

    private void Update()
	{
		if (isTrackingQR) trackingManager.OnTrackingQR();
	}

	public override string StartQRAlignment(QRSceneAlignmentData data)
	{
		trackingManager.StartQRTracking();
		return base.StartQRAlignment(data);
	}


	public override string StopQRAlignment(string signal)
	{
		trackingManager.StopQRTracking();
		return base.StopQRAlignment(signal);
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
}
