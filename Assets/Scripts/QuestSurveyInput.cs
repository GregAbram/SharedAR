#if UNITY_ANDROID || UNITY_EDITOR
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Quest controls for the survey scene (RoomAnchor in survey mode): A = scan at
// full rate now, B = re-anchor, X = forget learned tags (start the survey over),
// right trigger = save the surveyed room_config.json, Y = switch scene. Status
// floats in front of the headset and follows the head smoothly.
public class QuestSurveyInput : MonoBehaviour
{
    [SerializeField] private RoomAnchor roomAnchor;
    [SerializeField] private TextMeshPro statusText;
    [SerializeField] private Transform head;
    [SerializeField] private string otherSceneName = "Quest AprilTags";
    [SerializeField] private float textDistance = 1.2f;
    [SerializeField] private float textDrop = 0.25f;
    [SerializeField] private float followSpeed = 3f;

    private string saveMessage = "";

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
            saveMessage = "";
            StartCoroutine(Pulse(0.3f, 0.05f));
        }
        if (OVRInput.GetDown(OVRInput.Button.Three))
        {
            roomAnchor.ForgetLearnedTags();
            saveMessage = "";
            StartCoroutine(Pulse(0.3f, 0.05f));
        }
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            Save();
        }
        if (OVRInput.GetDown(OVRInput.Button.Four))
        {
            SceneManager.LoadScene(otherSceneName);
        }

        statusText.text = "SURVEY   A: scan now   B: re-anchor   X: start over   Trigger: save   Y: other scene\n" +
                          roomAnchor.StatusText + saveMessage;

        // Ease toward a point in front of and slightly below the eyes, facing them.
        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        var target = head.position + forward * textDistance + Vector3.down * textDrop;
        var t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        statusText.transform.position = Vector3.Lerp(statusText.transform.position, target, t);
        statusText.transform.rotation = Quaternion.LookRotation(statusText.transform.position - head.position, Vector3.up);
    }

    private void Save()
    {
        if (!roomAnchor.IsAnchored)
        {
            saveMessage = "\nNot saved: the room isn't anchored yet.";
            StartCoroutine(Pulse(0.3f, 0.05f));
            return;
        }
        var (path, tags) = roomAnchor.SaveSurveyedConfig();
        var config = roomAnchor.BuildSurveyedConfig();
        var codeImage = RoomCodeImage.Save(config);
        saveMessage = tags < 2
            ? $"\nSaved only {tags} tag - learn at least one more before using it."
            : $"\nSaved {tags} tags (survey {RoomCode.SurveyId(config)}) to {System.IO.Path.GetFileName(path)}" +
              (codeImage != null ? " and room_code.png (print it for the room)." : ".");
        StartCoroutine(Pulse(1f, 0.15f));
    }

    private static IEnumerator Pulse(float amplitude, float seconds)
    {
        OVRInput.SetControllerVibration(1f, amplitude, OVRInput.Controller.Touch);
        yield return new WaitForSeconds(seconds);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);
    }
}
#endif
