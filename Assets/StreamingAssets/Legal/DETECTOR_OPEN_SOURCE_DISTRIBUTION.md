## Detector Open-Source Distribution Notes

This repository currently ships a native notes detector DLL that is loaded by Unity from:

- [Assets/Plugins/x86_64/NativeNotesDetectorBridgeNative_v6.dll](Assets/Plugins/x86_64/NativeNotesDetectorBridgeNative_v6.dll)

The detector bridge code lives in:

- [NativeNotesDetectorBridge/NativeNotesDetectorBridge.cpp](NativeNotesDetectorBridge/NativeNotesDetectorBridge.cpp)
- [NativeNotesDetectorBridge/NativeNotesDetectorBridge.vcxproj](NativeNotesDetectorBridge/NativeNotesDetectorBridge.vcxproj)

The detector build currently vendors aubio source from:

- [External/aubio](External/aubio)

## Why the detector source is included

The current detector DLL is built with vendored aubio source. Because of that, the conservative release posture is to keep the detector-side corresponding source available together with the distributed binary.

## Current repo posture

- The full upstream aubio repository clone has been trimmed down to the subset needed to rebuild the detector and keep the required aubio legal files.
- Old detector plugin versions and debug/import artifacts are not needed for runtime and should not be shipped.
- The active Unity bridge loads `NativeNotesDetectorBridgeNative_v6`.
- The packaged build now includes legal/notices files under `Assets/StreamingAssets/Legal`, and bootstrap mirrors them to `Application.persistentDataPath/Licenses` on startup for end-user access.

## Steam note

No Steamworks SDK integration was detected in this repository during the latest engineering review. If Steamworks SDK is added later, re-review licensing before shipping on Steam.

## Not legal advice

This file is a technical release-preparation note and not legal advice.
