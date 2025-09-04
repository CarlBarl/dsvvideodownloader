$ErrorActionPreference = 'Stop'

function Resolve-DotNet {
  # Prefer system-installed SDK
  $candidates = @(
    (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe')
  )
  foreach ($p in $candidates) { if (Test-Path $p) { return $p } }
  $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }

  # Fallback: install local SDK (9.0) into repo .dotnet
  Write-Host 'No .NET SDK found. Installing local .NET 9 SDK...' -ForegroundColor Yellow
  $repoRoot = Split-Path $PSScriptRoot -Parent
  $localDir = Join-Path $repoRoot '.dotnet'
  New-Item -ItemType Directory -Force -Path $localDir | Out-Null
  $dl = Join-Path $env:TEMP 'dotnet-install.ps1'
  Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $dl
  & powershell -NoProfile -ExecutionPolicy Bypass -File $dl -Channel 9.0 -Quality GA -InstallDir $localDir -Architecture x64
  $local = Join-Path $localDir 'dotnet.exe'
  if (-not (Test-Path $local)) { throw 'Local .NET install failed.' }
  return $local
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

# Publish self-contained single-file
$publishDir = Join-Path $repoRoot 'publish\gui'
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
& $dotnet publish (Join-Path $repoRoot 'src\DsvDownloader.Wpf') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

# Copy EXE to dev folder under repo for convenience
$devDir = Join-Path $repoRoot 'dev'
New-Item -ItemType Directory -Force -Path $devDir | Out-Null
$publishedExe = Get-ChildItem -Path $publishDir -Filter 'DsvDownloader.Wpf.exe' -File | Select-Object -First 1
if ($null -eq $publishedExe) { throw 'Published EXE not found.' }
Copy-Item $publishedExe.FullName -Destination (Join-Path $devDir $publishedExe.Name) -Force

# Install to LocalAppData\Video Downloader
$installDir = Join-Path $env:LocalAppData 'Video Downloader'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item $publishedExe.FullName -Destination (Join-Path $installDir $publishedExe.Name) -Force

$exe = Join-Path $installDir 'DsvDownloader.Wpf.exe'
if (-not (Test-Path $exe)) { throw 'Install failed: EXE not found after copy.' }

# Shortcuts
$startMenuDir = Join-Path $env:AppData 'Microsoft\Windows\Start Menu\Programs'
$lnk = Join-Path $startMenuDir 'Video Downloader.lnk'
New-Shortcut -Path $lnk -Target $exe -Icon $exe -WorkingDirectory $installDir

$desktop = [Environment]::GetFolderPath('Desktop')
$deskLnk = Join-Path $desktop 'Video Downloader.lnk'
New-Shortcut -Path $deskLnk -Target $exe -Icon $exe -WorkingDirectory $installDir

Write-Host ("Published EXE: {0}" -f $publishedExe.FullName) -ForegroundColor Green
Write-Host ("Dev copy: {0}" -f (Join-Path $devDir $publishedExe.Name)) -ForegroundColor Green
Write-Host ("Installed to: {0}" -f $installDir) -ForegroundColor Green
Write-Host ("Start Menu shortcut: {0}" -f $lnk) -ForegroundColor Green
Write-Host ("Desktop shortcut: {0}" -f $deskLnk) -ForegroundColor Green

# Launch
Start-Process -FilePath $exe
Write-Host 'Launched Video Downloader.' -ForegroundColor Green

