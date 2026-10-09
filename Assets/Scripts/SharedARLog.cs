using UnityEngine;

// Turns on the AprilTags package's session log for this app: sharedar.log in
// persistentDataPath (previous session in sharedar.prev.log).
public static class SharedARLog
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Begin() => SessionLogFile.Begin("sharedar.log");
}
