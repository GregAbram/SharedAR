using UnityEditor;
using UnityEngine;

// Shared by the scene setup scripts.
public static class SharedARMarkers
{
    // A magenta post standing on the floor below the center of the measured tags
    // (RoomReferenceMarker), 1.6 m tall with a crossbar at 1 m, under the room.
    public static void AddReferenceMarker(RoomAnchor anchor, Material material)
    {
        var marker = new GameObject("Reference Marker");
        marker.transform.SetParent(anchor.transform, false);
        AddBar(marker.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.03f, 1.6f, 0.03f), material);
        AddBar(marker.transform, new Vector3(0f, 1.0f, 0f), new Vector3(0.3f, 0.03f, 0.03f), material);
        AddBar(marker.transform, new Vector3(0f, 1.0f, 0f), new Vector3(0.03f, 0.03f, 0.3f), material);
        var component = marker.AddComponent<RoomReferenceMarker>();
        var so = new SerializedObject(component);
        so.FindProperty("roomAnchor").objectReferenceValue = anchor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddBar(Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "Bar";
        Object.DestroyImmediate(bar.GetComponent<Collider>());
        bar.transform.SetParent(parent, false);
        bar.transform.localPosition = position;
        bar.transform.localScale = scale;
        bar.GetComponent<Renderer>().sharedMaterial = material;
    }
}
