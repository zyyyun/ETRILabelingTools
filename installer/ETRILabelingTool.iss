; ============================================================
; ETRI Labeling Tool - Inno Setup Script
; ============================================================
; Build steps:
;   1. dotnet build ETRILabelingTool.sln -c Release -p:Platform=x64
;   2. Put optional CUDA runtime files under installer\cudnn\
;   3. Put optional CUDA installer files under installer\cuda\
;   4. Compile with Inno Setup Compiler (iscc.exe)
; ============================================================

[Setup]
AppId={{E7B3F1A2-4D5C-4E6F-8A9B-1C2D3E4F5A6B}
AppName=ETRI Labeling Tool
AppVersion=1.1.1
AppPublisher=ETRI
DefaultDirName={autopf}\ETRILabelingTool
DefaultGroupName=ETRI Labeling Tool
OutputDir=output
OutputBaseFilename=ETRILabelingTool_Setup_1.1.1
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayIcon={app}\WinFormsApp1.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional options:"

[Files]
; Main app files
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\WinFormsApp1.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\WinFormsApp1.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\WinFormsApp1.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\WinFormsApp1.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\Clipper2Lib.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\FFMpegCore.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\Instances.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\Microsoft.Extensions.DependencyInjection.Abstractions.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\Microsoft.Extensions.DependencyInjection.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\Microsoft.ML.OnnxRuntime.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\Newtonsoft.Json.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\OpenCvSharp.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\OpenCvSharp.Extensions.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\SixLabors.Fonts.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\SixLabors.ImageSharp.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\SixLabors.ImageSharp.Drawing.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\System.Drawing.Common.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\System.Numerics.Tensors.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\YoloSharp.Gpu.dll"; DestDir: "{app}"; Flags: ignoreversion

; ONNX runtime root files
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\onnxruntime.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\onnxruntime_providers_cuda.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\onnxruntime_providers_shared.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\onnxruntime_providers_tensorrt.dll"; DestDir: "{app}"; Flags: ignoreversion

; YOLO model
Source: "..\WinFormsApp1\yolov8n.onnx"; DestDir: "{app}"; Flags: ignoreversion

; Native runtime files
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\runtimes\win-x64\native\OpenCvSharpExtern.dll"; DestDir: "{app}\runtimes\win-x64\native"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\runtimes\win-x64\native\opencv_videoio_ffmpeg4110_64.dll"; DestDir: "{app}\runtimes\win-x64\native"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\runtimes\win-x64\native\onnxruntime.dll"; DestDir: "{app}\runtimes\win-x64\native"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\runtimes\win-x64\native\onnxruntime_providers_cuda.dll"; DestDir: "{app}\runtimes\win-x64\native"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\runtimes\win-x64\native\onnxruntime_providers_shared.dll"; DestDir: "{app}\runtimes\win-x64\native"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\runtimes\win-x64\native\onnxruntime_providers_tensorrt.dll"; DestDir: "{app}\runtimes\win-x64\native"; Flags: ignoreversion

; Optional CUDA runtime DLLs copied next to the app when provided
Source: "cudnn\cublas64_12.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist; Check: ShouldInstallCuda
Source: "cudnn\cublasLt64_12.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist; Check: ShouldInstallCuda
Source: "cudnn\cudart64_12.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist; Check: ShouldInstallCuda
Source: "cudnn\cufft64_11.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist; Check: ShouldInstallCuda
Source: "cudnn\nvJitLink_120_0.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist; Check: ShouldInstallCuda
Source: "cudnn\nvrtc64_120_0.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist; Check: ShouldInstallCuda
Source: "cudnn\nvrtc-builtins64_124.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist; Check: ShouldInstallCuda

; Optional cuDNN DLLs
Source: "cudnn\cudnn64_9.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "cudnn\cudnn_adv64_9.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "cudnn\cudnn_cnn64_9.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "cudnn\cudnn_engines_precompiled64_9.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "cudnn\cudnn_engines_runtime_compiled64_9.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "cudnn\cudnn_graph64_9.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "cudnn\cudnn_heuristic64_9.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "cudnn\cudnn_ops64_9.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; Optional bundled CUDA installer contents
Source: "cuda\*"; DestDir: "{tmp}\cuda"; Flags: recursesubdirs nocompression deleteafterinstall skipifsourcedoesntexist

[Icons]
Name: "{group}\ETRI Labeling Tool"; Filename: "{app}\WinFormsApp1.exe"
Name: "{group}\Uninstall ETRI Labeling Tool"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ETRI Labeling Tool"; Filename: "{app}\WinFormsApp1.exe"; Tasks: desktopicon

[Run]
Filename: "{tmp}\cuda\setup.exe"; Parameters: "-s"; StatusMsg: "Installing NVIDIA CUDA Toolkit..."; Check: ShouldRunBundledCudaInstaller; Flags: waituntilterminated
Filename: "{app}\WinFormsApp1.exe"; Description: "Launch ETRI Labeling Tool"; Flags: nowait postinstall skipifsilent

[Code]
function ShouldInstallCuda(): Boolean;
var
  SubKeys: TArrayOfString;
  I: Integer;
  Value: String;
begin
  Result := True;

  if RegGetSubkeyNames(HKLM,
    'SOFTWARE\NVIDIA Corporation\GPU Computing Toolkit\CUDA',
    SubKeys) then
  begin
    for I := 0 to GetArrayLength(SubKeys) - 1 do
    begin
      if Pos('v12.', SubKeys[I]) = 1 then
      begin
        if RegQueryStringValue(HKLM,
          'SOFTWARE\NVIDIA Corporation\GPU Computing Toolkit\CUDA\' + SubKeys[I],
          '64BitInstalled', Value) then
        begin
          if Value = '1' then
          begin
            Result := False;
            Exit;
          end;
        end;
      end;
    end;
  end;
end;

function HasNvidiaGpu(): Boolean;
var
  SubKeys: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetSubkeyNames(HKLM, 'SYSTEM\CurrentControlSet\Enum\PCI', SubKeys) then
  begin
    for I := 0 to GetArrayLength(SubKeys) - 1 do
    begin
      if Pos('VEN_10DE', SubKeys[I]) > 0 then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;
end;

function ShouldRunBundledCudaInstaller(): Boolean;
begin
  Result := ShouldInstallCuda() and FileExists(ExpandConstant('{tmp}\cuda\setup.exe'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if ShouldInstallCuda() then
    begin
      if not HasNvidiaGpu() then
      begin
        MsgBox(
          'CUDA was not installed because no NVIDIA GPU was detected on this PC.' + #13#10 + #13#10 +
          'The application will continue in CPU mode.',
          mbInformation, MB_OK);
      end
      else if not FileExists(ExpandConstant('{tmp}\cuda\setup.exe')) then
      begin
        MsgBox(
          'A bundled CUDA installer was not found under installer\\cuda.' + #13#10 + #13#10 +
          'The application will continue in CPU mode unless CUDA is installed manually.',
          mbInformation, MB_OK);
      end;
    end;
  end;
end;
