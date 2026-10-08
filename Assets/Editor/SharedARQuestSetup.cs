using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Meta.XR;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

// Quest 3 settings: Meta's own stack (OVRCameraRig, Meta XR OpenXR feature,
// MRUK passthrough camera access) as in the tested PVLinkAR project. The Quest
// scene uses OVRCameraRig rather than the template's XR Origin because MRUK's
// PassthroughCameraAccess reports camera poses in Meta's tracking space, which
// is Unity world space under OVRCameraRig. Re-runnable; never modifies an
// existing scene. Needs the Android build target. Menu: Tools > SharedAR >
// Configure Quest, or batchmode:
// -buildTarget Android -executeMethod SharedARQuestSetup.ConfigureQuest
public static class SharedARQuestSetup
{
    // Scenes built for Quest; the first is the one the app starts in.
    public static readonly string[] QuestScenes =
    {
        "Assets/Scenes/Quest/Quest AprilTags.unity",
    };

    private const string CameraRigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";

    // Same OpenXR feature set as the tested AprilTags2/PVLinkAR Quest projects.
    private static readonly string[] OpenXRFeatures =
    {
        "MetaXRFeature",
        "MetaXRSubsampledLayout",
        "OculusTouchControllerProfile",
        "OculusTouchControllerProximityProfile",
        "MetaQuestTouchPlusControllerProfile",
    };

    [MenuItem("Tools/SharedAR/Configure Quest")]
    public static void ConfigureQuest()
    {
        ConfigurePlayer();
        ConfigureXR();
        ApplyMetaProjectSetupFixes();
        CreateQuestSceneIfMissing(QuestScenes[0]);
        // Build And Run in the editor uses this list; the iOS build passes its own.
        EditorBuildSettings.scenes = Array.ConvertAll(QuestScenes, path => new EditorBuildSettingsScene(path, true));
        AssetDatabase.SaveAssets();
        Debug.Log("[SharedARQuestSetup] Quest configured");
    }

    // Writes Builds/Quest/SharedAR.apk from the Quest scenes. Batchmode:
    // -buildTarget Android -executeMethod SharedARQuestSetup.BuildQuest
    [MenuItem("Tools/SharedAR/Build Quest APK")]
    public static void BuildQuest()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = QuestScenes,
            locationPathName = "Builds/Quest/SharedAR.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development,
        });
        Debug.Log($"[SharedARQuestSetup] Quest build {report.summary.result}: {report.summary.totalErrors} error(s)");
        if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }

    private static void ConfigurePlayer()
    {
        var android = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(android, "edu.utexas.tacc.sharedar");
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
        PlayerSettings.Android.forceInternetPermission = true;   // PVLink DataManager
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });

        // Assets/Plugins/Android/AndroidManifest.xml carries the HEADSET_CAMERA
        // permission MRUK's camera access needs; Unity only uses it with
        // "Custom Main Manifest" ticked, which has no public scripting API.
        var playerSettings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
        playerSettings.FindProperty("useCustomMainManifest").boolValue = true;
        playerSettings.ApplyModifiedPropertiesWithoutUndo();

        var config = OVRProjectConfig.CachedProjectConfig;
        config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Supported;
        OVRProjectConfig.CommitProjectConfig(config);
    }

    private static void ConfigureXR()
    {
        var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
        var manager = perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        perTarget.SettingsForBuildTarget(BuildTargetGroup.Android).InitManagerOnStart = true;
        if (!XRPackageMetadataStore.IsLoaderAssigned("UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android))
        {
            XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android);
        }
        EditorUtility.SetDirty(perTarget);

        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        foreach (var feature in settings.GetFeatures())
        {
            if (OpenXRFeatures.Contains(feature.GetType().Name))
            {
                feature.enabled = true;
            }
        }
        EditorUtility.SetDirty(settings);
        var enabled = settings.GetFeatures().Where(f => f.enabled).Select(f => f.GetType().Name).OrderBy(n => n).ToList();
        var missing = OpenXRFeatures.Except(enabled).ToList();
        Debug.Log($"[SharedARQuestSetup] OpenXR Android features enabled: {string.Join(", ", enabled)}" +
                  (missing.Count > 0 ? $"; NOT FOUND: {string.Join(", ", missing)}" : ""));
    }

    // Meta's Project Setup Tool fixes (its "Fix All"), Required + Recommended
    // only; Optional ones turn on features this project doesn't use. The fixer
    // API is internal, hence reflection. Android only.
    [MenuItem("Tools/SharedAR/Apply Meta Setup Fixes (Android)")]
    public static void ApplyMetaProjectSetupFixes()
    {
        var setupType = typeof(OVRProjectSetup);
        var fixTasks = setupType.GetMethod("FixTasks", BindingFlags.Static | BindingFlags.NonPublic);
        var logMessagesType = setupType.GetNestedType("LogMessages", BindingFlags.NonPublic);
        var taskType = fixTasks?.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments()[0];
        if (fixTasks == null || logMessagesType == null || taskType == null)
        {
            Debug.LogError("[SharedARQuestSetup] Meta setup tool API changed; run Meta > Tools > Project Setup Tool > Fix All manually.");
            return;
        }

        var target = BuildTargetGroup.Android;
        var filter = typeof(SharedARQuestSetup)
            .GetMethod(nameof(MakeFilter), BindingFlags.Static | BindingFlags.NonPublic)
            .MakeGenericMethod(taskType)
            .Invoke(null, new object[] { (Func<object, bool>)(task => GetTaskLevel(task, target) >= 1) });

        for (var pass = 0; pass < 3; pass++)
        {
            fixTasks.Invoke(null, new[] { target, filter, Enum.ToObject(logMessagesType, 3), true, null });
        }
        Debug.Log("[SharedARQuestSetup] Applied Meta Project Setup Tool fixes (Required + Recommended)");
    }

    private static Func<IEnumerable<T>, List<T>> MakeFilter<T>(Func<object, bool> keep) =>
        tasks => tasks.Where(task => keep(task)).ToList();

    private static int GetTaskLevel(object task, BuildTargetGroup target)
    {
        var level = task.GetType().GetProperty("Level")?.GetValue(task);
        var value = level?.GetType().GetMethod("GetValue", new[] { typeof(BuildTargetGroup) })?.Invoke(level, new object[] { target });
        return value == null ? -1 : Convert.ToInt32(value);
    }

    // A starting point only; once it exists the scene is edited by hand.
    private static void CreateQuestSceneIfMissing(string path)
    {
        if (File.Exists(path))
        {
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera rig with passthrough as an underlay behind a transparent clear,
        // camera permission requested at startup.
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CameraRigPrefabPath), scene);
        var ovrManager = rig.GetComponent<OVRManager>();
        ovrManager.isInsightPassthroughEnabled = true;
        var managerSo = new SerializedObject(ovrManager);
        managerSo.FindProperty("requestPassthroughCameraAccessPermissionOnStartup").boolValue = true;
        managerSo.ApplyModifiedPropertiesWithoutUndo();
        rig.AddComponent<OVRPassthroughLayer>();   // always an underlay as of SDK 207
        var centerEye = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
        var eyeCamera = centerEye.GetComponent<Camera>();
        eyeCamera.clearFlags = CameraClearFlags.SolidColor;
        eyeCamera.backgroundColor = Color.clear;

        var cameraAccess = new GameObject("PassthroughCameraAccess").AddComponent<PassthroughCameraAccess>();

        var tagCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tagCube.name = "Tag Cube Template";
        tagCube.transform.localScale = Vector3.one * 0.1f;
        UnityEngine.Object.DestroyImmediate(tagCube.GetComponent<Collider>());
        tagCube.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TagCube.mat");

        // A thin white post standing on the fitted origin: visible even when a
        // per-tag cube sits in the same spot.
        var fitMarker = new GameObject("Fit Marker").transform;
        var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = "Post";
        post.transform.SetParent(fitMarker, false);
        post.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        post.transform.localScale = new Vector3(0.02f, 0.6f, 0.02f);
        UnityEngine.Object.DestroyImmediate(post.GetComponent<Collider>());
        post.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FitMarker.mat");

        var status = new GameObject("Status Text").AddComponent<TextMeshPro>();
        status.fontSize = 0.4f;
        status.alignment = TextAlignmentOptions.TopLeft;
        status.rectTransform.sizeDelta = new Vector2(1.2f, 0.6f);
        status.color = Color.white;
        status.outlineWidth = 0.2f;
        status.outlineColor = Color.black;

        var app = new GameObject("AprilTags");
        var source = app.AddComponent<PassthroughCameraSource>();
        SetField(source, "cameraAccess", cameraAccess);
        var localizer = app.AddComponent<AprilTagRoomLocalizer>();
        SetField(localizer, "cameraSource", source);
        var demo = app.AddComponent<AprilTagDemo>();
        SetField(demo, "localizer", localizer);
        SetField(demo, "tagCubeTemplate", tagCube.GetComponent<Renderer>());
        SetField(demo, "fitMarker", fitMarker);
        var demoSo = new SerializedObject(demo);
        demoSo.FindProperty("showScreenUi").boolValue = false;
        demoSo.ApplyModifiedPropertiesWithoutUndo();
        var input = app.AddComponent<QuestDemoInput>();
        SetField(input, "demo", demo);
        SetField(input, "statusText", status);
        SetField(input, "head", centerEye);

        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[SharedARQuestSetup] Created {path}");
    }

    private static void SetField(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
