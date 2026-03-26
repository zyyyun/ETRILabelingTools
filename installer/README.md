# ETRI Labeling Tool - Installer 준비 가이드

## 버전 요구사항

| 구성 요소 | 필요 버전 | 비고 |
|-----------|----------|------|
| CUDA Toolkit | **12.x** (12.4, 12.6 등 아무 마이너 버전) | 메이저 버전 12 내에서 하위 호환 |
| cuDNN | **9.x** (CUDA 12용) | cuDNN 8.x와 호환 안 됨 |
| .NET Desktop Runtime | **8.0** (x64) | 프레임워크 종속 앱 |

### 참고: ONNX Runtime 1.22.1이 참조하는 DLL

```
cublas64_12.dll      ← CUDA Toolkit에 포함
cublasLt64_12.dll    ← CUDA Toolkit에 포함
cudart64_12.dll      ← CUDA Toolkit에 포함
cufft64_11.dll       ← CUDA Toolkit에 포함
cudnn64_9.dll        ← cuDNN 별도 다운로드 필요
```

---

## 1. CUDA Toolkit 인스톨러 준비

### 다운로드
- https://developer.nvidia.com/cuda-downloads
- Windows > x86_64 > exe (local) 또는 exe (network) 선택
- 네트워크 버전(~30MB) 권장 (로컬 버전은 ~4GB)

### 배치
다운로드한 인스톨러를 `installer/cuda/` 폴더에 복사:
```
installer/cuda/cuda_12.6.3_561.17_windows.exe
```

### Inno Setup에서 Silent Install 실행
```ini
[Run]
; 전체 설치 (silent)
Filename: "{tmp}\cuda_installer.exe"; Parameters: "-s"; Flags: waituntilterminated

; 또는 필요한 컴포넌트만 설치
Filename: "{tmp}\cuda_installer.exe"; Parameters: "-s cuda_12.6 cublas_12.6 cudart_12.6 cufft_12.6"; Flags: waituntilterminated
```

---

## 2. cuDNN DLL 준비

### 다운로드
- https://developer.nvidia.com/cudnn-downloads
- cuDNN **9.x** for CUDA **12.x** 선택
- Windows > x86_64 > zip 다운로드

### 배치
zip 해제 후 DLL 파일을 `installer/cudnn/` 폴더에 복사:
```
installer/cudnn/cudnn64_9.dll           (필수)
installer/cudnn/cudnn_adv64_9.dll       (있으면 포함)
installer/cudnn/cudnn_cnn64_9.dll       (있으면 포함)
installer/cudnn/cudnn_ops64_9.dll       (있으면 포함)
installer/cudnn/cudnn_engines_precompiled64_9.dll  (있으면 포함)
installer/cudnn/cudnn_engines_runtime_compiled64_9.dll (있으면 포함)
installer/cudnn/cudnn_graph64_9.dll     (있으면 포함)
installer/cudnn/cudnn_heuristic64_9.dll (있으면 포함)
```

> cuDNN DLL은 Inno Setup에서 `{app}` (앱 설치 경로)에 복사합니다.
> `CudaEnvironmentHelper`가 `AppContext.BaseDirectory`를 첫 번째로 탐색하므로 환경변수 설정 없이 동작합니다.

---

## 3. Release 빌드

```bash
dotnet build ETRILabelingTool.sln -c Release -p:Platform=x64
```

빌드 출력 경로: `WinFormsApp1/bin/x64/Release/net8.0-windows/`

---

## 4. Inno Setup [Files] 섹션 경로 매핑

| 소스 경로 | Inno Setup 대상 | 설명 |
|-----------|----------------|------|
| `WinFormsApp1\bin\x64\Release\net8.0-windows\*.exe` | `{app}` | 메인 실행파일 |
| `WinFormsApp1\bin\x64\Release\net8.0-windows\*.dll` | `{app}` | 관리/네이티브 DLL |
| `WinFormsApp1\bin\x64\Release\net8.0-windows\*.json` | `{app}` | runtimeconfig, deps |
| `WinFormsApp1\yolov8n.onnx` | `{app}` | YOLO 모델 |
| `...\runtimes\win-x64\native\*.dll` | `{app}\runtimes\win-x64\native` | OpenCV, ONNX 네이티브 |
| `installer\cudnn\*.dll` | `{app}` | cuDNN (앱 폴더에 직접 배치) |
| `installer\cuda\cuda_installer.exe` | `{tmp}` | CUDA 인스톨러 (설치 후 삭제) |

### 제외할 파일
- `*.pdb` - 디버그 심볼
- `*.lib` - 개발용 라이브러리
- `runtimes\linux-x64\` - Linux용
- `runtimes\win-x86\` - 32비트용

---

## 5. CUDA 설치 여부 레지스트리 체크

Inno Setup [Code] 섹션에서 사용할 레지스트리 경로:

```
HKEY_LOCAL_MACHINE\SOFTWARE\NVIDIA Corporation\GPU Computing Toolkit\CUDA
```

- 하위 키: `v12.1`, `v12.4`, `v12.6` 등
- 확인 값: `64BitInstalled` = `"1"`
- `v12.` 로 시작하는 키가 하나라도 있으면 CUDA 설치됨으로 판단

---

## 폴더 구조 요약

```
installer/
├── README.md              ← 이 파일
├── cuda/
│   └── cuda_12.x.x_xxx.xx_windows.exe   ← 사용자가 직접 배치
├── cudnn/
│   ├── cudnn64_9.dll                     ← 사용자가 직접 배치
│   └── (기타 cuDNN DLL)
└── ETRILabelingTool.iss   ← Inno Setup 스크립트 (사용자가 작성)
```
