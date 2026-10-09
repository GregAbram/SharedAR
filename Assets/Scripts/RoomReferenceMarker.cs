using UnityEngine;

// A fixed point for checking that viewers agree: keeps this Transform (a child
// of the RoomAnchor) on the floor (room y = 0) below the center of the measured
// tags. Each viewer sees it where their own device places the room; the
// distance between where two devices show it is how far they disagree there.
public class RoomReferenceMarker : MonoBehaviour
{
    [SerializeField] private RoomAnchor roomAnchor;

    private void LateUpdate()
    {
        var sum = Vector3.zero;
        var count = 0;
        foreach (var id in roomAnchor.PlacedTagIds)
        {
            if (roomAnchor.TryGetTagRoomPosition(id, out var position, out var measured) && measured)
            {
                sum += position;
                count++;
            }
        }
        if (count > 0)
        {
            var center = sum / count;
            transform.localPosition = new Vector3(center.x, 0f, center.z);
            transform.localRotation = Quaternion.identity;
        }
    }
}
