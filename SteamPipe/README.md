# String Theory SteamPipe Upload

This folder contains a SteamPipe upload helper. It generates the SteamPipe VDF under the Steamworks SDK ContentBuilder scripts folder, then runs SteamCMD.

Preview only:

```powershell
powershell -ExecutionPolicy Bypass -File .\SteamPipe\UploadStringTheoryBuild.ps1 `
  -AppId YOUR_APP_ID `
  -DepotId YOUR_WINDOWS_DEPOT_ID `
  -SteamUsername YOUR_STEAMWORKS_USERNAME `
  -BuildRoot "C:\Path\To\WindowsBuild" `
  -SdkRoot "C:\Path\To\steamworks_sdk\sdk" `
  -Preview
```

Real upload:

```powershell
powershell -ExecutionPolicy Bypass -File .\SteamPipe\UploadStringTheoryBuild.ps1 `
  -AppId YOUR_APP_ID `
  -DepotId YOUR_WINDOWS_DEPOT_ID `
  -SteamUsername YOUR_STEAMWORKS_USERNAME `
  -BuildRoot "C:\Path\To\WindowsBuild" `
  -SdkRoot "C:\Path\To\steamworks_sdk\sdk"
```

The batch launcher reads values from environment variables:

```bat
set STEAM_APP_ID=YOUR_APP_ID
set STEAM_DEPOT_ID=YOUR_WINDOWS_DEPOT_ID
set STEAMWORKS_USERNAME=YOUR_STEAMWORKS_USERNAME
set STRINGTHEORY_BUILD_ROOT=C:\Path\To\WindowsBuild
set STEAMWORKS_SDK_ROOT=C:\Path\To\steamworks_sdk\sdk
SteamPipe\RunStringTheoryUpload.bat
```

After upload, set the build live manually in Steamworks:

`App Admin -> SteamPipe -> Builds`

Use `StringTheory.exe` as the Windows launch executable.
