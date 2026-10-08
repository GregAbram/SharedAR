using System;
using System.IO;
using UnityEngine;

// Appends this app's own log lines - those starting with a [Component] tag -
// plus every warning and error, with timestamps, to sharedar.log in
// persistentDataPath. The device's system log is overwritten within minutes;
// this keeps a whole session. The previous session is kept as sharedar.prev.log.
// Quest: adb pull /sdcard/Android/data/edu.utexas.tacc.sharedar/files/sharedar.log
// iPhone: Files app (On My iPhone > SharedAR), or Finder with the phone connected.
public static class SessionLogFile
{
    private static readonly object Gate = new();
    private static StreamWriter writer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Start()
    {
        if (Application.isEditor)
        {
            return;
        }
        try
        {
            var path = Path.Combine(Application.persistentDataPath, "sharedar.log");
            var previous = Path.Combine(Application.persistentDataPath, "sharedar.prev.log");
            if (File.Exists(path))
            {
                File.Copy(path, previous, true);
            }
            writer = new StreamWriter(path, false) { AutoFlush = true };
            writer.WriteLine($"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} {Application.productName} {Application.version} on {SystemInfo.deviceModel} ({SystemInfo.operatingSystem})");
            Application.logMessageReceivedThreaded += OnLog;
            Application.quitting += () =>
            {
                lock (Gate)
                {
                    writer?.Dispose();
                    writer = null;
                }
            };
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SessionLogFile] Can't write the session log: {e.Message}");
        }
    }

    private static void OnLog(string message, string stackTrace, LogType type)
    {
        var important = type is LogType.Warning or LogType.Error or LogType.Exception or LogType.Assert;
        if (!important && !message.StartsWith("["))
        {
            return;
        }
        lock (Gate)
        {
            if (writer == null)
            {
                return;
            }
            writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {(important ? type.ToString().ToUpperInvariant() + " " : "")}{message}");
            if (type is LogType.Error or LogType.Exception && !string.IsNullOrEmpty(stackTrace))
            {
                writer.WriteLine(stackTrace.TrimEnd());
            }
        }
    }
}
