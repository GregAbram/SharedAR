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

## Status

- **iPhone:** `Assets/Scenes/iPhone/iPhone AprilTags.unity` works on an
  iPhone 11. Tap **Acquire** and aim at a tag: a colored cube marks the room
  origin as solved from that tag. **Mode** switches between *Multi* (locks
  accumulate; with two or more, a white post marks the `RoomFit` best-fit
  origin, with per-tag residuals on screen) and *Single* (each Acquire forgets
  earlier locks).
- **Quest 3:** `Assets/Scenes/Quest/Quest AprilTags.unity`, the same demo on
  Meta's `OVRCameraRig` with passthrough and MRUK's passthrough camera access
  (the template's XR Origin isn't used: MRUK reports camera poses in Meta's
  tracking space, which is Unity world space only under `OVRCameraRig`).
  Controller buttons: **A** Acquire/Cancel, **B** Reset, **X** Mode; the status
  floats in front of you. Tested on a Quest 3.
- **PVLink scene:** not yet.
- `Assets/Scenes/SampleScene.unity` is the template's sample, kept for reference.

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

`Assets/StreamingAssets/room_config.json` lists the tags (id, room-frame
position, yaw, printed size); see the
[AprilTags package](https://github.com/GregAbram/AprilTags) README for the
format. A `room_config.json` in the app's Documents folder overrides the
bundled one. Tags must be from the **tagStandard41h12** family.

## Packages

`edu.tacc.apriltags`, the AprilTag detector (`edu.umn.cs.ivlab.apriltag`) and
`com.paraviewlink.unity` (PVLink's `ParaViewLinkUnity` folder) are pinned to
GitHub commits in `Packages/manifest.json`. To pick up package changes, push
them and update the `#commit` there.
