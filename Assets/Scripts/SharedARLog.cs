using UnityEngine;

// App startup: turns on the AprilTags package's session log (sharedar.log in
// persistentDataPath, previous session in sharedar.prev.log) and installs the
// QR room-code decoder explicitly rather than relying on startup order.
public static class SharedARLog
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Begin()
    {
        SessionLogFile.Begin("sharedar.log");
        ZXingRoomCode.Install();
    }
}
