$ErrorActionPreference = 'Stop'

function Resolve-DotNet {
  $candidates = @(
    (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'),
    (Join-Path (Get-Location) '.dotnet\dotnet.exe')
  )
  foreach ($path in $candidates) { if (Test-Path $path) { return $path } }
  $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }
  throw 'dotnet SDK not found. Install .NET 9 or adjust PATH.'
}

function New-Shortcut($Path, $Target, $Arguments = '', $Icon = $Target, $WorkingDirectory = $null) {
  $ws = New-Object -ComObject WScript.Shell
  $sc = $ws.CreateShortcut($Path)
  $sc.TargetPath = $Target
  if ($Arguments) { $sc.Arguments = $Arguments }
  $sc.IconLocation = $Icon
  if ($WorkingDirectory) { $sc.WorkingDirectory = $WorkingDirectory }
  $sc.Save()
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$dotnet = Resolve-DotNet

# 1) Publish self-contained single-file GUI
$publishDir = Join-Path $repoRoot 'publish\gui'
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
& $dotnet publish (Join-Path $repoRoot 'src\DsvDownloader.Wpf') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

# 2) Stage setup payload (installer runner + files)
$setupDir = Join-Path $repoRoot 'setup'
if (Test-Path $setupDir) { Remove-Item -Recurse -Force $setupDir }
New-Item -ItemType Directory -Force -Path $setupDir | Out-Null
Copy-Item -Path (Join-Path $publishDir '*') -Destination $setupDir -Recurse -Force

$installPs1 = @'
$ErrorActionPreference = "Stop"

function New-Shortcut($Path, $Target, $Arguments = '', $Icon = $Target, $WorkingDirectory = $null) {
  $ws = New-Object -ComObject WScript.Shell
  $sc = $ws.CreateShortcut($Path)
  $sc.TargetPath = $Target
  if ($Arguments) { $sc.Arguments = $Arguments }
  $sc.IconLocation = $Icon
  if ($WorkingDirectory) { $sc.WorkingDirectory = $WorkingDirectory }
  $sc.Save()
}

$srcDir = $PSScriptRoot
$targetDir = Join-Path $env:LocalAppData 'Video Downloader'
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
Get-ChildItem -Path $srcDir -File | ForEach-Object { Copy-Item $_.FullName -Destination (Join-Path $targetDir $_.Name) -Force }

$exe = Join-Path $targetDir 'DsvDownloader.Wpf.exe'
if (-not (Test-Path $exe)) { throw 'Install failed: EXE not found after copy.' }

$startMenuDir = Join-Path $env:AppData 'Microsoft\Windows\Start Menu\Programs'
$lnk = Join-Path $startMenuDir 'Video Downloader.lnk'
New-Shortcut -Path $lnk -Target $exe -Icon $exe -WorkingDirectory $targetDir

$desktop = [Environment]::GetFolderPath('Desktop')
$deskLnk = Join-Path $desktop 'Video Downloader.lnk'
New-Shortcut -Path $deskLnk -Target $exe -Icon $exe -WorkingDirectory $targetDir

Start-Process -FilePath $exe
'@
Set-Content -Path (Join-Path $setupDir 'install.ps1') -Value $installPs1 -Encoding UTF8

# 3) Create IExpress SED configuration
$distDir = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
$outExe = Join-Path $distDir 'VideoDownloader-Setup.exe'

$files = Get-ChildItem -Path $setupDir -File
$strings = @()
$refs = @()
$index = 0
foreach ($f in $files) {
  $index++
  $key = "FILE$index"
  $strings += "$key=$($f.Name)"
  $refs += "%$key%="
}

$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=0
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=I
InstallPrompt=
DisplayLicense=
FinishMessage=Installation completed.
TargetName=$outExe
FriendlyName=Video Downloader Setup
AppLaunched=cmd /c start "" powershell -NoProfile -ExecutionPolicy Bypass -File install.ps1
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles

[Strings]
$(($strings -join "`n"))

[SourceFiles]
SourceFiles0=$setupDir

[SourceFiles0]
$(($refs -join "`n"))
"@

Set-Content -Path (Join-Path $setupDir 'package.sed') -Value $sed -Encoding Ascii

# 4) Build installer EXE with IExpress
$iexpress = Join-Path $env:WINDIR 'System32\iexpress.exe'
if (-not (Test-Path $iexpress)) { throw 'iexpress.exe not found (Windows component missing).'; }
& $iexpress /N (Join-Path $setupDir 'package.sed')
if ($LASTEXITCODE -ne 0) { throw 'IExpress packaging failed.' }

Write-Host "Installer created: $outExe" -ForegroundColor Green
Write-Host 'Double-click the installer to install and launch the app.' -ForegroundColor Green




