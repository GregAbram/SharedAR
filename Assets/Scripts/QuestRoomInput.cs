#if UNITY_ANDROID || UNITY_EDITOR
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Quest controls for the background-scanning RoomAnchor scene: A = scan at full
// rate now, B = forget this session's observations (re-anchor), X = forget all
// learned tag positions, left trigger = scan a room code (QR), Y = switch scene. The anchor's
// status floats in front of the headset and follows the head smoothly.
public class QuestRoomInput : MonoBehaviour
{
    [SerializeField] private RoomAnchor roomAnchor;
    [SerializeField] private RoomCodeReader roomCodeReader;
    [SerializeField] private TextMeshPro statusText;
    [SerializeField] private Transform head;
    [SerializeField] private string otherSceneName = "Quest Survey";
    [SerializeField] private float textDistance = 1.2f;
    [SerializeField] private float textDrop = 0.25f;
    [SerializeField] private float followSpeed = 3f;

    private void OnEnable()
    {
        roomAnchor.Localized += OnLocalized;
    }

    private void OnDisable()
    {
        roomAnchor.Localized -= OnLocalized;
    }

    private void Awake()
    {
        // Plain log lines without stack traces: on device they fill the log
        // buffer and push out the lines worth reading. Warnings and errors keep theirs.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
    }

    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            roomAnchor.Rescan();
            StartCoroutine(Pulse(0.3f, 0.05f));
        }
        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            roomAnchor.ClearTags();
            StartCoroutine(Pulse(0.3f, 0.05f));
        }
        if (OVRInput.GetDown(OVRInput.Button.Three))
        {
            roomAnchor.ForgetLearnedTags();
            StartCoroutine(Pulse(0.3f, 0.05f));
        }
        if (roomCodeReader != null && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
        {
            if (roomCodeReader.IsScanning)
            {
                roomCodeReader.CancelScan();
            }
            else
            {
                roomCodeReader.BeginScan();
            }
            StartCoroutine(Pulse(0.3f, 0.05f));
        }
        if (OVRInput.GetDown(OVRInput.Button.Four))
        {
            SceneManager.LoadScene(otherSceneName);
        }

        statusText.text = "A: scan now   B: re-anchor   X: forget learned   L trigger: room code   Y: other scene\n" +
                          (roomCodeReader != null ? roomCodeReader.StatusLine + "\n" : "") + roomAnchor.StatusText;

        // Ease toward a point in front of and slightly below the eyes, facing them.
        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        var target = head.position + forward * textDistance + Vector3.down * textDrop;
        var t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        statusText.transform.position = Vector3.Lerp(statusText.transform.position, target, t);
        statusText.transform.rotation = Quaternion.LookRotation(statusText.transform.position - head.position, Vector3.up);
    }

    private bool wasProvisional = true;

    // A firm pulse when the room becomes anchored.
    private void OnLocalized(RoomAnchor anchor)
    {
        if (wasProvisional && anchor.IsAnchored)
        {
            StartCoroutine(Pulse(1f, 0.15f));
        }
        wasProvisional = !anchor.IsAnchored;
    }

    private static IEnumerator Pulse(float amplitude, float seconds)
    {
        OVRInput.SetControllerVibration(1f, amplitude, OVRInput.Controller.Touch);
        yield return new WaitForSeconds(seconds);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);
    }
}
#endif
