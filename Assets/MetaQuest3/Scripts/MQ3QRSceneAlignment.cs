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
        Vector3 leftPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
		Vector3 rightPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
		transform.position = (leftPos + rightPos) / 2;
		Quaternion leftRot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch);
		Quaternion rightRot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);
        transform.rotation = Quaternion.Slerp(leftRot, rightRot, 0.5f);
	}

}
