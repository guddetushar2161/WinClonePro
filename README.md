# WinClone Pro

WinClone Pro is a Windows imaging and deployment tool for Windows 10 and Windows 11 capture, restore, and deployment workflows.

## Smooth Installation

1. Use the MSI at `WinClonePro.Setup\bin\Release\WinCloneProSetup.msi`.
2. Right-click the MSI and choose `Run as administrator`.
3. Complete the installer wizard and keep the default install location unless your environment requires a different path.
4. Launch `WinClone Pro` from the Start menu or desktop shortcut after setup finishes.

## Recommended Before First Launch

- Sign in with an administrator account.
- Keep an active internet connection available for the first run if Windows ADK is not already installed.
- Close other software installers, Windows Update installers, or package managers before opening WinClone Pro for the first time.
- Keep a few GB of free space available on the system drive for logs, downloads, and temporary working files.

## What Happens On First Launch

- WinClone Pro starts elevated.
- The app performs a system check after the main window opens.
- If required Windows ADK deployment components are missing, WinClone Pro downloads `adksetup.exe` to `C:\ProgramData\WinClonePro\downloads\`.
- The installer then attempts to add the required deployment tools automatically.

## Troubleshooting

- Application logs: `%AppData%\WinClonePro\logs`
- ADK download cache: `C:\ProgramData\WinClonePro\downloads`
- Extracted fallback tools: `C:\ProgramData\WinClonePro\tools`

If first-run preparation does not complete:

1. Close WinClone Pro.
2. Make sure no other installer is currently running.
3. Re-open WinClone Pro as administrator.
4. Check `%AppData%\WinClonePro\logs` for the latest `winclonepro-log-YYYYMMDD.log`.
5. If `adksetup.exe` already exists in `C:\ProgramData\WinClonePro\downloads\`, allow WinClone Pro to reuse it on the next run.

## Build Outputs

- MSI installer: `WinClonePro.Setup\bin\Release\WinCloneProSetup.msi`
- App publish folder: `WinClonePro.UI\bin\Release\net8.0-windows\win-x64\publish\`
