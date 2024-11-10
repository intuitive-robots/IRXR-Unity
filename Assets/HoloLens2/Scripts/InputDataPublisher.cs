using UnityEngine;
using Microsoft.MixedReality.Toolkit;

public class InputDataPublisher : Streamer
{
    [SerializeField]
    private Transform gazeTrans;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    protected override void SetupTopic() {
        _topic = "InputData";
    }

    // Update is called once per frame
    void Update()
    {
        gazeTrans.transform.position = CoreServices.InputSystem.EyeGazeProvider.HitPosition;
    }
}
