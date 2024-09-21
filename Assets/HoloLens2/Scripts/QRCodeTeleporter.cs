/*
 * QRCodeTeleporter.cs
 * Script for teleport GameObject to QR Code in real world
 */

using UnityEngine;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.OpenXR;
using Microsoft.MixedReality.SampleQRCodes;

public class QRCodeTeleporter : MonoBehaviour
{
    [SerializeField]
    private string targetQRData;
    [SerializeField]
    private Vector3 offsetPos;
    [SerializeField]
    private Vector3 offsetRot;

    private QRCodesManager qrCodeManager;
    private SpatialGraphNode node;

    // Start is called before the first frame update
    void Start()
    {
        qrCodeManager = GameObject.Find("QRCodesManager").GetComponent<QRCodesManager>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!qrCodeManager.IsTrackerRunning)
        {
            return;
        }

        if (node == null)
        {
            foreach (var ite in qrCodeManager.GetList())
            {
                if (ite.Data == targetQRData)
                {
                    node = SpatialGraphNode.FromStaticNodeId(ite.SpatialGraphNodeId);
                    break;
                }
            }
        }

        if (node != null)
        {
            if (node.TryLocate(FrameTime.OnUpdate, out Pose pose))
            {
                // If there is a parent to the camera that means we are using teleport and we should not apply the teleport
                // to these objects so apply the inverse
                if (CameraCache.Main.transform.parent != null)
                {
                    pose = pose.GetTransformedBy(CameraCache.Main.transform.parent);
                }
                Vector3 eulerAngles = pose.rotation.eulerAngles;
                
                Vector3 qrPosition = new Vector3(pose.position.x, pose.position.y, pose.position.z);
                Quaternion qrQuant = new Quaternion(pose.rotation.x, pose.rotation.y, pose.rotation.z, pose.rotation.w);
                transform.SetPositionAndRotation(qrPosition, qrQuant);
                transform.Translate(offsetPos);
                transform.Rotate(offsetRot.x, offsetRot.y, offsetRot.z, Space.Self);
                // Debug.Log("Moving...");
                // Debug.Log("Id= " + Id + " QRPose = " + pose.position.ToString("F7") + " QRRot = " + pose.rotation.ToString("F7"));
            }
            else
            {
                Debug.LogWarning("Cannot locate " + targetQRData);
            }
        }
    }

    public void TackingToggle()
    {
        if (!qrCodeManager.IsTrackerRunning)
        {
            qrCodeManager.StartQRTracking();
        }
        else
        {
            qrCodeManager.StopQRTracking();
        }
    }
}
