using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class JointController : MonoBehaviour
{
    static public Type GetJointType(string jointType) {
      switch (jointType) {
        case "HINGE": return typeof(HingeJointController);
        case "SLIDE": return typeof(SlideJointController);
        case "BALL" : return typeof(BallJointController);
        case "FREE" : return typeof(FreeJointController);
        default: return typeof(FixedJointController);
      }
    }
    public abstract void InitializeState(SimJoint data);

    public abstract void SetValue(List<float> value);
}


class HingeJointController : JointController {
  
    [SerializeField] private float _maxRot;
    [SerializeField] private float _minRot;
    [SerializeField] private Vector3 _axis;
    [SerializeField] private float _value = 0.0f;
    [SerializeField] private float _velocity = 0.0f;

    public override void InitializeState(SimJoint data) {
      _axis = new Vector3(data.Axis[0], data.Axis[1], data.Axis[2]);
      _minRot = data.Minrot;
      _maxRot = data.Maxrot;

      transform.Rotate(_axis, data.Initial);
    }

    
    public override void SetValue(List<float> value) {
      float new_pos = value[0];
      if (new_pos > _maxRot || new_pos < _minRot) return;

      float diff = new_pos - _value;
      _velocity = value[1];
      _value += diff;
      transform.Rotate(_axis, diff); 
    }

    public void Update() {
      float diff = Time.deltaTime; 
      float value  = _velocity * diff;
      if (value + _value > _maxRot || value + _value < _minRot) return;
      _value += value;
      transform.Rotate(_axis, value);
    }
}

class SlideJointController : JointController {

  private float _maxRot;
  private float _minRot;
  [SerializeField] private Vector3 _axis;
  [SerializeField] private float _value = 0.0f;
  [SerializeField] private float _velocity = 0.0f;
  public override void InitializeState(SimJoint data) {
    _axis = new Vector3(data.Axis[0], data.Axis[1], data.Axis[2]);

    _minRot = data.Minrot;
    _maxRot = data.Maxrot;

    transform.Translate(_axis * data.Initial);
  }

  
  public override void SetValue(List<float> value) {
    float new_pos = value[0];
    // Debug.Log(new_pos, _minRot, _maxRot);
    // if (new_pos > _maxRot || new_pos < _minRot) return;
    float diff = new_pos - _value;
    _velocity = value[1];
    _value += diff;
    transform.Translate(_axis * diff);
  }

  public void Update() {
    float diff = Time.deltaTime;
    float value  = _velocity * diff;
    if (value + _value > _maxRot || value + _value < _minRot) return;
    _value += value;
    transform.Translate(_axis * value);
  }
}

class FreeJointController : JointController {
  public override void InitializeState(SimJoint data) {}

  
  public override void SetValue(List<float> value) {
    var pos = new Vector3(value[0], value[1], value[2]);
    var rot = Quaternion.Euler(value[3], value[4], value[5]);
    transform.SetPositionAndRotation(pos, rot);  
  }
}


class BallJointController : JointController {

    private Vector3 _rotationPoint;

    public override void InitializeState(SimJoint data) {
    }
    
    public override void SetValue(List<float> value) {
    }
} 

class FixedJointController : JointController {
    public override void InitializeState(SimJoint data) {}
    
    public override void SetValue(List<float> value) {}
} 