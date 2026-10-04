# Copilot Game Bar Bridge

An Xbox Game Bar widget that runs the full Microsoft Copilot chat (copilot.microsoft.com) inside the widget window, so you can ask questions without leaving your game.

## How it works

- The chat is hosted in a WebView2 (WinUI 2) that fills the widget and resizes with it.
- Sign-in popups open as an overlay inside the widget instead of a separate window behind the game.
- The browser profile lives in the app's local data folder, so you stay signed in between sessions.
- A slim toolbar offers Back, Reload, New chat, and Open in the Copilot app.

The widget never reads Copilot credentials, tokens, cookies, or chat history; they stay inside the WebView2 profile.

## Prerequisites

- Windows 11
- Xbox Game Bar
- Visual Studio 2022 with:
  - Universal Windows Platform development workload
  - Windows 11 SDK 10.0.26100
- Windows Developer Mode enabled

## Build and install

1. Open `CopilotGameBarBridge.sln` in Visual Studio 2022.
2. Allow NuGet restore.
3. Select `Debug`, `x64`, and `Local Machine`.
4. Build and deploy with `Ctrl+F5`.
5. Press `Win+G`.
6. Open Widget Menu and choose `Copilot Bridge`.
7. Sign in to Copilot once inside the widget, then chat.

If the package identity publisher does not match your local development certificate, Visual Studio will offer to create/use a temporary development certificate during deployment.

## Next milestone

Add a user-initiated screenshot button that attaches the capture to the chat. Avoid automatic capture or game-memory access.
