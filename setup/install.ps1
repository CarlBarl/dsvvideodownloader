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
