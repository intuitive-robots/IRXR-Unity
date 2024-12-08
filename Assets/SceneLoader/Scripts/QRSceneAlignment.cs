using System;
using System.Collections.Generic;
using UnityEngine;

public class QRSceneAlignment : MonoBehaviour {

    [Serializable] public class QRSceneAlignmentData {
        public string qrText;
        public List<float> pos;
        public List<float> euler;
        public bool fixeZ;
        public Vector3 GetPos() {
            // TODO: add transform
            return new Vector3(pos[0], pos[1], pos[2]);
        }

        public Quaternion GetRot() {
            // TODO: add transform
            return Quaternion.Euler(euler[0], euler[1], euler[2]);
        }
    }
    [SerializeField] private GameObject indicator;
    protected QRSceneAlignmentData _data;
    public bool isTracingQR = false;
    virtual public void StartQRTracing(QRSceneAlignmentData data) {
        _data = data;
        isTracingQR = true;
        indicator.SetActive(true);
    }

    virtual public void StopQRTracing() {
        isTracingQR = false;
        indicator.SetActive(false);
    }

    protected void SetSceneOrigin(Pose origin){
        indicator.transform.position = origin.position;
        indicator.transform.rotation = origin.rotation;
        transform.position = origin.position;
        transform.rotation = origin.rotation;
    }
}

