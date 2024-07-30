using NetMQ;
using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class MetaQuest3InputData
{
    public List<float> left_pos;
    public List<float> left_rot;
    public bool left_index_trigger;
    public bool left_hand_trigger;
    public List<float> right_pos;
    public List<float> right_rot;
    public bool right_index_trigger;
    public bool right_hand_trigger;
    public bool A;
    public bool B;
    public bool X;
    public bool Y;
}

public class MetaQuest3InputStreamer : StreamPublisher
{

    [SerializeField] private Transform trackingSpace;
    [SerializeField] private Transform rootTrans;

    void Update() {
        MetaQuest3InputData inputData = new MetaQuest3InputData();
        Vector3 leftPos = trackingSpace.TransformPoint(OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch));
        leftPos = rootTrans.InverseTransformPoint(leftPos);
        inputData.left_pos = new List<float> {leftPos.z, -leftPos.x, leftPos.y};
        Quaternion leftRot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch) * trackingSpace.rotation;
        leftRot = leftRot * Quaternion.Inverse(rootTrans.rotation);
        inputData.left_rot = new List<float> {-leftRot.z, leftRot.x, -leftRot.y, leftRot.w};
        inputData.left_index_trigger = OVRInput.Get(OVRInput.RawButton.LIndexTrigger);
        inputData.left_hand_trigger = OVRInput.Get(OVRInput.RawButton.LHandTrigger);

        Vector3 rightPos =  trackingSpace.TransformPoint(OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch));
        rightPos = rootTrans.InverseTransformPoint(rightPos);
        inputData.right_pos = new List<float> {rightPos.z, -rightPos.x, rightPos.y};
        Quaternion rightRot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch) * trackingSpace.rotation;
        rightRot = rightRot * Quaternion.Inverse(rootTrans.rotation);
        inputData.right_rot = new List<float> {-rightRot.z, rightRot.x, -rightRot.y, rightRot.w};
        inputData.right_index_trigger = OVRInput.Get(OVRInput.RawButton.RIndexTrigger);
        inputData.right_hand_trigger = OVRInput.Get(OVRInput.RawButton.RHandTrigger);

        inputData.A = OVRInput.Get(OVRInput.RawButton.A);
        inputData.B = OVRInput.Get(OVRInput.RawButton.B);
        inputData.X = OVRInput.Get(OVRInput.RawButton.X);
        inputData.Y = OVRInput.Get(OVRInput.RawButton.Y);

        Publish(inputData);
    }
}