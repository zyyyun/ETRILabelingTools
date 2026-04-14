; ============================================================
; AOLT IFEZ Demo Setup Script
; ============================================================

[Setup]
AppId={{E7B3F1A2-4D5C-4E6F-8A9B-1C2D3E4F5A6B}

; 프로그램 정보
AppName=AOLT IFEZ Demo
AppVersion=1.0
AppPublisher=주식회사 애나

; URL 정보 추가
AppPublisherURL=http://theannacompany.com/
AppSupportURL=http://theannacompany.com/
AppUpdatesURL=http://theannacompany.com/

; 설치 경로
DefaultDirName={autopf}\AOLT IFEZ Demo
DefaultGroupName=AOLT IFEZ Demo

; 출력 파일
OutputDir=output
OutputBaseFilename=AOLT_IFEZ_Demo_Setup

; 64비트 설정
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; 압축 설정
Compression=lzma2
SolidCompression=yes

; 관리자 권한
PrivilegesRequired=admin

; UI
WizardStyle=modern

; 제거 아이콘
UninstallDisplayIcon={app}\WinFormsApp1.exe


[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 생성"; GroupDescription: "추가 옵션:"


[Files]
; app 폴더 전체 복사 (핵심)
Source: "app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs


[Icons]
Name: "{group}\AOLT IFEZ Demo"; Filename: "{app}\WinFormsApp1.exe"
Name: "{group}\AOLT IFEZ Demo 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\AOLT IFEZ Demo"; Filename: "{app}\WinFormsApp1.exe"; Tasks: desktopicon


[Run]
Filename: "{app}\WinFormsApp1.exe"; \
    Description: "AOLT IFEZ Demo 실행"; \
    Flags: nowait postinstall skipifsilent