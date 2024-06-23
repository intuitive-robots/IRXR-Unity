using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public abstract class JointController : MonoBehaviour
{
  
    [SerializeField] protected Vector3 _axis;
    [SerializeField] protected float _value = 0.0f;

    static public Type GetJointType(string jointType) {
      switch (jointType) {
        case "HINGE": return typeof(HingeJointController);
        case "SLIDE": return typeof(SlideJointController);
        case "BALL" : return typeof(BallJointController);
        case "FREE" : return typeof(FreeJointController);
        default: return typeof(FixedJointController);
      }
    }
    
    public virtual void InitializeState(SimJoint data) {
      Assert.AreEqual(data.Axis.Count, 3);
      _axis = new Vector3(data.Axis[0], data.Axis[1], data.Axis[2]);

      Move(data.Initial);
    }

    
    public virtual void SetValue(List<float> value) {
      Assert.AreEqual(value.Count, 1);
      float diff = value[0] - _value;
      Move(diff);
      _value += diff; 
    }

    public virtual void Move(float amount) {}
}


class HingeJointController : JointController {
    public override void Move(float amount) {
      transform.Rotate(_axis, amount);
    }
}

class SlideJointController : JointController {
  public override void Move(float amount) {
      transform.Translate(_axis * amount);
  }
}

class FreeJointController : JointController {
  public override void InitializeState(SimJoint data) {}

  
  public override void SetValue(List<float> value) {
    var pos = new Vector3(value[0], value[1], value[2]);
    var rot = new Quaternion(value[3], value[4], value[5], value[6]);
    transform.SetPositionAndRotation(pos, rot);  
  }
}


class BallJointController : JointController {
} 

class FixedJointController : JointController {
} 