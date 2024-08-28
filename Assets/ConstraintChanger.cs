using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

public class ConstraintChanger : MonoBehaviour
{
    
    private enum ScaleMode {
        ALL,
        X,
        Y,
        Z
    }

    [SerializeField]
    private Grabbable _grabbable;

    [SerializeField]
    private ScaleMode _scaleMode = ScaleMode.ALL;

    [SerializeField]
    private GrabFreeTransformer _transformer;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    
    private TransformerUtils.ScaleConstraints getCustomScaleConstraints(bool x, bool y, bool z) {
        TransformerUtils.FloatRange range = new TransformerUtils.FloatRange(){
            Min = 1.0f,
            Max = 1.0f
        };
        TransformerUtils.ScaleConstraints scaleConstraints = new TransformerUtils.ScaleConstraints()
        {
            XAxis = new TransformerUtils.ConstrainedAxis()
            {
                ConstrainAxis = x,
                AxisRange = range
            },
            YAxis = new TransformerUtils.ConstrainedAxis()
            {
                ConstrainAxis = y,
                AxisRange = range
            },
            ZAxis = new TransformerUtils.ConstrainedAxis()
            {
                ConstrainAxis = z,
                AxisRange = range
            },
            ConstraintsAreRelative = true
            
        };
        return scaleConstraints;
    }

    private ScaleMode nextScaleMode(ScaleMode scaleMode) {
        return (ScaleMode)(((int)scaleMode + 1) % 4);
    }

    void Update() {
        if (OVRInput.GetDown(OVRInput.Button.Three))
        {
            _scaleMode = nextScaleMode(_scaleMode);
            Debug.Log("[CustomGT] Scale Mode: " + _scaleMode);
            bool x = _scaleMode == ScaleMode.Y || _scaleMode == ScaleMode.Z,
                y = _scaleMode == ScaleMode.X || _scaleMode == ScaleMode.Z,
                z = _scaleMode == ScaleMode.X || _scaleMode == ScaleMode.Y;
            
            var scaleConstraints = getCustomScaleConstraints(x, y, z);
            _transformer.InjectOptionalScaleConstraints(scaleConstraints);
            _transformer.Initialize(_grabbable);
        }
    }
}