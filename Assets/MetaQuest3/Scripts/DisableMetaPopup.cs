#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class DisableMetaPopup : MonoBehaviour
{
    private const string TelemetryEnabledKey = "OVRTelemetry.TelemetryEnabled";

    [SerializeField] private bool telemetryEnabled;

    private void OnValidate()
    {
        EditorPrefs.SetBool(TelemetryEnabledKey, telemetryEnabled);
        Debug.Log($"Meta Telemetry Enabled set to: {telemetryEnabled}");
    }

    private void OnEnable()
    {
        telemetryEnabled = EditorPrefs.GetBool(TelemetryEnabledKey, false);
    }
}
#endif