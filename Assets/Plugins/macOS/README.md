macOS native plugins for Unity builds go here.

Expected runtime files:

- libNativeNotesDetectorBridgeNative_v6.dylib
- libStringTheoryToneHost.dylib
- libportaudio.dylib
- libonnxruntime.dylib
- libonnxruntime_providers_shared.dylib if required by the ONNX Runtime package used

Do not place Windows DLLs in this folder. Unity plugin importer settings should include this
folder only for macOS standalone builds once the dylibs are produced on a Mac build machine.

Recommended macOS command:

```bash
bash Tools/Mac/build-macos-native-plugins.sh
```
