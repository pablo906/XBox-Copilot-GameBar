# Copilot Game Bar Bridge

An Xbox Game Bar widget that runs the full Microsoft Copilot chat (copilot.microsoft.com) inside the widget window, so you can ask questions without leaving your game.

## How it works

- The chat is hosted in a WebView2 (WinUI 2) that fills the widget and resizes with it.
- Sign-in popups open as an overlay inside the widget instead of a separate window behind the game.
- The browser profile lives in the app's local data folder, so you stay signed in between sessions.
- A slim toolbar offers Back, Reload, New chat, and Open in the Copilot app.

The widget never reads Copilot credentials, tokens, cookies, or chat history; they stay inside the WebView2 profile.

## Install (no Visual Studio needed)

1. Open the [latest release](https://github.com/pablo906/XBox-Copilot-GameBar/releases/latest) and download `CopilotGameBarBridge-Setup.exe`.
2. Run it. If Windows SmartScreen says it protected your PC, choose **More info**, then **Run anyway**.
3. Approve the administrator prompt and click through the installer.
4. Press `Win+G`, open the Widget menu, and choose `Copilot Bridge`.
5. Sign in to Copilot once inside the widget, then chat.

The installer trusts the app's signing certificate on your PC and installs the frameworks the widget needs. The certificate is the project's own self-signed one rather than one from a certificate authority, which is also why SmartScreen warns about the installer. Only install it if you trust this repository.

To update, run the newer installer. To remove the widget, uninstall **Copilot Game Bar Bridge** from Settings > Apps.

Prefer not to run an installer? Each release also has a `-manual.zip`: extract it, right-click `Install.ps1`, and choose **Run with PowerShell**.

## Build from source

### Prerequisites

- Windows 11
- Xbox Game Bar
- Visual Studio 2022 with:
  - Universal Windows Platform development workload
  - Windows 11 SDK 10.0.26100
- Windows Developer Mode enabled

### Build and install

1. Open `CopilotGameBarBridge.sln` in Visual Studio 2022.
2. Allow NuGet restore.
3. Select `Debug`, `x64`, and `Local Machine`.
4. Build and deploy with `Ctrl+F5`.
5. Press `Win+G`.
6. Open Widget Menu and choose `Copilot Bridge`.
7. Sign in to Copilot once inside the widget, then chat.

If the package identity publisher does not match your local development certificate, Visual Studio will offer to create/use a temporary development certificate during deployment.

## Publishing a release

Pushing a version tag builds a signed package and `Setup.exe` (from `installer/Setup.iss`, using Inno Setup) on GitHub Actions and attaches them to a GitHub Release:

```powershell
git tag v0.2.0
git push origin v0.2.0
```

The workflow lives in `.github/workflows/release.yml`. Pull requests that touch the app also run it, so a broken build shows up before a release; those runs keep the package as a workflow artifact and publish nothing.

### Signing certificate (optional)

Windows only installs sideloaded packages signed by a certificate the PC trusts. The release build signs with a certificate stored in two repository secrets:

| Secret | Value |
| --- | --- |
| `SIGNING_CERT_PFX_BASE64` | The `.pfx` file, base64-encoded |
| `SIGNING_CERT_PASSWORD` | The `.pfx` password |

To create them, run this once on your PC from the repo folder:

```powershell
.\scripts\New-SigningCertificate.ps1 -Subject "CN=Your Name"
```

It saves the `.pfx` in your user folder (outside the repo) and copies the base64 text to your clipboard. Add both secrets under **Settings > Secrets and variables > Actions**. Keep the `.pfx` somewhere safe and reuse it for every release; users who trusted it once won't be asked again. Never commit the `.pfx`.

The workflow sets the package Publisher to the certificate's subject at build time, so the manifest in the repo doesn't need to change. If the secrets are missing, the build signs with a throwaway certificate instead. That still installs fine, because the installer trusts whichever certificate the release was signed with, but each release then adds another certificate to users' PCs.

## Next milestone

Add a user-initiated screenshot button that attaches the capture to the chat. Avoid automatic capture or game-memory access.
