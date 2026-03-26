; ============================================================
; ETRI Labeling Tool - Inno Setup Script
; ============================================================
; 빌드 전 준비사항:
;   1. dotnet build ETRILabelingTool.sln -c Release -p:Platform=x64
;   2. installer/cuda/   에 CUDA Toolkit 압축 해제 (setup.exe 포함)
;   3. installer/cudnn/  에 cuDNN 9.x DLL 배치
;   4. Inno Setup Compiler(iscc.exe)로 이 파일 컴파일
; ============================================================

; ------------------------------------------------------------
; [Setup] - 인스톨러 기본 설정
; ------------------------------------------------------------
[Setup]
; 고유 식별자 (GUID) - 업데이트/제거 시 동일 앱으로 인식
AppId={{E7B3F1A2-4D5C-4E6F-8A9B-1C2D3E4F5A6B}
AppName=ETRI Labeling Tool
AppVersion=1.0.0
AppPublisher=ETRI
DefaultDirName={autopf}\ETRILabelingTool
DefaultGroupName=ETRI Labeling Tool

; 인스톨러 출력 설정
OutputDir=output
OutputBaseFilename=AOLT_IFEZ_Demo_Setup

; x64 전용 (32비트 Windows에서 설치 차단)
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; 압축 설정 (lzma2 = 최고 압축률)
Compression=lzma2
SolidCompression=yes

; 관리자 권한 필요 (CUDA 설치 + Program Files 경로)
PrivilegesRequired=admin

; UI 스타일
WizardStyle=modern

; 아이콘 (WinFormsApp1.exe 내장 아이콘 사용)
UninstallDisplayIcon={app}\WinFormsApp1.exe

; ------------------------------------------------------------
; [Tasks] - 설치 시 선택 옵션
; ------------------------------------------------------------
[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 생성"; GroupDescription: "추가 옵션:"

; ------------------------------------------------------------
; [Files] - 설치할 파일 목록
; ------------------------------------------------------------
[Files]

; === 1. 메인 앱 파일 (Release 빌드 출력) ===
;   - *.exe, *.dll, *.json 을 {app} 폴더에 복사
;   - *.pdb (디버그 심볼)는 제외
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\WinFormsApp1.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\WinFormsApp1.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\WinFormsApp1.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\WinFormsApp1.deps.json"; DestDir: "{app}"; Flags: ignoreversion

; 의존 DLL (pdb, lib 제외)
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

; ONNX Runtime 네이티브 (루트 레벨)
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\onnxruntime.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\onnxruntime_providers_cuda.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\onnxruntime_providers_shared.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\onnxruntime_providers_tensorrt.dll"; DestDir: "{app}"; Flags: ignoreversion

; === 2. YOLO 모델 ===
Source: "..\WinFormsApp1\yolov8n.onnx"; DestDir: "{app}"; Flags: ignoreversion

; === 3. 네이티브 런타임 (win-x64만, .lib 제외) ===
;   - OpenCV, ONNX Runtime 네이티브 DLL
;   - linux-x64, win-x86 폴더는 불필요하므로 제외
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\runtimes\win-x64\native\OpenCvSharpExtern.dll"; DestDir: "{app}\runtimes\win-x64\native"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\runtimes\win-x64\native\opencv_videoio_ffmpeg4110_64.dll"; DestDir: "{app}\runtimes\win-x64\native"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\runtimes\win-x64\native\onnxruntime.dll"; DestDir: "{app}\runtimes\win-x64\native"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\runtimes\win-x64\native\onnxruntime_providers_cuda.dll"; DestDir: "{app}\runtimes\win-x64\native"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\runtimes\win-x64\native\onnxruntime_providers_shared.dll"; DestDir: "{app}\runtimes\win-x64\native"; Flags: ignoreversion
Source: "..\WinFormsApp1\bin\x64\Release\net8.0-windows\runtimes\win-x64\native\onnxruntime_providers_tensorrt.dll"; DestDir: "{app}\runtimes\win-x64\native"; Flags: ignoreversion

; === 4. cuDNN DLL ===
;   - {app} 폴더에 직접 배치
;   - CudaEnvironmentHelper가 AppContext.BaseDirectory를 먼저 탐색하므로
;     환경변수 설정 없이 앱이 바로 참조 가능
; CUDA Runtime DLL (CUDA Toolkit 없이도 동작하도록 앱 폴더에 번들)
Source: "cudnn\cublas64_12.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "cudnn\cublasLt64_12.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "cudnn\cudart64_12.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "cudnn\cufft64_11.dll"; DestDir: "{app}"; Flags: ignoreversion

; cuDNN DLL
Source: "cudnn\cudnn64_9.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "cudnn\cudnn_adv64_9.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "cudnn\cudnn_cnn64_9.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "cudnn\cudnn_engines_precompiled64_9.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "cudnn\cudnn_engines_runtime_compiled64_9.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "cudnn\cudnn_graph64_9.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "cudnn\cudnn_heuristic64_9.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "cudnn\cudnn_ops64_9.dll"; DestDir: "{app}"; Flags: ignoreversion

; === 5. CUDA Toolkit 인스톨러 ===
;   - 임시 폴더에 복사, 설치 후 자동 삭제
;   - nocompression: 이미 압축된 파일이므로 재압축 불필요
Source: "cuda\*"; DestDir: "{tmp}\cuda"; Flags: recursesubdirs nocompression deleteafterinstall

; ------------------------------------------------------------
; [Icons] - 바로가기 생성
; ------------------------------------------------------------
[Icons]
Name: "{group}\ETRI Labeling Tool"; Filename: "{app}\WinFormsApp1.exe"
Name: "{group}\ETRI Labeling Tool 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ETRI Labeling Tool"; Filename: "{app}\WinFormsApp1.exe"; Tasks: desktopicon

; ------------------------------------------------------------
; [Run] - 설치 완료 후 실행할 프로그램
; ------------------------------------------------------------
[Run]
; CUDA Toolkit silent 설치 (CUDA 미설치 시에만 실행)
;   -s = silent 모드 (UI 없이 자동 설치)
;   Check: ShouldInstallCuda = [Code] 섹션의 함수로 조건 판단
Filename: "{tmp}\cuda\setup.exe"; Parameters: "-s"; \
    StatusMsg: "NVIDIA CUDA Toolkit 설치 중... (수 분 소요될 수 있습니다)"; \
    Check: ShouldInstallCuda; Flags: waituntilterminated

; 설치 완료 후 앱 실행 옵션 (체크박스로 표시)
Filename: "{app}\WinFormsApp1.exe"; \
    Description: "ETRI Labeling Tool 실행"; \
    Flags: nowait postinstall skipifsilent

; ------------------------------------------------------------
; [Code] - Pascal Script (설치 로직)
; ------------------------------------------------------------
[Code]

// ============================================================
// ShouldInstallCuda()
// - 레지스트리에서 CUDA 12.x 설치 여부를 확인
// - 설치 안 되어 있으면 True 반환 → CUDA 인스톨러 실행
// - 이미 설치되어 있으면 False 반환 → 건너뜀
// ============================================================
function ShouldInstallCuda(): Boolean;
var
  SubKeys: TArrayOfString;
  I: Integer;
  Value: String;
begin
  Result := True;  // 기본값: CUDA 설치 필요

  // 레지스트리 경로: HKLM\SOFTWARE\NVIDIA Corporation\GPU Computing Toolkit\CUDA
  // 하위 키: v12.1, v12.4, v12.6 등
  if RegGetSubkeyNames(HKLM,
    'SOFTWARE\NVIDIA Corporation\GPU Computing Toolkit\CUDA',
    SubKeys) then
  begin
    for I := 0 to GetArrayLength(SubKeys) - 1 do
    begin
      // v12.x 로 시작하는 키가 있는지 확인
      if (Pos('v12.', SubKeys[I]) = 1) then
      begin
        // 64비트 설치가 완료되었는지 확인
        if RegQueryStringValue(HKLM,
          'SOFTWARE\NVIDIA Corporation\GPU Computing Toolkit\CUDA\' + SubKeys[I],
          '64BitInstalled', Value) then
        begin
          if Value = '1' then
          begin
            Result := False;  // CUDA 12.x 이미 설치됨 → 건너뜀
            Exit;
          end;
        end;
      end;
    end;
  end;
end;

// ============================================================
// CurStepChanged()
// - 설치 완료 후 CUDA 설치 결과를 검증
// - CUDA 설치를 시도했는데 실패한 경우 경고 표시
// ============================================================
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // CUDA 설치를 시도했는데 여전히 감지 안 되면 경고
    if ShouldInstallCuda() then
    begin
      MsgBox(
        'CUDA Toolkit 12.x 설치가 완료되지 않았을 수 있습니다.' + #13#10 +
        'GPU 가속이 작동하지 않을 수 있으며, YOLO 기능은 비활성화됩니다.' + #13#10 + #13#10 +
        'CUDA를 수동으로 설치하려면:' + #13#10 +
        'https://developer.nvidia.com/cuda-downloads',
        mbInformation, MB_OK);
    end;
  end;
end;
