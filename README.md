# SharedAR

Shared mixed reality for Meta Quest and iPhone: each device localizes itself in
the room from wall-mounted AprilTags, so everyone sees the same content in the
same place. Content is meant to stream in from ParaView through
[PVLink](https://github.com/GregAbram/PVLink) rather than being built into the
app.

Unity 6000.3.10f1, started from the Mixed Reality template (URP, OpenXR, AR
Foundation 6.5), with the Apple ARKit plugin for iPhone and Meta XR Core SDK +
MR Utility Kit 207 for Quest. **The editor needs Android Build Support
installed on every machine, Mac included**: the Meta SDK's editor code doesn't
compile without it.

## Scenes

The localization is the [AprilTags package](https://github.com/GregAbram/AprilTags)
(`edu.tacc.apriltags` 0.3.0) - see its README for how it works, setting up a
room, and accuracy. This app is a thin shell around it.

| Scene | Quest 3 | iPhone | What it does |
|---|---|---|---|
| **Room** (startup) | `Quest/Quest Room` | `iPhone/iPhone Room` | `RoomAnchor` anchored by fitting two or more tags' positions; content (the magenta reference post, tag cubes) appears once anchored |
| **Survey** | `Quest/Quest Survey` | `iPhone/iPhone Survey` | Sets a room up: anchor on the configured tag, learn every other tag, save a new `room_config.json` |
| **AprilTags** | `Quest/Quest AprilTags` | `iPhone/iPhone AprilTags` | The original Acquire demo, for diagnostics |
| `SampleScene` | | | The Mixed Reality template's sample, kept for reference |

Controls - Quest: **A** scan now, **B** re-anchor, **X** forget learned / start
over, **right trigger** save (survey), **Y** next scene (Room → Survey →
AprilTags). iPhone: the same as on-screen buttons, **Scene** to switch.

The magenta post stands on the floor below the center of the surveyed tags:
mark where one device shows it and compare another device's.

## Setting up a room

1. Hang tagStandard41h12 tags around the room; list one of them (roughly
   measured, `"measured": true`) in `Assets/StreamingAssets/room_config.json`
   with `learnUnlistedTags` and `defaultTagSizeMeters`.
2. On the Quest: Room → **Y** → Survey → **X**; walk within ~1 m of the listed
   tag until anchored; look at every other tag until each shows *learned*;
   **right trigger** to save. The Quest uses the result from its next start.
3. Copy the saved file into the project so every build carries it:
   `adb pull /sdcard/Android/data/edu.utexas.tacc.sharedar/files/room_config.json Assets/StreamingAssets/`
   then rebuild both apps.

The current `room_config.json` is the Quest survey of 2026-10-09 (tags 8, 3,
7, 9). Survey on the Quest rather than the iPhone: the iPhone's single-tag
orientation varies 1-2.6 deg by viewpoint, and a survey inherits it.

**Logs:** each app writes `sharedar.log` (and `sharedar.prev.log`) to its
data folder - Quest: `adb pull /sdcard/Android/data/edu.utexas.tacc.sharedar/files/sharedar.log`;
iPhone: `xcrun devicectl device copy from --device <id> --domain-type
appDataContainer --domain-identifier edu.utexas.tacc.sharedar --source
Documents/sharedar.log --destination .` (also visible in Files/Finder).

## Build and run on iPhone

1. Open the project in Unity and run **Tools → SharedAR → Configure iOS**. It
   applies the iOS player and XR settings and never modifies existing scenes.
   Then run **Tools → SharedAR → Build iOS Xcode Project**, which writes
   `Builds/iOS` from the iPhone scenes listed in `SharedARSetup.IPhoneScenes`.
   Batchmode equivalents: `-buildTarget iOS -executeMethod
   SharedARSetup.ConfigureIOS` and `... SharedARSetup.BuildIOS`.
2. **Close the project in Xcode before building from Unity.** Unity tries to
   close an open Xcode project before overwriting it, and that crashes Unity
   (`-[SBApplication workspaceDocuments]: unrecognized selector`).
3. Open `Builds/iOS/Unity-iPhone.xcodeproj` in Xcode, choose your phone and
   press Run. The phone needs Developer Mode on, and with a free Apple ID you
   must trust the developer under Settings → General → VPN & Device Management
   (again whenever the provisioning profile is renewed).
4. Hold the phone in landscape with the charging port on the right.

The first Xcode build compiles a lot of IL2CPP C++; later builds into the same
`Builds/iOS` are incremental. iOS uses "Faster (smaller) builds" IL2CPP code
generation and Medium managed stripping; a `MissingMethodException` at runtime
would point at stripping.

If Xcode reports "The tunnel connection failed while the system tried to
connect to the device", restart the phone. A VPN on the Mac can also cause it.

**Signing:** `Assets/Editor/SharedARSetup.cs` sets the Apple team ID
(`appleDeveloperTeamID`). Change it to your own team, or pick your team in
Xcode after each build.

## Build and run on Quest

1. On a machine with Android Build Support, switch to Android and run
   **Tools → SharedAR → Configure Quest** (Android player settings, OpenXR +
   Meta XR feature, passthrough, Meta's Project Setup Tool fixes; creates the
   Quest scene only if missing). Then **Tools → SharedAR → Build Quest APK**
   (`Builds/Quest/SharedAR.apk`), or Build And Run, which uses the Quest scene
   list. Batchmode: `-buildTarget Android -executeMethod
   SharedARQuestSetup.ConfigureQuest` / `... SharedARQuestSetup.BuildQuest`.
2. Install with `adb install -r Builds/Quest/SharedAR.apk`; it appears under
   Library → Unknown sources. The headset camera permission
   (`horizonos.permission.HEADSET_CAMERA`, in
   `Assets/Plugins/Android/AndroidManifest.xml`) is requested at startup, or
   grant it with `adb shell pm grant edu.utexas.tacc.sharedar
   horizonos.permission.HEADSET_CAMERA`.

Meta XR Core SDK 203 breaks iOS player builds (`#define` after code in
`RuntimeOptimizerPlugin.cs`); 207 fixes it.

## Room config

`Assets/StreamingAssets/room_config.json`; see the AprilTags package README
for the format. A `room_config.json` in the app's data folder (written by a
survey) overrides the bundled one; delete it to revert.

## Packages

`edu.tacc.apriltags`, the AprilTag detector (`edu.umn.cs.ivlab.apriltag`) and
`com.paraviewlink.unity` (PVLink's `ParaViewLinkUnity` folder) are pinned to
GitHub commits in `Packages/manifest.json`. To pick up package changes, push
them and update the `#commit` there.
