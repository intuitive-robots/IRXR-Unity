using UnityEngine;


public class MQ3QRSceneAlignment : QRSceneAlignment
{
	private bool isTracking = false;

	private void Update() {
		if (isTracking) {
			OnTrackingMotionController();
		}
	}

	public override void StartQRTracking(QRSceneAlignmentData data)
	{
		isTracking = true;
		ApplyOffset();
	}

	public override void StopQRTracking()
	{
		isTracking = false;
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
