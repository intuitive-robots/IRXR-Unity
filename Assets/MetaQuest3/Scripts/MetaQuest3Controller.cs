using System.Collections.Generic;
using UnityEngine;

public class MetaQuest3Hand
{
    public List<float> pos;
    public List<float> rot;
    public bool index_trigger;
    public bool hand_trigger;
}

public class MetaQuest3InputData
{
    public MetaQuest3Hand left;
    public MetaQuest3Hand right;
    public bool A;
    public bool B;
    public bool X;
    public bool Y;
}

public class MetaQuest3Controller : Streamer
{

    [SerializeField] private Transform trackingSpace;
    [SerializeField] private Transform rootTrans;

    protected override void SetupTopic()
    {
        _topic = "InputData";
    }

    protected override void Initialize()
    {
        IRXRNetManager _netManager = IRXRNetManager.Instance;
        string hostName = _netManager.GetHostName();
        _netManager.SubscribeTopic($"{hostName}/StartVibration", StartVibration);
        _netManager.SubscribeTopic($"{hostName}/StopVibration", StopVibration);
    }

    public void StartVibration(string message) {
        if (message == "left")
        {
           OVRInput.SetControllerVibration(1.0f, 1.0f, OVRInput.Controller.LTouch);
        }
        else if (message == "right")
        {
            OVRInput.SetControllerVibration(1.0f, 1.0f, OVRInput.Controller.RTouch);
        }
    }


    public void StopVibration(string message) {
        if (message == "left")
        {
            OVRInput.SetControllerVibration(0.0f, 0.0f, OVRInput.Controller.LTouch);
        }
        else if (message == "right")
        {
            OVRInput.SetControllerVibration(0.0f, 0.0f, OVRInput.Controller.RTouch);
        }
    }



    void Update() {
        MetaQuest3InputData inputData = new();
        // left hand
        MetaQuest3Hand leftHand = new();
        Vector3 leftPos = trackingSpace.TransformPoint(OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch));
        leftPos = rootTrans.InverseTransformPoint(leftPos);
        leftHand.pos = new List<float> {leftPos.z, -leftPos.x, leftPos.y};
        Quaternion leftRot = trackingSpace.rotation * OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch);
        leftRot *=  Quaternion.Inverse(rootTrans.rotation);
        leftHand.rot = new List<float> {-leftRot.z, leftRot.x, -leftRot.y, leftRot.w};
        leftHand.index_trigger = OVRInput.Get(OVRInput.RawButton.LIndexTrigger);
        leftHand.hand_trigger = OVRInput.Get(OVRInput.RawButton.LHandTrigger);
        inputData.left = leftHand;
        // right hand
        MetaQuest3Hand rightHand = new();
        Vector3 rightPos =  trackingSpace.TransformPoint(OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch));
        rightPos = rootTrans.InverseTransformPoint(rightPos);
        rightHand.pos = new List<float> {rightPos.z, -rightPos.x, rightPos.y};
        Quaternion rightRot = trackingSpace.rotation * OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);
        rightRot *= Quaternion.Inverse(rootTrans.rotation);
        rightHand.rot = new List<float> {-rightRot.z, rightRot.x, -rightRot.y, rightRot.w};
        rightHand.index_trigger = OVRInput.Get(OVRInput.RawButton.RIndexTrigger);
        rightHand.hand_trigger = OVRInput.Get(OVRInput.RawButton.RHandTrigger);
        inputData.right = rightHand;
        // other buttons
        inputData.A = OVRInput.Get(OVRInput.RawButton.A);
        inputData.B = OVRInput.Get(OVRInput.RawButton.B);
        inputData.X = OVRInput.Get(OVRInput.RawButton.X);
        inputData.Y = OVRInput.Get(OVRInput.RawButton.Y);

        _publisher.Publish(inputData);
    }
}