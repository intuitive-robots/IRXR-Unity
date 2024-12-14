using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

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

    private void Start() {
        IRXRNetManager.Instance.RegisterServiceCallback("StartQRTracing", StartQRTracing);
        IRXRNetManager.Instance.RegisterServiceCallback("StopQRTracing", StopQRTracing);
    }

    virtual public string StartQRTracing(string data) {
        Debug.Log("Start QR Tracing");
        _data = JsonConvert.DeserializeObject<QRSceneAlignmentData>(data);
        isTracingQR = true;
        indicator.SetActive(true);
        return "OK";
    }

    virtual public string StopQRTracing(string data) {
        Debug.Log("Stop QR Tracing");
        isTracingQR = false;
        indicator.SetActive(false);
        return "OK";
    }

    protected void SetSceneOrigin(Pose origin){
        indicator.transform.position = origin.position;
        indicator.transform.rotation = origin.rotation;
        transform.position = origin.position;
        transform.rotation = origin.rotation;
    }
}

