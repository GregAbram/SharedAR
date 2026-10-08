using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

// iPhone AprilTag demo. Tap Acquire and point the camera at a tag: each lock
// places a colored cube at the room origin as solved from that tag.
//   Multi:  one cube per tag (replaced on re-lock); once two or more tags have
//           locked, RoomFit marks the best-fit origin with a white post.
//   Single: each Acquire forgets every earlier lock, so only the latest tag's
//           origin is shown.
public class AprilTagDemo : MonoBehaviour
{
    [SerializeField] private AprilTagRoomLocalizer localizer;
    [SerializeField] private Renderer tagCubeTemplate;
    [SerializeField] private Transform fitMarker;
    [SerializeField] private bool singleTagMode;
    [SerializeField] private Color[] tagColors =
    {
        new(0.95f, 0.3f, 0.25f), new(0.3f, 0.8f, 0.35f), new(0.3f, 0.55f, 0.95f),
        new(0.95f, 0.8f, 0.2f), new(0.8f, 0.4f, 0.9f), new(0.2f, 0.85f, 0.85f),
    };

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private readonly SortedDictionary<int, RoomOriginEstimate> estimates = new();
    private readonly Dictionary<int, Renderer> tagCubes = new();
    private RoomFitResult? fit;
    private string status = "Tap Acquire, then point the camera at a tag";
    private GUIStyle labelStyle;
    private GUIStyle shadowStyle;
    private GUIStyle buttonStyle;

    private void Awake()
    {
        tagCubeTemplate.gameObject.SetActive(false);
        fitMarker.gameObject.SetActive(false);
        localizer.EstimateAcquired += OnEstimateAcquired;
    }

    private void OnDestroy()
    {
        localizer.EstimateAcquired -= OnEstimateAcquired;
    }

    private void OnEstimateAcquired(RoomOriginEstimate estimate)
    {
        estimates[estimate.TagId] = estimate;

        if (!tagCubes.TryGetValue(estimate.TagId, out var cube))
        {
            cube = Instantiate(tagCubeTemplate);
            cube.name = $"Tag {estimate.TagId} origin";
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, tagColors[Mathf.Abs(estimate.TagId) % tagColors.Length]);
            cube.SetPropertyBlock(block);
            tagCubes[estimate.TagId] = cube;
        }
        cube.transform.SetPositionAndRotation(estimate.Position, estimate.Rotation);
        cube.gameObject.SetActive(true);

        status = $"Locked tag {estimate.TagId}: {estimate.SampleCount} samples, {estimate.TagPositionCameraLocal.z:F2} m away";
        UpdateFit();
    }

    private void UpdateFit()
    {
        var correspondences = new List<TagCorrespondence>();
        foreach (var estimate in estimates.Values)
        {
            correspondences.Add(new TagCorrespondence(estimate.TagId, estimate.TagRoomPosition, estimate.TagWorldPosition));
        }

        if (RoomFit.TryFit(correspondences, out var result))
        {
            fit = result;
            fitMarker.SetPositionAndRotation(result.Position, result.Rotation);
            fitMarker.gameObject.SetActive(true);

            var log = new StringBuilder($"[AprilTagDemo] RoomFit from {correspondences.Count} tags: origin {result.Position} yaw {result.Rotation.eulerAngles.y:F2}, RMS {result.RmsResidual * 100f:F1} cm;");
            foreach (var (tagId, residual) in result.Residuals)
            {
                log.Append($" tag {tagId} residual {residual * 100f} cm;");
            }
            Debug.Log(log.ToString());
        }
        else
        {
            fit = null;
            fitMarker.gameObject.SetActive(false);
        }
    }

    private void Forget()
    {
        estimates.Clear();
        foreach (var cube in tagCubes.Values)
        {
            Destroy(cube.gameObject);
        }
        tagCubes.Clear();
        fit = null;
        fitMarker.gameObject.SetActive(false);
    }

    private void OnGUI()
    {
        var unit = Mathf.Min(Screen.width, Screen.height) / 20f;
        labelStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
        shadowStyle ??= new GUIStyle(labelStyle) { normal = { textColor = Color.black } };
        buttonStyle ??= new GUIStyle(GUI.skin.button);
        labelStyle.fontSize = Mathf.RoundToInt(unit * 0.55f);
        shadowStyle.fontSize = labelStyle.fontSize;
        buttonStyle.fontSize = Mathf.RoundToInt(unit * 0.7f);

        var safe = Screen.safeArea;
        var left = safe.xMin + unit * 0.5f;
        var top = Screen.height - safe.yMax + unit * 0.5f;
        var buttonWidth = unit * 5f;
        var buttonHeight = unit * 1.6f;
        var step = buttonWidth + unit * 0.5f;

        if (localizer.IsAcquiring)
        {
            if (GUI.Button(new Rect(left, top, buttonWidth, buttonHeight), "Cancel", buttonStyle))
            {
                localizer.CancelAcquisition();
                status = "Cancelled";
            }
        }
        else if (GUI.Button(new Rect(left, top, buttonWidth, buttonHeight), "Acquire", buttonStyle))
        {
            if (singleTagMode)
            {
                Forget();
            }
            localizer.BeginAcquisition();
            status = "Acquiring - hold a tag near the center of the view";
        }
        if (GUI.Button(new Rect(left + step, top, buttonWidth, buttonHeight), "Reset", buttonStyle))
        {
            localizer.CancelAcquisition();
            Forget();
            status = "Cleared. Tap Acquire, then point the camera at a tag";
        }
        if (GUI.Button(new Rect(left + step * 2f, top, buttonWidth, buttonHeight), singleTagMode ? "Mode: Single" : "Mode: Multi", buttonStyle))
        {
            singleTagMode = !singleTagMode;
            localizer.CancelAcquisition();
            Forget();
            status = singleTagMode ? "Single tag: each Acquire forgets earlier locks" : "Multi tag: locks accumulate and are fitted together";
        }

        var text = new StringBuilder();
        text.AppendLine($"AR session: {ARSession.state}   localizer: {(localizer.IsReady ? "ready" : "starting")}   screen: {Screen.orientation}");
        text.AppendLine(status);
        foreach (var estimate in estimates.Values)
        {
            var tag = estimate.TagRoomPosition;
            text.Append($"Tag {estimate.TagId} at room ({tag.x:F2}, {tag.y:F2}, {tag.z:F2}) m, {estimate.TagPositionCameraLocal.z:F2} m from camera");
            if (fit.HasValue)
            {
                // This tag's origin estimate relative to the fit, in room axes.
                var offset = Quaternion.Inverse(fit.Value.Rotation) * (estimate.Position - fit.Value.Position) * 100f;
                text.Append($", origin vs fit ({offset.x:F1}, {offset.y:F1}, {offset.z:F1}) cm");
            }
            text.AppendLine();
        }
        if (fit.HasValue)
        {
            text.AppendLine($"Fit: {estimates.Count} tags, RMS residual {fit.Value.RmsResidual * 100f:F1} cm");
            foreach (var (tagId, residual) in fit.Value.Residuals)
            {
                var cm = residual * 100f;
                text.AppendLine($"  tag {tagId} residual ({cm.x:F1}, {cm.y:F1}, {cm.z:F1}) cm");
            }
        }

        var textTop = top + buttonHeight + unit * 0.3f;
        var area = new Rect(left, textTop, safe.width - unit, Screen.height - textTop);
        GUI.Label(new Rect(area.x + 2, area.y + 2, area.width, area.height), text.ToString(), shadowStyle);
        GUI.Label(area, text.ToString(), labelStyle);
    }
}
