$ErrorActionPreference = 'Stop'

function Resolve-DotNet {
  $candidates = @(
    (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'),
    (Join-Path (Get-Location) '.dotnet\dotnet.exe')
  )
  foreach ($path in $candidates) {
    if (Test-Path $path) { return $path }
  }
  $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }
  throw 'dotnet SDK not found. Install .NET 9 or adjust PATH.'
}

$dotnet = Resolve-DotNet
& $dotnet run -p 'src\DsvDownloader.Wpf' -c Release
exit $LASTEXITCODE
