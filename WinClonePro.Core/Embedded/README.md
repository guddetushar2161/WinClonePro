WinClonePro embedded tools

Place optional fallback payloads in this folder before publishing.

Supported filenames:
- `dism.exe`
- `diskpart.exe`
- `bcdboot.exe`
- `copype.cmd`
- `MakeWinPEMedia.cmd`

Build behavior:
- Files under `Embedded\` are compiled as embedded resources.
- On startup, WinClone Pro extracts supported payloads to `%ProgramData%\WinClonePro\tools\`.
- System tools are always preferred over extracted tools.
- Extracted `.exe` payloads are signature-validated before use.

Repository note:
- This source tree does not ship Microsoft binaries by default.
- Add only payloads you are authorized to redistribute.
