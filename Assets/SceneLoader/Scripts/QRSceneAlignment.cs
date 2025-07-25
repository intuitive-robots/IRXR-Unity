using System;
using System.Collections.Generic;
using UnityEngine;
using IRXR.Node;
using IRXR.Utilities;

public class QRSceneAlignment : MonoBehaviour {

    // [Serializable]
    public class QRSceneAlignmentData {
        public string qrText;
        public List<float> pos;
        public List<float> euler;
        public bool fixZAxis;
        public Vector3 GetPos() {
            return new Vector3(-pos[1], pos[2], pos[0]);
        }

        public Quaternion GetRot() {
            if (fixZAxis) {
                return Quaternion.Euler(0, euler[2], 0);
            }
            else {
                return Quaternion.Euler(euler[1], -euler[2], -euler[0]);
            }
        }
    }

    [SerializeField] protected GameObject indicator;
    protected QRSceneAlignmentData _data;
    protected bool isTrackingQR = false;
    protected Service<QRSceneAlignmentData, string> startAlignmentService;
    protected Service<string, string> stopAlignmentService;

    private void Start() {
        startAlignmentService = new("StartQRAlignment", StartQRAlignment);
        stopAlignmentService = new("StopQRAlignment", StopQRAlignment);
    }

    protected void ApplyOffset() {
        foreach (Transform child in transform) {
            if (child.gameObject == indicator) 
                continue;
            child.SetLocalPositionAndRotation(_data.GetPos(), _data.GetRot());
        }
    }

    public virtual string StartQRAlignment(QRSceneAlignmentData data) {
        _data = data;
        isTrackingQR = true;
        indicator.SetActive(true);
        Debug.Log("Start QR Tracking");
        StartQRTracking(_data);
        return IRXRSignal.SUCCESS;
    }

    public virtual string StopQRAlignment(string signal) {
        isTrackingQR = false;
        indicator.SetActive(false);
        Debug.Log("Stop QR Tracking");
        StopQRTracking();
        return IRXRSignal.SUCCESS;
    }

    public virtual void StartQRTracking(QRSceneAlignmentData data) {
        ApplyOffset();  // Only for testing
    }

    public virtual void StopQRTracking() {
    }

    protected void SetSceneOrigin(Pose origin){
        transform.position = origin.position;
        transform.rotation = origin.rotation;
        ApplyOffset();
    }
}

