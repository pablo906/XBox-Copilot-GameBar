# Copilot Game Bar Bridge MVP

A minimal Xbox Game Bar widget that lets you compose a prompt in Win+G and open the installed unified Microsoft Copilot app. It copies the prompt to the clipboard by default and attempts to prefill Copilot.

## Important limitation

Microsoft documents the Game Bar SDK and Windows URI launching, but does not publish a supported API for third-party apps to send a prompt into the consumer unified Copilot app or submit it automatically. This MVP therefore:

1. Copies the prompt to the clipboard.
2. Tries the locally registered `ms-copilot:` URI.
3. Tries an observed query-prefill form (`ms-copilot:chat?q=...`) that is not a documented public contract.
4. Falls back to the Copilot web app if the installed app cannot be launched.

The widget never reads Copilot credentials, tokens, cookies, or chat history.

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
7. Type a prompt and select `Open Copilot`.

If the package identity publisher does not match your local development certificate, Visual Studio will offer to create/use a temporary development certificate during deployment.

## Next milestone

Add a user-initiated screenshot button and append the saved image path to the prompt workflow. Avoid automatic capture or game-memory access.
