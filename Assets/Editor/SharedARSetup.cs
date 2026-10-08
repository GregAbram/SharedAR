using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARKit;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

// iPhone settings for this (Quest-first, Mixed Reality template) project.
// Re-runnable; it never modifies an existing scene. Menu: Tools > SharedAR >
// Configure iOS, or batchmode: -executeMethod SharedARSetup.ConfigureIOS
public static class SharedARSetup
{
    // Scenes built for iPhone; the first is the one the app starts in.
    public static readonly string[] IPhoneScenes =
    {
        "Assets/Scenes/iPhone/iPhone Room.unity",
        "Assets/Scenes/iPhone/iPhone AprilTags.unity",
    };

    [MenuItem("Tools/SharedAR/Configure iOS")]
    public static void ConfigureIOS()
    {
        ConfigurePlayer();
        ConfigureXR();
        CreateIPhoneRoomSceneIfMissing(IPhoneScenes[0]);
        CreateIPhoneSceneIfMissing(IPhoneScenes[1]);
        AssetDatabase.SaveAssets();
        Debug.Log("[SharedARSetup] iOS configured");
    }

    // Generates the Xcode project in Builds/iOS from the iPhone scenes. Batchmode:
    // -buildTarget iOS -executeMethod SharedARSetup.BuildIOS
    [MenuItem("Tools/SharedAR/Build iOS Xcode Project")]
    public static void BuildIOS()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = IPhoneScenes,
            locationPathName = "Builds/iOS",
            target = BuildTarget.iOS,
            options = BuildOptions.Development,
        });
        Debug.Log($"[SharedARSetup] iOS build {report.summary.result}: {report.summary.totalErrors} error(s)");
        if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }

    private static void ConfigurePlayer()
    {
        PlayerSettings.companyName = "TACC";
        PlayerSettings.productName = "SharedAR";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "edu.utexas.tacc.sharedar");
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "edu.utexas.tacc.sharedar");
        PlayerSettings.iOS.cameraUsageDescription = "The camera is used to find AprilTags and track the room.";
        PlayerSettings.iOS.targetOSVersionString = "15.4";
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.iOS.appleDeveloperTeamID = "MQSWMVW9A8";

        // ARKit's CPU image is landscape with the home button / USB port on the
        // right; locking the screen there makes the camera roll correction zero.
        // (Ignored on Quest.)
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

        // The template's packages make ~1 GB of IL2CPP C++; generating less of it
        // cuts Xcode build time a lot at little runtime cost for this app.
        // Medium stripping removes unused managed code first; packages that use
        // reflection (XRI, Input System) ship link.xml rules for it.
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.iOS, Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Medium);

        ARKitSettings.GetOrCreateSettings().requirement = ARKitSettings.Requirement.Required;
    }

    private static void ConfigureXR()
    {
        var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
        var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.iOS);
        if (general == null)
        {
            general = ScriptableObject.CreateInstance<XRGeneralSettings>();
            general.name = "iPhone Settings";
            var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
            manager.name = "iPhone Providers";
            general.Manager = manager;
            AssetDatabase.AddObjectToAsset(general, perTarget);
            AssetDatabase.AddObjectToAsset(manager, perTarget);
            perTarget.SetSettingsForBuildTarget(BuildTargetGroup.iOS, general);
        }
        general.InitManagerOnStart = true;

        // Without these the loader is never initialized at startup and
        // AR Foundation reports no session subsystem.
        general.Manager.automaticLoading = true;
        general.Manager.automaticRunning = true;
        EditorUtility.SetDirty(general.Manager);

        if (!XRPackageMetadataStore.IsLoaderAssigned("UnityEngine.XR.ARKit.ARKitLoader", BuildTargetGroup.iOS))
        {
            XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.ARKit.ARKitLoader", BuildTargetGroup.iOS);
        }
        EditorUtility.SetDirty(perTarget);
        EditorUtility.SetDirty(general);

        // ARKit compiles its real implementation only with this define. The
        // ARKit package adds it from an editor coroutine, which never gets to
        // run in a batchmode setup-then-quit, leaving a build with stubs only
        // ("Failed to load session subsystem").
        var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS);
        if (!defines.Contains("UNITY_XR_ARKIT_LOADER_ENABLED"))
        {
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, string.IsNullOrEmpty(defines)
                ? "UNITY_XR_ARKIT_LOADER_ENABLED"
                : defines + ";UNITY_XR_ARKIT_LOADER_ENABLED");
        }
    }

    // A starting point only: AR Foundation's own AR Session and XR Origin
    // (Mobile AR). Once it exists the scene is edited by hand like any other.
    private static void CreateIPhoneSceneIfMissing(string path)
    {
        if (File.Exists(path))
        {
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!EditorApplication.ExecuteMenuItem("GameObject/XR/AR Session")
            || !EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (Mobile AR)"))
        {
            Debug.LogError("[SharedARSetup] AR Foundation's GameObject > XR menu items were not found; scene not created");
            return;
        }
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[SharedARSetup] Created {path}");
    }

    // A starting point only; once it exists the scene is edited by hand.
    // RoomAnchor with background scanning, as in the Quest Room scene: a post at
    // the room origin, markers on placed tags, on-screen controls and status.
    private static void CreateIPhoneRoomSceneIfMissing(string path)
    {
        if (File.Exists(path))
        {
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!EditorApplication.ExecuteMenuItem("GameObject/XR/AR Session")
            || !EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (Mobile AR)"))
        {
            Debug.LogError("[SharedARSetup] AR Foundation's GameObject > XR menu items were not found; scene not created");
            return;
        }
        var cameraManager = Object.FindAnyObjectByType<UnityEngine.XR.ARFoundation.ARCameraManager>();

        var app = new GameObject("AprilTags");
        var source = app.AddComponent<ARFoundationCameraSource>();
        SetField(source, "cameraManager", cameraManager);
        var localizer = app.AddComponent<AprilTagRoomLocalizer>();
        SetField(localizer, "cameraSource", source);

        var room = new GameObject("Room");
        var anchor = room.AddComponent<RoomAnchor>();
        SetField(anchor, "localizer", localizer);

        // A thin white post standing on the room origin.
        var origin = new GameObject("Origin");
        origin.transform.SetParent(room.transform, false);
        var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = "Post";
        post.transform.SetParent(origin.transform, false);
        post.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        post.transform.localScale = new Vector3(0.02f, 0.6f, 0.02f);
        Object.DestroyImmediate(post.GetComponent<Collider>());
        post.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FitMarker.mat");

        // Markers on placed tags; the template stays outside the room, whose
        // children RoomAnchor hides and shows.
        var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
        template.name = "Tag Marker Template";
        template.transform.localScale = Vector3.one * 0.04f;
        Object.DestroyImmediate(template.GetComponent<Collider>());
        template.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TagCube.mat");
        var markers = room.AddComponent<RoomTagMarkers>();
        SetField(markers, "markerTemplate", template.GetComponent<Renderer>());

        var ui = new GameObject("UI").AddComponent<PhoneRoomUI>();
        SetField(ui, "roomAnchor", anchor);

        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[SharedARSetup] Created {path}");
    }

    private static void SetField(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
