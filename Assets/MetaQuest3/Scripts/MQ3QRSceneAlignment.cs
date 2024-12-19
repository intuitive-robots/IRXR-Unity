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
        transform.position = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
		Quaternion rot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch);
		Vector3 euler = rot.eulerAngles;
        transform.rotation = Quaternion.Euler(0, euler.y, 0);
	}

}
