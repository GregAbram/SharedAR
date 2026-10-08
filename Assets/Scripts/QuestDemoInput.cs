#if UNITY_ANDROID || UNITY_EDITOR
using System.Collections;
using TMPro;
using UnityEngine;

// Quest controls for AprilTagDemo: A = Acquire/Cancel, B = Reset, X = Mode.
// The demo's status text floats in front of the headset and follows the head
// smoothly. A light haptic tick confirms each press.
public class QuestDemoInput : MonoBehaviour
{
    [SerializeField] private AprilTagDemo demo;
    [SerializeField] private TextMeshPro statusText;
    [SerializeField] private Transform head;
    [SerializeField] private float textDistance = 1.2f;
    [SerializeField] private float textDrop = 0.25f;
    [SerializeField] private float followSpeed = 3f;

    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            demo.Acquire();
            StartCoroutine(Pulse());
        }
        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            demo.ResetAll();
            StartCoroutine(Pulse());
        }
        if (OVRInput.GetDown(OVRInput.Button.Three))
        {
            demo.ToggleMode();
            StartCoroutine(Pulse());
        }

        statusText.text = "A: " + (demo.IsAcquiring ? "Cancel" : "Acquire") + "   B: Reset   X: Mode\n" + demo.StatusText;

        // Ease toward a point in front of and slightly below the eyes, facing them.
        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        var target = head.position + forward * textDistance + Vector3.down * textDrop;
        var t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        statusText.transform.position = Vector3.Lerp(statusText.transform.position, target, t);
        statusText.transform.rotation = Quaternion.LookRotation(statusText.transform.position - head.position, Vector3.up);
    }

    private static IEnumerator Pulse()
    {
        OVRInput.SetControllerVibration(1f, 0.3f, OVRInput.Controller.Touch);
        yield return new WaitForSeconds(0.05f);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);
    }
}
#endif
