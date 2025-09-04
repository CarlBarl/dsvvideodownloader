$ErrorActionPreference = 'Stop'

function Stop-App($name) {
  Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
    try { $_ | Stop-Process -Force -ErrorAction SilentlyContinue } catch {}
  }
}

$installDir = Join-Path $env:LocalAppData 'DSVDownloader'
Stop-App -name 'DsvDownloader.Wpf'

try {
  if (Test-Path $installDir) { Remove-Item -Recurse -Force $installDir }
} catch {}

$startMenuDir = Join-Path $env:AppData 'Microsoft\Windows\Start Menu\Programs'
$lnk = Join-Path $startMenuDir 'DSV Video Downloader.lnk'
if (Test-Path $lnk) { Remove-Item -Force $lnk }

$desktop = [Environment]::GetFolderPath('Desktop')
$deskLnk = Join-Path $desktop 'DSV Video Downloader.lnk'
if (Test-Path $deskLnk) { Remove-Item -Force $deskLnk }

Write-Host 'Uninstalled DSV Video Downloader.' -ForegroundColor Green
