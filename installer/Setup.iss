; Single-file installer for the Copilot Game Bar Bridge widget.
; Built by .github/workflows/release.yml with:
;   ISCC /DAppVersion=1.2.3.0 /DPackageDir=<sideload package folder> /DOutputDir=<dist> installer\Setup.iss
; It trusts the package's signing certificate, then installs the .msix and its
; framework dependencies for the user who ran the installer.

#ifndef AppVersion
  #define AppVersion "0.0.0.0"
#endif

[Setup]
AppId={{7C1E3A52-4F0B-4E7B-9C51-2B8E0D3C6A11}
AppName=Copilot Game Bar Bridge
AppVersion={#AppVersion}
AppPublisher=Copilot Game Bar Bridge
AppPublisherURL=https://github.com/pablo906/XBox-Copilot-GameBar
OutputDir={#OutputDir}
OutputBaseFilename=CopilotGameBarBridge-Setup
; Trusting the certificate machine-wide needs admin rights.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
; Nothing is installed to Program Files: Windows owns the app once the package
; is added, and it is removed from Settings > Apps.
CreateAppDir=no
Uninstallable=no
DisableReadyPage=yes
DisableFinishedPage=no
WizardStyle=modern
Compression=lzma2
SolidCompression=yes

[Messages]
FinishedLabelNoIcons=Copilot Bridge is installed.%n%nPress Win+G, open the Widget menu, and choose Copilot Bridge.

[Files]
Source: "{#PackageDir}\*.msix"; DestDir: "{tmp}\pkg"; Flags: ignoreversion
Source: "{#PackageDir}\*.cer"; DestDir: "{tmp}"; DestName: "signing.cer"; Flags: ignoreversion
Source: "{#PackageDir}\Dependencies\x64\*.appx"; DestDir: "{tmp}\pkg\deps"; Flags: ignoreversion
Source: "Install-Package.ps1"; DestDir: "{tmp}"; Flags: ignoreversion

[Code]
var
  InstallError: String;

procedure Fail(const Msg: String);
begin
  InstallError := Msg;
  MsgBox(Msg, mbCriticalError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
begin
  if CurStep <> ssPostInstall then
    Exit;

  WizardForm.StatusLabel.Caption := 'Trusting the app''s signing certificate...';
  if not Exec(ExpandConstant('{sys}\certutil.exe'),
      '-f -addstore TrustedPeople "' + ExpandConstant('{tmp}\signing.cer') + '"',
      '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
  begin
    Fail('Could not trust the signing certificate (certutil exit code ' + IntToStr(Code) + ').');
    Exit;
  end;

  // Packages install per user, so run this as the person who started setup,
  // not as the elevated admin account.
  WizardForm.StatusLabel.Caption := 'Installing the widget...';
  if not ExecAsOriginalUser('powershell.exe',
      '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\Install-Package.ps1') +
      '" -PackageDir "' + ExpandConstant('{tmp}\pkg') + '"',
      '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
    Fail('The widget could not be installed. Details are in:' + #13#10 +
      '%TEMP%\CopilotGameBarBridge-install.log');
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpFinished) and (InstallError <> '') then
  begin
    WizardForm.FinishedHeadingLabel.Caption := 'Installation failed';
    WizardForm.FinishedLabel.Caption := InstallError;
  end;
end;
