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
TargetName=C:\Users\Calle\Dev\dsvvideodownloader\dist\VideoDownloader-Setup.exe
FriendlyName=Video Downloader Setup
AppLaunched=cmd /c start "" powershell -NoProfile -ExecutionPolicy Bypass -File install.ps1
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles

[Strings]
FILE1=D3DCompiler_47_cor3.dll
FILE2=DsvDownloader.Core.pdb
FILE3=DsvDownloader.Wpf.exe
FILE4=DsvDownloader.Wpf.pdb
FILE5=install.ps1
FILE6=PenImc_cor3.dll
FILE7=PresentationNative_cor3.dll
FILE8=vcruntime140_cor3.dll
FILE9=wpfgfx_cor3.dll

[SourceFiles]
SourceFiles0=C:\Users\Calle\Dev\dsvvideodownloader\setup

[SourceFiles0]
%FILE1%=
%FILE2%=
%FILE3%=
%FILE4%=
%FILE5%=
%FILE6%=
%FILE7%=
%FILE8%=
%FILE9%=
