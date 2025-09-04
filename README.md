DSV Video Downloader (Windows 11, WPF)
=====================================

A simple, polished Windows app to download DSV .mp4?token=… links reliably.

- Paste the full URL (ends with `.mp4?token=…`).
- Choose destination (defaults to `A:\DSV`).
- Click Download. See progress, speed, ETA. Cancel anytime.
- On success, Open and Show in Explorer appear. Optionally auto-open.

Version is shown in the UI (title and footer), e.g., `v3`.

Requirements
------------
- Windows 11 (22H2+)
- .NET SDK 9.0 installed (recommended), or use the full path to `dotnet.exe`.

Build and Run
-------------
From the repo root:

- Build: `& "$env:ProgramFiles\dotnet\dotnet.exe" build src\DsvDownloader.Wpf -c Release`
- Run: `& "$env:ProgramFiles\dotnet\dotnet.exe" run -p src\DsvDownloader.Wpf -c Release`

If `dotnet` is not recognized in your terminal:

- Use the full path as above; or add it for the session:
  - `$env:PATH = "$env:ProgramFiles\dotnet;$env:PATH"`
  - Verify: `Get-Command dotnet` and `dotnet --info`

Convenience scripts are in `scripts/` (use full path to PowerShell):

- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1`
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-gui.ps1`

How It Works
------------
- Validates URL shape and masks tokens in UI.
- Attempts download with HttpClient using minimal headers:
  - `Referer: https://play-store-prod.dsv.su.se/`
  - `User-Agent: Mozilla/5.0`
- If the server returns non-MP4 (e.g., HTML), automatically falls back to PowerShell:
  - `Invoke-WebRequest -Uri "<URL>" -OutFile "<path>" -Headers @{ Referer='https://play-store-prod.dsv.su.se/' } -UserAgent 'Mozilla/5.0'`
- Writes to a temporary `.part` file for HttpClient path; for PowerShell fallback it writes directly to the target path.

Creating/Getting a Download Link
--------------------------------
1) From the video page (preferred):
- Go to `https://play.dsv.su.se/presentation/<uuid>`.
- Use the page’s download link if present (right-click → Copy link). It should be an `.mp4?token=...` URL on a `*.play-store-prod.dsv.su.se` host.

2) From browser DevTools if no download button:
- Open DevTools → Network tab → Clear.
- Play/seek the video; filter for `.mp4`.
- Click the `.mp4` request → Headers → copy the full Request URL.
- Paste into the app promptly — tokens expire quickly. If you see 401/403 or HTML, refresh the page and grab a fresh link.

UI Tips
-------
- Clipboard banner: if the clipboard contains a valid DSV URL, click “Use URL”.
- Drag-and-drop: drop a URL string onto the window.
- “Open when done”: auto-opens the file after a successful download.

Troubleshooting
---------------
- `dotnet: not recognized`:
  - Use full path: `& "$env:ProgramFiles\dotnet\dotnet.exe" --info`
  - Add to PATH for the session: `$env:PATH = "$env:ProgramFiles\dotnet;$env:PATH"`
  - Ensure you’re in the repo root (where `src\...` exists) when running commands.
- Build fails with file lock on `DsvDownloader.Core.dll`:
  - Close any running `DsvDownloader.Wpf.exe` and retry build.
- Server returns HTML instead of MP4:
  - Token likely expired; fetch a new link from the presentation page.

Notes
-----
- This app does not include MCP integration; it’s a straightforward local GUI tool.
- Target framework: .NET 9.0 (`net9.0` for Core and MCP server; `net9.0-windows10.0.19041.0` for WPF).

Installer
---------
- One-click installer EXE (for sharing):
  - Build: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\make-setup.ps1`
  - Output: `dist\VideoDownloader-Setup.exe` — double-click to install + launch.
- Script installer (dev convenience): `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\install.ps1`
