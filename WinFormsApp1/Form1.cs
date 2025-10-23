using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Newtonsoft.Json;
using System.IO;
using OpenCvSharp.Tracking;
using Compunet.YoloSharp;
using System.CodeDom;  // YoloSharp 추가
using FFMpegCore;
using FFMpegCore.Enums;
using System.Diagnostics;

namespace WinFormsApp1
{
    public enum UndoActionType { AddBox, RemoveBox, ModifyBox, Tracking }

    public class UndoAction
    {
        public UndoActionType Type { get; set; }
        public BoundingBox Box { get; set; }
        public Rectangle OriginalRectangle { get; set; }
        public string OriginalLabel { get; set; }
        public int OriginalObjectId { get; set; }
        public List<BoundingBox> TrackedBoxes { get; set; }
    }

    public class WaypointMarker
    {
        public int EntryFrame { get; set; }
        public int ExitFrame { get; set; }
        public Color MarkerColor { get; set; }
        public string EntryTime { get; set; }
        public string ExitTime { get; set; }
        public int ObjectId { get; set; } // PersonId, VehicleId, EventId 중 하나
        public string Label { get; set; }
    }

    public class CustomLabel
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int Id { get; set; }
        public Panel Panel { get; set; }
        public Label Label { get; set; }
    }

    public class BoundingBox
    {
        public int FrameIndex { get; set; }
        public Rectangle Rectangle { get; set; }
        public string Label { get; set; }
        public int PersonId { get; set; }
        public int VehicleId { get; set; }
        public int EventId { get; set; }
        public string Action { get; set; }
        public string VehicleName { get; set; }
        public string EventName { get; set; }
    }

    public class SubtitleEntry
    {
        public int Index { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Text { get; set; }
    }

    #region JSON Serialization Classes

    public class ImageInfo
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("height")] public int Height { get; set; }
        [JsonProperty("width")] public int Width { get; set; }
        [JsonProperty("frame_number")] public int FrameNumber { get; set; }
        [JsonProperty("timestamp")] public string Timestamp { get; set; }
    }

    public class TrackEntry
    {
        [JsonProperty("frame")] public int Frame { get; set; }
        [JsonProperty("timestamp")] public string Timestamp { get; set; }
    }

    public class TrackInfo
    {
        [JsonProperty("entry")] public TrackEntry Entry { get; set; }
        [JsonProperty("exit")] public TrackEntry Exit { get; set; }
        [JsonProperty("current_clip_count")] public int CurrentClipCount { get; set; }
    }

    public class AnnotationData
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("image_id")] public int ImageId { get; set; }
        [JsonProperty("category_id")] public int CategoryId { get; set; }
        [JsonProperty("bbox")] public int[] Bbox { get; set; }
        [JsonProperty("area")] public int Area { get; set; }
        [JsonProperty("iscrowd")] public int Iscrowd { get; set; }
        [JsonProperty("track_id")] public int TrackId { get; set; }
        [JsonProperty("track_info")] public TrackInfo TrackInfo { get; set; }
    }

    public class CategoryData
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("supercategory")] public string Supercategory { get; set; }
    }

    public class VideoInfoExtended
    {
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("version")] public string Version { get; set; }
        [JsonProperty("year")] public int Year { get; set; }
        [JsonProperty("date_created")] public string DateCreated { get; set; }
        [JsonProperty("video_file")] public string VideoFile { get; set; }
    }

    public class LabelingDataExtended
    {
        [JsonProperty("info")] public VideoInfoExtended Info { get; set; }
        [JsonProperty("licenses")] public List<object> Licenses { get; set; }
        [JsonProperty("images")] public List<ImageInfo> Images { get; set; }
        [JsonProperty("annotations")] public List<AnnotationData> Annotations { get; set; }
        [JsonProperty("categories")] public List<CategoryData> Categories { get; set; }
    }

    #endregion

    public abstract class TrackingEngine
    {
        public abstract List<BoundingBox> TrackObjects(
            VideoCapture videoCapture,
            BoundingBox startBox,
            int startFrame,
            int endFrame,
            double fps);
    }

    public class YoloTrackingEngine : TrackingEngine
    {
        private YoloPredictor _predictor;
        private string _tempImagePath;

        public YoloTrackingEngine(string modelPath)
        {
            _predictor = new YoloPredictor(modelPath);
            
            // 임시 파일 경로 설정 및 디렉토리 확인
            string tempDir = Path.GetTempPath();
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            _tempImagePath = Path.Combine(tempDir, "yolo_frame.jpg");
        }

        public override List<BoundingBox> TrackObjects(
            VideoCapture videoCapture,
            BoundingBox startBox,
            int startFrame,
            int endFrame,
            double fps)
        {
            System.Diagnostics.Debug.WriteLine($"[YOLO] TrackObjects 호출 - Label: {startBox.Label}, PersonId: {startBox.PersonId}, VehicleId: {startBox.VehicleId}, EventId: {startBox.EventId}");
            System.Diagnostics.Debug.WriteLine($"[YOLO] Frame 범위: {startFrame} ~ {endFrame}");
            
            var trackedBoxes = new List<BoundingBox>();
            videoCapture.Set(VideoCaptureProperties.PosFrames, startFrame);
            Mat frame = new Mat();

            // 이전 프레임 박스 초기화: 사용자 지정 startBox로 시작
            Rectangle previousRect = startBox.Rectangle;
            string fixedLabel = startBox.Label;
            int fixedIdPerson = startBox.PersonId;
            int fixedIdVehicle = startBox.VehicleId;
            int fixedIdEvent = startBox.EventId;

            System.Diagnostics.Debug.WriteLine($"[YOLO] 추적 시작 박스: {previousRect}, Label: {fixedLabel}, PersonId: {fixedIdPerson}, VehicleId: {fixedIdVehicle}, EventId: {fixedIdEvent}");

            for (int i = startFrame; i <= endFrame; i++)
            {
                if (!videoCapture.Read(frame) || frame.Empty())
                    break;

                try
                {
                    // Mat을 임시 파일로 저장 (YoloSharp는 파일 입력을 요구)
                    Cv2.ImWrite(_tempImagePath, frame);

                    var detections = _predictor.Detect(_tempImagePath);
                    System.Diagnostics.Debug.WriteLine($"[YOLO] Frame {i}: {detections.Count}개 검출");

                    // 이전 박스와 IoU가 가장 큰 검출만 채택 (사용자 박스 기반 추적)
                    double bestIou = 0.0;
                    OpenCvSharp.Rect? best = null;
                    foreach (var d in detections)
                    {
                        var rect = new OpenCvSharp.Rect(
                            (int)d.Bounds.Left,
                            (int)d.Bounds.Top,
                            (int)d.Bounds.Width,
                            (int)d.Bounds.Height);

                        double iou = ComputeIoU(previousRect, new Rectangle(rect.X, rect.Y, rect.Width, rect.Height));
                        if (iou > bestIou)
                        {
                            bestIou = iou;
                            best = rect;
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[YOLO] Frame {i}: BestIoU = {bestIou:F3}, Best = {best?.ToString() ?? "null"}");

                    // 매칭이 없으면 이전 박스를 그대로 유지해 연속성 보장
                    var nextRect = previousRect;
                    if (best.HasValue)
                    {
                        nextRect = new Rectangle(best.Value.X, best.Value.Y, best.Value.Width, best.Value.Height);
                    }

                    var trackedBox = new BoundingBox
                    {
                        FrameIndex = i,
                        Rectangle = nextRect, // 좌표는 항상 이미지 픽셀 기준
                        Label = fixedLabel,
                        PersonId = fixedIdPerson,
                        VehicleId = fixedIdVehicle,
                        EventId = fixedIdEvent,
                        Action = "waypoint"
                    };

                    trackedBoxes.Add(trackedBox);
                    previousRect = nextRect;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"프레임 {i} 추론 오류: {ex.Message}");
                    continue;
                }
            }

            frame.Dispose();

            try
            {
                if (File.Exists(_tempImagePath))
                    File.Delete(_tempImagePath);
            }
            catch { }

            return trackedBoxes;
        }

        private static double ComputeIoU(Rectangle a, Rectangle b)
        {
            int x1 = Math.Max(a.Left, b.Left);
            int y1 = Math.Max(a.Top, b.Top);
            int x2 = Math.Min(a.Right, b.Right);
            int y2 = Math.Min(a.Bottom, b.Bottom);

            int interW = Math.Max(0, x2 - x1);
            int interH = Math.Max(0, y2 - y1);
            double inter = interW * interH;

            double areaA = a.Width * a.Height;
            double areaB = b.Width * b.Height;
            double union = areaA + areaB - inter;
            if (union <= 0) return 0;
            return inter / union;
        }

        public void Dispose()
        {
            _predictor?.Dispose();
        }
    }

    public partial class Form1 : Form
    {
        private VideoCapture videoCapture;
        private Mat currentFrame;
        private bool isPlaying = false;
        private int currentFrameIndex = 0;
        private int totalFrames = 0;
        private double fps = 30.0;
        private string currentVideoFile = "";

        private enum DrawMode { None, Select, Draw }
        private DrawMode currentMode = DrawMode.Select;

        private List<BoundingBox> boundingBoxes = new List<BoundingBox>();
        private BoundingBox selectedBox = null;
        private BoundingBox drawingBox = null;
        private System.Drawing.Point drawStartPoint;
        private bool isDrawing = false;
        private bool isDragging = false;
        private System.Drawing.Point dragOffset;

        private int? entryFrameIndex = null;
        private int? exitFrameIndex = null;

        private List<WaypointMarker> waypointMarkers = new List<WaypointMarker>();
        private Color[] markerColors = new Color[]
        {
            Color.FromArgb(59, 130, 246),
            Color.FromArgb(16, 185, 129),
            Color.FromArgb(139, 92, 246),
            Color.FromArgb(245, 158, 11),
            Color.FromArgb(236, 72, 153),
        };
        private int currentColorIndex = 0;

        private float timelineProgress = 0.25f;
        private bool isDarkMode = false;
        private double playbackSpeed = 1.0;
        private long lastFrameTime = 0;
        private double msPerFrame = 0;
        private bool isTimelineDragging = false;


        private Stack<UndoAction> undoStack = new Stack<UndoAction>();
        private Stack<UndoAction> redoStack = new Stack<UndoAction>();
        private const int MAX_UNDO_STACK = 50;

        private List<CustomLabel> customLabels = new List<CustomLabel>();
        private int customLabelYPosition = 185;

        private List<string> videoFileList = new List<string>();
        private int currentVideoIndex = 0;
        private Form videoListForm = null;
        private ListView videoListView = null;

        private Dictionary<int, CategoryData> categoryMap = new Dictionary<int, CategoryData>();
        private int nextAnnotationId = 1;
        
        // ID 관리 변수 제거 - 사용자가 수동으로 Ctrl+1~14로 지정
        private int currentAssignedId = 1; // 현재 할당할 ID (Ctrl+1~14로 변경됨)

        private TrackingEngine trackingEngine = null;  
        private bool isYoloAvailable = false;
        private string yoloModelPath = Path.Combine(Application.StartupPath, @"..\..\..\..\yolov8n.onnx");

        // SRT 자막 관련
        private string currentSrtFile = "";
        private List<SubtitleEntry> subtitleEntries = new List<SubtitleEntry>();
        private bool isFFmpegAvailable = false;
        private bool isSubtitleVisible = false; // 자막 표시 상태
        
        // 현재 선택된 라벨 (person, vehicle, event)
        private string currentSelectedLabel = "person";
        
        // bbox 리스트 렌더링 최적화를 위한 변수
        private int lastRenderedBoxCount = -1;

        // 카테고리 ID 매핑 (스펙에 따른 고정 매핑)
        private static readonly Dictionary<string, int> CategoryIdMap = new Dictionary<string, int>
        {
            // Person categories (1~14)
            {"person_01", 1}, {"person_02", 2}, {"person_03", 3}, {"person_04", 4},
            {"person_05", 5}, {"person_06", 6}, {"person_07", 7}, {"person_08", 8},
            {"person_09", 9}, {"person_10", 10}, {"person_11", 11}, {"person_12", 12},
            {"person_13", 13}, {"person_14", 14},
            
            // Vehicle categories (15~18)
            {"car", 15}, {"motorcycle", 16}, {"e_scooter", 17}, {"bicycle", 18},
            
            // Event categories (19~22)
            {"contact", 19}, {"exchange", 20}, {"board", 21}, {"final_exchange", 22}
        };


        public Form1()
        {
            InitializeComponent();
            UpdateBoxCount();
            
            // 자막 초기 상태를 닫힌 상태로 설정
            btnToggleSubtitle.Text = "자막 열기";
            btnToggleSubtitle.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
            if (labelSubtitleTimestamp != null)
            {
                labelSubtitleTimestamp.Visible = false;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Maximized;
            ApplyTheme();
            btnSelectFolder.Text = "파일 선택";
            
            // Form이 키 이벤트를 받을 수 있도록 포커스 설정
            this.Focus();
            this.Activate();

            // YOLO 모델 초기화 시도
            InitializeYoloModel();

            // FFmpeg 경로 설정
            SetupFFmpegPath();
        }


        private void SetupFFmpegPath()
        {
            try
            {
                // FFmpeg가 시스템 PATH에 있는지 확인
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            Arguments = "-version",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    process.WaitForExit(3000); // 3초 타임아웃

                    if (process.ExitCode == 0)
                    {
                        // FFmpeg가 PATH에 있음
                        GlobalFFOptions.Configure(new FFOptions { BinaryFolder = "" });
                        isFFmpegAvailable = true;
                        return;
                    }
                }
                catch
                {
                    // PATH에서 FFmpeg를 찾지 못함 - 로컬 폴더에서 찾기
                }

                // 로컬 폴더에서 FFmpeg 찾기
                string localFFmpegPath = Path.Combine(Application.StartupPath, "ffmpeg");
                string ffmpegExe = Path.Combine(localFFmpegPath, "ffmpeg.exe");
                
                if (File.Exists(ffmpegExe))
                {
                    GlobalFFOptions.Configure(new FFOptions { BinaryFolder = localFFmpegPath });
                    isFFmpegAvailable = true;
                }
                else
                {
                    // FFmpeg를 찾을 수 없음
                    isFFmpegAvailable = false;
                    MessageBox.Show(
                        "FFmpeg를 찾을 수 없습니다.\n\n" +
                        "비디오 내 자막 추출 기능을 사용하려면:\n" +
                        "1. FFmpeg를 다운로드 (https://ffmpeg.org/download.html)\n" +
                        "2. 시스템 PATH에 추가하거나\n" +
                        "3. 실행 파일과 같은 폴더에 'ffmpeg' 폴더를 만들고 ffmpeg.exe를 넣어주세요\n\n" +
                        "※ 외부 SRT 파일이 있으면 자동으로 로드됩니다.",
                        "FFmpeg 없음",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                isFFmpegAvailable = false;
                MessageBox.Show(
                    $"FFmpeg 설정 중 오류:\n{ex.Message}\n\n" +
                    "외부 SRT 파일만 사용 가능합니다.",
                    "FFmpeg 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void InitializeYoloModel()
        {
            try
            {
                if (!File.Exists(yoloModelPath))
                {
                    MessageBox.Show(
                        $"YOLO 모델을 찾을 수 없습니다: {yoloModelPath}\n\n" +
                        "yolov8n.onnx 파일을 실행 파일과 같은 폴더에 배치해주세요.",
                        "경고",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    isYoloAvailable = false;
                    return; // Application.Exit() 대신 return으로 변경
                }

                trackingEngine = new YoloTrackingEngine(yoloModelPath);
                isYoloAvailable = true;

                MessageBox.Show(
                    "YOLO 모델이 성공적으로 로드되었습니다.\n" +
                    "Ctrl+T로 추적을 시작할 때 YOLO 모드를 선택할 수 있습니다.",
                    "정보",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                if (errorMessage.Contains("Opset 22"))
                {
                    errorMessage += "\n\n해결 방법:\n" +
                                  "1. YOLOv8 모델을 Opset 21로 다시 변환하세요\n" +
                                  "2. Python: model.export(format='onnx', opset=21)\n" +
                                  "3. 또는 YOLO 기능 없이 계속 진행하세요";
                }
                
                MessageBox.Show(
                        $"YOLO 모델 로딩중 에러: {errorMessage}\n\n" +
                        "YOLO 기능 없이 계속 진행합니다.",
                        "경고",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                isYoloAvailable = false;
                // Application.Exit() 제거하여 프로그램이 계속 실행되도록 함
            }
        }


        #region Window Controls
        private void btnClose_Click(object sender, EventArgs e) => this.Close();
        private void btnMaximize_Click(object sender, EventArgs e)
        {
            this.WindowState = this.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }
        private void btnMinimize_Click(object sender, EventArgs e) => this.WindowState = FormWindowState.Minimized;

        private async void btnExportJson_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentVideoFile))
            {
                MessageBox.Show("먼저 비디오 파일을 로드해주세요.", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (boundingBoxes.Count == 0)
            {
                MessageBox.Show("저장할 라벨링 데이터가 없습니다.", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 저장 중 폼 생성
            Form loadingForm = new Form
            {
                Width = 300,
                Height = 120,
                Text = "JSON 저장",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                TopMost = true
            };

            Label loadingLabel = new Label
            {
                Text = "저장 중...",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(100, 30)
            };

            loadingForm.Controls.Add(loadingLabel);
            loadingForm.Show();
            loadingForm.Refresh();

            try
            {
                // 비동기로 저장 작업 수행
                await Task.Run(() => SaveCurrentLabelingData());
                
                loadingForm.Close();

                string videoDir = Path.GetDirectoryName(currentVideoFile);
                string labelsDir = Path.Combine(videoDir, "labels");
                
                MessageBox.Show(
                    $"JSON 파일이 저장되었습니다.\n\n" +
                    $"위치: {labelsDir}\n" +
                    $"박스 개수: {boundingBoxes.Count}개",
                    "저장 완료",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                loadingForm.Close();
                MessageBox.Show($"JSON 저장 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Theme
        private void btnTheme_Click(object sender, EventArgs e)
        {
            isDarkMode = !isDarkMode;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            Color bgColor = isDarkMode ? Color.FromArgb(17, 24, 39) : Color.FromArgb(243, 244, 246);
            Color surfaceColor = isDarkMode ? Color.FromArgb(31, 41, 55) : Color.White;
            Color textColor = isDarkMode ? Color.FromArgb(249, 250, 251) : Color.FromArgb(31, 41, 55);

            panelMainContainer.BackColor = bgColor;
            panelHeader.BackColor = surfaceColor;
            panelLeftSidebar.BackColor = surfaceColor;
            panelCenter.BackColor = bgColor;
            panelVideoControls.BackColor = surfaceColor;
            panelRightSidebar.BackColor = surfaceColor;

            labelTitle.ForeColor = textColor;
            labelBoxCount.ForeColor = textColor;
            labelTimeInfo.ForeColor = textColor;
        }
        #endregion

        #region Video Loading
        private async void btnSelectFolder_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Video Files|*.avi;*.mp4;*.mkv|All Files|*.*";
                ofd.Title = "Select Video Files";
                ofd.Multiselect = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    // 비디오 전환 시 자동 저장 제거 - 수동으로만 저장

                    videoFileList.Clear();
                    videoFileList.AddRange(ofd.FileNames);
                    currentVideoIndex = 0;

                    if (videoFileList.Count > 0)
                    {
                        await LoadVideoWithSubtitle(videoFileList[0]);
                    }
                }
            }
        }

        private async void btnSelectFolderPath_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select Folder containing Video Files";

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    // 비디오 전환 시 자동 저장 제거 - 수동으로만 저장

                    videoFileList.Clear();
                    string[] videoExtensions = { "*.avi", "*.mp4", "*.mkv", "*.mov", "*.flv", "*.wmv" };

                    foreach (string extension in videoExtensions)
                    {
                        string[] files = Directory.GetFiles(fbd.SelectedPath, extension, SearchOption.AllDirectories);
                        videoFileList.AddRange(files);
                    }

                    videoFileList.Sort();

                    if (videoFileList.Count == 0)
                    {
                        MessageBox.Show("선택한 폴더에서 영상 파일을 찾을 수 없습니다.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    currentVideoIndex = 0;
                    await LoadVideoWithSubtitle(videoFileList[0]);
                    boundingBoxes.Clear();
                    waypointMarkers.Clear();
                    selectedBox = null;
                    // ID는 수동 지정 방식으로 변경됨: 별도 초기화 불필요
                    UpdateBoxCount();
                    UpdateWaypointListView();
                    pictureBoxVideo.Invalidate();

                    MessageBox.Show($"총 {videoFileList.Count}개의 영상 파일을 불러왔습니다.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private async Task LoadVideoWithSubtitle(string filePath)
        {
            try
            {
                // 기존 비디오 로드
                LoadVideo(filePath);

                // 1. 먼저 외부 SRT 파일이 있는지 확인
                string videoDir = Path.GetDirectoryName(filePath);
                string videoName = Path.GetFileNameWithoutExtension(filePath);
                string externalSrtPath = Path.Combine(videoDir, $"{videoName}.srt");

                if (File.Exists(externalSrtPath))
                {
                    // 외부 SRT 파일이 있으면 바로 로드
                    currentSrtFile = externalSrtPath;
                    await LoadSrtFile(externalSrtPath);
                    return;
                }

                // 2. 외부 SRT 파일이 없으면 비디오 내부에서 자막 추출 시도
                await ExtractSrtFromVideo(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"비디오 로드 중 오류가 발생했습니다:\n{ex.Message}", 
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadVideo(string filePath)
        {
            try
            {
                if (videoCapture != null)
                {
                    videoCapture.Release();
                    videoCapture.Dispose();
                }

                videoCapture = new VideoCapture(filePath);

                if (!videoCapture.IsOpened())
                {
                    MessageBox.Show("Failed to open video file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                currentVideoFile = filePath;
                totalFrames = (int)videoCapture.Get(VideoCaptureProperties.FrameCount);
                fps = videoCapture.Get(VideoCaptureProperties.Fps);
                currentFrameIndex = 0;


                LoadFrame(0);
                UpdateTimeLabels();

                labelTitle.Text = $"Form_AllDay - {Path.GetFileName(filePath)}";

                // Form이 키 이벤트를 받을 수 있도록 포커스 설정
                this.Focus();
                this.Activate();

                // 동일 파일명의 JSON 자동 로드
                LoadLabelingData(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading video: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadFrame(int frameIndex)
        {
            if (videoCapture == null || !videoCapture.IsOpened())
                return;

            if (frameIndex < 0 || frameIndex >= totalFrames)
                return;

            videoCapture.Set(VideoCaptureProperties.PosFrames, frameIndex);

            if (currentFrame != null)
                currentFrame.Dispose();

            currentFrame = new Mat();
            videoCapture.Read(currentFrame);

            if (!currentFrame.Empty())
            {
                pictureBoxVideo.Image?.Dispose();
                pictureBoxVideo.Image = BitmapConverter.ToBitmap(currentFrame);
            }

            currentFrameIndex = frameIndex;
            UpdateTimeLabels();
            
            // Waypoint entry 프레임에서만 bbox 리스트 업데이트 (리소스 최적화)
            if (ShouldUpdateBboxList(frameIndex))
            {
                UpdateBboxListDisplay();
            }
            
            pictureBoxVideo.Invalidate();
        }

        private void UpdateTimeLabels()
        {
            if (videoCapture == null || !videoCapture.IsOpened())
                return;

            double currentSeconds = currentFrameIndex / fps;
            double totalSeconds = totalFrames / fps;

            TimeSpan currentTime = TimeSpan.FromSeconds(currentSeconds);
            TimeSpan totalTime = TimeSpan.FromSeconds(totalSeconds);

            string speedText = playbackSpeed == 1.0 ? "" : $" ({playbackSpeed}x)";
            string subtitleText = isSubtitleVisible ? GetCurrentSubtitle() : "";
            
            // 자막이 표시 상태일 때만 타임스탬프 추출하여 우측 하단에 표시
            if (isSubtitleVisible)
            {
                string timestamp = ExtractTimestampFromSubtitle(subtitleText);
                if (!string.IsNullOrEmpty(timestamp))
                {
                    labelSubtitleTimestamp.Text = timestamp;
                    labelSubtitleTimestamp.Visible = true;
                }
                else
                {
                    labelSubtitleTimestamp.Visible = false;
                }
            }
            else
            {
                labelSubtitleTimestamp.Visible = false;
            }
            
            if (!string.IsNullOrEmpty(subtitleText))
            {
                labelTimeInfo.Text = $"{currentTime:hh\\:mm\\:ss} / {totalTime:hh\\:mm\\:ss} x264{speedText}\n자막: {subtitleText}";
            }
            else
            {
                labelTimeInfo.Text = $"{currentTime:hh\\:mm\\:ss} / {totalTime:hh\\:mm\\:ss} x264{speedText}";
            }

            timelineProgress = totalFrames > 0 ? (float)currentFrameIndex / totalFrames : 0;
            panelTimeline.Invalidate();
        }
        #endregion

        #region Video Playback
        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (videoCapture == null || !videoCapture.IsOpened())
            {
                MessageBox.Show("비디오 파일이 로드되지 않았습니다.\n먼저 파일을 선택해주세요.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            isPlaying = !isPlaying;

            if (isPlaying)
            {
                btnPlay.Text = "⏸";
                lastFrameTime = DateTime.Now.Ticks / 10000;
                msPerFrame = 1000.0 / fps;

                if (msPerFrame <= 0)
                {
                    MessageBox.Show($"FPS 오류: {fps}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    isPlaying = false;
                    btnPlay.Text = "▶";
                    return;
                }

                timerPlayback.Interval = 33;
                timerPlayback.Start();
            }
            else
            {
                btnPlay.Text = "▶";
                timerPlayback.Stop();
            }
        }

        private void timerPlayback_Tick(object sender, EventArgs e)
        {
            long currentTime = DateTime.Now.Ticks / 10000;
            long elapsedMs = currentTime - lastFrameTime;

            int framesToMove = (int)(elapsedMs * playbackSpeed / msPerFrame);

            if (framesToMove > 0)
            {
                int nextFrame = Math.Min(totalFrames - 1, currentFrameIndex + framesToMove);
                LoadFrame(nextFrame);
                lastFrameTime = currentTime;

                if (nextFrame >= totalFrames - 1)
                {
                    isPlaying = false;
                    btnPlay.Text = "▶";
                    timerPlayback.Stop();
                    playbackSpeed = 1.0;
                }
            }
        }

        private void btnRewind_Click(object sender, EventArgs e)
        {
            int framesToMove = (int)(fps * 5);
            int newFrame = Math.Max(0, currentFrameIndex - framesToMove);
            LoadFrame(newFrame);
        }

        private void btnForward_Click(object sender, EventArgs e)
        {
            int framesToMove = (int)(fps * 5);
            int newFrame = Math.Min(totalFrames - 1, currentFrameIndex + framesToMove);
            LoadFrame(newFrame);
        }
        #endregion

        #region Entry/Exit Markers
        private void btnEntry_Click(object sender, EventArgs e)
        {
            // 선택된 웨이포인트가 있는지 확인
            if (listViewWaypoints.SelectedItems.Count > 0)
            {
                var selectedItem = listViewWaypoints.SelectedItems[0];
                var waypoint = selectedItem.Tag as WaypointMarker;

                if (waypoint != null)
                {
                    // 선택된 웨이포인트의 Entry 프레임으로 이동
                    LoadFrame(waypoint.EntryFrame);
                    MessageBox.Show(
                        $"Entry 프레임으로 이동했습니다.\n\n" +
                        $"프레임: {waypoint.EntryFrame}\n" +
                        $"시간: {waypoint.EntryTime}",
                        "Entry 이동",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
            }

            // 웨이포인트가 선택되지 않았으면 안내 메시지
            MessageBox.Show(
                "웨이포인트를 선택하면 Entry 프레임으로 이동합니다.\n\n" +
                "웨이포인트가 없거나 선택되지 않았습니다.",
                "안내",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void btnToggleSubtitle_Click(object sender, EventArgs e)
        {
            isSubtitleVisible = !isSubtitleVisible;
            btnToggleSubtitle.Text = isSubtitleVisible ? "자막 닫기" : "자막 열기";
            btnToggleSubtitle.BackColor = isSubtitleVisible 
                ? System.Drawing.Color.FromArgb(239, 68, 68) // 빨강 (닫기)
                : System.Drawing.Color.FromArgb(100, 116, 139); // 회색 (열기)
            
            // 자막 레이블 표시/숨기기
            if (labelSubtitleTimestamp != null)
            {
                labelSubtitleTimestamp.Visible = isSubtitleVisible;
            }
            
            // 시간 정보 업데이트하여 자막 텍스트 반영
            UpdateTimeLabels();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            // 선택된 웨이포인트가 있는지 확인
            if (listViewWaypoints.SelectedItems.Count > 0)
            {
                var selectedItem = listViewWaypoints.SelectedItems[0];
                var waypoint = selectedItem.Tag as WaypointMarker;

                if (waypoint != null)
                {
                    // 선택된 웨이포인트의 Exit 프레임으로 이동
                    LoadFrame(waypoint.ExitFrame);
                    MessageBox.Show(
                        $"Exit 프레임으로 이동했습니다.\n\n" +
                        $"프레임: {waypoint.ExitFrame}\n" +
                        $"시간: {waypoint.ExitTime}",
                        "Exit 이동",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
            }

            // 웨이포인트가 선택되지 않았으면 안내 메시지
            MessageBox.Show(
                "웨이포인트를 선택하면 Exit 프레임으로 이동합니다.\n\n" +
                "웨이포인트가 없거나 선택되지 않았습니다.",
                "안내",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // E키로 Entry 마커 설정 (원래 기능)
        private void SetEntryMarker()
        {
            entryFrameIndex = currentFrameIndex;
            TimeSpan time = TimeSpan.FromSeconds(currentFrameIndex / fps);
            btnEntry.Text = $"Entry: {time:hh\\:mm\\:ss}";
            panelTimeline.Invalidate();
        }

        // X키로 Exit 마커 설정 및 웨이포인트 생성 (원래 기능)
        private void SetExitMarkerAndCreateWaypoint()
        {
            if (!entryFrameIndex.HasValue)
            {
                MessageBox.Show("먼저 Entry를 설정해주세요. (E키)", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Entry 프레임의 모든 박스를 찾기
            var entryBoxes = boundingBoxes.Where(b => b.FrameIndex == entryFrameIndex.Value).ToList();
            if (entryBoxes.Count == 0)
            {
                MessageBox.Show("Entry 프레임에 박스가 없습니다. 먼저 박스를 그려주세요.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            exitFrameIndex = currentFrameIndex;
            TimeSpan exitTime = TimeSpan.FromSeconds(currentFrameIndex / fps);
            TimeSpan entryTime = TimeSpan.FromSeconds(entryFrameIndex.Value / fps);

            btnExit.Text = $"Exit: {exitTime:hh\\:mm\\:ss}";

            System.Diagnostics.Debug.WriteLine($"[Waypoint 생성] Entry 프레임 {entryFrameIndex.Value}에 {entryBoxes.Count}개의 박스 발견");

            // 하나의 waypoint 생성 (첫 번째 박스 기준으로, 나머지는 추적 시 함께 처리)
            var waypoint = new WaypointMarker
            {
                EntryFrame = entryFrameIndex.Value,
                ExitFrame = exitFrameIndex.Value,
                MarkerColor = markerColors[currentColorIndex % markerColors.Length],
                EntryTime = entryTime.ToString(@"hh\:mm\:ss"),
                ExitTime = exitTime.ToString(@"hh\:mm\:ss"),
                ObjectId = 0, // 여러 객체를 포함하는 waypoint이므로 0으로 설정
                Label = "multi" // 여러 객체 타입
            };

            System.Diagnostics.Debug.WriteLine($"[Waypoint 생성] Multi-object waypoint: {entryBoxes.Count}개 객체 ({waypoint.EntryTime} ~ {waypoint.ExitTime})");

            waypointMarkers.Add(waypoint);
            currentColorIndex++;

            var item = new ListViewItem(waypoint.EntryTime);
            item.SubItems.Add(waypoint.ExitTime);
            item.SubItems.Add($"● {entryBoxes.Count}개 객체");
            item.ForeColor = waypoint.MarkerColor;
            item.Tag = waypoint;
            listViewWaypoints.Items.Add(item);

            entryFrameIndex = null;
            exitFrameIndex = null;
            btnEntry.Text = "Entry: 00:00:00";
            btnExit.Text = "Exit: 00:00:00";

            panelTimeline.Invalidate();

            var result = MessageBox.Show(
                $"Waypoint가 생성되었습니다. ({entryBoxes.Count}개 객체)\n자동 추적을 수행하시겠습니까?",
                "Auto Tracking",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                PerformTrackingForWaypoint(waypoint, true);
            }
        }

        private void btnDeleteWaypoint_Click(object sender, EventArgs e)
        {
            if (listViewWaypoints.SelectedItems.Count == 0)
            {
                MessageBox.Show("삭제할 Waypoint를 선택해주세요.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = listViewWaypoints.SelectedItems[0];
            var waypoint = selectedItem.Tag as WaypointMarker;

            if (waypoint != null)
            {
                // 해당 웨이포인트의 ObjectId와 프레임 범위에 일치하는 박스만 삭제
                var boxesToDelete = boundingBoxes
                    .Where(b => 
                        b.FrameIndex >= waypoint.EntryFrame && 
                        b.FrameIndex <= waypoint.ExitFrame &&
                        GetBoxId(b) == waypoint.ObjectId &&
                        b.Label == waypoint.Label)
                    .ToList();

                foreach (var box in boxesToDelete)
                {
                    AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(box) });
                    boundingBoxes.Remove(box);
                }

                if (selectedBox != null && boxesToDelete.Contains(selectedBox))
                    selectedBox = null;

                waypointMarkers.Remove(waypoint);
                listViewWaypoints.Items.Remove(selectedItem);
                UpdateBoxCount();
                panelTimeline.Invalidate();
                pictureBoxVideo.Invalidate();
            }
        }
        #endregion

        #region Drawing Mode
        private void btnVideoList_Click(object sender, EventArgs e) => ShowVideoListForm();

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            currentMode = DrawMode.Select;
            btnSelectAll.BackColor = Color.FromArgb(59, 130, 246);
            btnEdit.BackColor = SystemColors.Control;
            pictureBoxVideo.Cursor = Cursors.Hand;
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            currentMode = DrawMode.Draw;
            btnEdit.BackColor = Color.FromArgb(59, 130, 246);
            btnSelectAll.BackColor = SystemColors.Control;
            pictureBoxVideo.Cursor = Cursors.Cross;
        }

        private void pictureBoxVideo_MouseDown(object sender, MouseEventArgs e)
        {
            if (currentMode == DrawMode.Draw)
            {
                // Entry 마커 체크 제거 - YOLO 추적 시에만 필요
                isDrawing = true;
                drawStartPoint = e.Location; // 뷰 좌표로 저장

                // 이미지 좌표로 변환하여 저장
                var imagePoint = ViewToImage(new PointF(e.X, e.Y));
                
                drawingBox = new BoundingBox
                {
                    FrameIndex = currentFrameIndex,
                    Rectangle = new Rectangle((int)imagePoint.X, (int)imagePoint.Y, 0, 0),
                    Label = currentSelectedLabel,
                    PersonId = currentSelectedLabel == "person" ? currentAssignedId : 0,
                    VehicleId = currentSelectedLabel == "vehicle" ? currentAssignedId : 0,
                    EventId = currentSelectedLabel == "event" ? currentAssignedId : 0,
                    Action = "waypoint"
                };
            }
            else if (currentMode == DrawMode.Select)
            {
                selectedBox = GetBoundingBoxAt(e.Location);

                if (selectedBox != null)
                {
                    isDragging = true;
                    // 뷰 좌표로 변환한 박스 위치 기준으로 드래그 오프셋 계산
                    var viewRect = ImageToView(new RectangleF(selectedBox.Rectangle.X, selectedBox.Rectangle.Y, 
                        selectedBox.Rectangle.Width, selectedBox.Rectangle.Height));
                    dragOffset = new System.Drawing.Point(e.X - (int)viewRect.X, e.Y - (int)viewRect.Y);
                    UpdateObjectInfo(selectedBox);
                }
            }
        }

        private void pictureBoxVideo_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing && drawingBox != null)
            {
                // 뷰 좌표에서 이미지 좌표로 변환
                var startImagePoint = ViewToImage(new PointF(drawStartPoint.X, drawStartPoint.Y));
                var currentImagePoint = ViewToImage(new PointF(e.X, e.Y));

                int x = (int)Math.Min(startImagePoint.X, currentImagePoint.X);
                int y = (int)Math.Min(startImagePoint.Y, currentImagePoint.Y);
                int width = (int)Math.Abs(currentImagePoint.X - startImagePoint.X);
                int height = (int)Math.Abs(currentImagePoint.Y - startImagePoint.Y);

                drawingBox.Rectangle = new Rectangle(x, y, width, height);

                pictureBoxVideo.Invalidate();
            }
            else if (isDragging && selectedBox != null)
            {
                // 뷰 좌표에서 이미지 좌표로 변환
                var viewPos = new PointF(e.X - dragOffset.X, e.Y - dragOffset.Y);
                var imagePos = ViewToImage(viewPos);

                selectedBox.Rectangle = new Rectangle(
                    (int)imagePos.X,
                    (int)imagePos.Y,
                    selectedBox.Rectangle.Width,
                    selectedBox.Rectangle.Height
                );

                pictureBoxVideo.Invalidate();
            }
        }

        private void pictureBoxVideo_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDrawing && drawingBox != null)
            {
                if (drawingBox.Rectangle.Width > 10 && drawingBox.Rectangle.Height > 10)
                {
                    boundingBoxes.Add(drawingBox);
                    AddUndoAction(new UndoAction { Type = UndoActionType.AddBox, Box = drawingBox });

                    selectedBox = drawingBox;
                    UpdateObjectInfo(selectedBox);
                    UpdateBoxCount();
                    UpdateBboxListDisplay();
                }

                drawingBox = null;
                isDrawing = false;
                pictureBoxVideo.Invalidate();
            }
            else if (isDragging)
            {
                isDragging = false;
            }
        }

        private void pictureBoxVideo_Resize(object sender, EventArgs e)
        {
            // PictureBox 크기가 변경될 때 타임스탬프 Label 위치 조정 (좌측 하단)
            if (labelSubtitleTimestamp != null && pictureBoxVideo != null)
            {
                labelSubtitleTimestamp.Location = new System.Drawing.Point(
                    50,
                    pictureBoxVideo.Height - labelSubtitleTimestamp.Height - 30
                );
            }
        }

        private void panelVideoControls_Resize(object sender, EventArgs e)
        {
            // panelVideoControls 크기가 변경될 때 panelTimeline을 가운데 정렬
            if (panelTimeline != null && panelVideoControls != null)
            {
                panelTimeline.Location = new System.Drawing.Point(
                    (panelVideoControls.Width - panelTimeline.Width) / 2,
                    panelTimeline.Location.Y // Y 위치는 유지
                );
            }
        }

        private void pictureBoxVideo_Paint(object sender, PaintEventArgs e)
        {
            if (pictureBoxVideo.Image == null)
                return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 현재 프레임에 해당하는 박스들을 필터링
            var currentFrameBoxes = boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex);

            foreach (var box in currentFrameBoxes)
            {
                // 현재 프레임의 박스만 표시 (box.FrameIndex == currentFrameIndex)
                // currentFrameBoxes에서 이미 필터링되었으므로 추가 체크 불필요

                // 이미지 좌표를 뷰 좌표로 변환
                var viewRect = ImageToView(new RectangleF(box.Rectangle.X, box.Rectangle.Y, 
                    box.Rectangle.Width, box.Rectangle.Height));

                Color boxColor = GetColorForLabel(box.Label);
                using (Pen pen = new Pen(boxColor, 3))
                {
                    if (box == selectedBox)
                        pen.Width = 5;
                    g.DrawRectangle(pen, viewRect.X, viewRect.Y, viewRect.Width, viewRect.Height);
                }

                // 라벨 텍스트 생성
                string labelText = "";
                if (box.Label == "person")
                {
                    labelText = $"person_{box.PersonId:D2}";
                }
                else if (box.Label == "vehicle")
                {
                    string[] vehicleTypes = { "car", "motorcycle", "bicycle", "e_scooter" };
                    if (box.VehicleId > 0 && box.VehicleId <= vehicleTypes.Length)
                        labelText = $"vehicle_{vehicleTypes[box.VehicleId - 1]}";
                    else
                        labelText = $"vehicle_{box.VehicleId}";
                }
                else if (box.Label == "event")
                {
                    string[] eventTypes = { "contact", "exchange", "board", "final_exchange" };
                    if (box.EventId > 0 && box.EventId <= eventTypes.Length)
                        labelText = $"event_{eventTypes[box.EventId - 1]}";
                    else
                        labelText = $"event_{box.EventId}";
                }
                
                using (Font font = new Font("Segoe UI", 10F, FontStyle.Bold))
                {
                    SizeF textSize = g.MeasureString(labelText, font);
                    RectangleF labelBg = new RectangleF(
                        viewRect.X,
                        viewRect.Y - textSize.Height - 4,
                        textSize.Width + 8,
                        textSize.Height + 4
                    );

                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(200, boxColor)))
                        g.FillRectangle(bgBrush, labelBg);

                    using (SolidBrush textBrush = new SolidBrush(Color.White))
                        g.DrawString(labelText, font, textBrush, viewRect.X + 4, viewRect.Y - textSize.Height - 2);
                }
            }

            if (isDrawing && drawingBox != null)
            {
                // 이미지 좌표를 뷰 좌표로 변환
                var viewRect = ImageToView(new RectangleF(drawingBox.Rectangle.X, drawingBox.Rectangle.Y, 
                    drawingBox.Rectangle.Width, drawingBox.Rectangle.Height));

                Color boxColor = GetColorForLabel(drawingBox.Label);
                using (Pen pen = new Pen(boxColor, 3) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, viewRect.X, viewRect.Y, viewRect.Width, viewRect.Height);
            }
        }

        private BoundingBox GetBoundingBoxAt(System.Drawing.Point location)
        {
            // 뷰 좌표를 이미지 좌표로 변환
            var imageLocation = ViewToImage(new PointF(location.X, location.Y));
            
            // 현재 프레임에 해당하는 박스들을 필터링
            var currentFrameBoxes = boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex);

            foreach (var box in currentFrameBoxes.Reverse())
            {
                // 웨이포인트 확인: 해당 박스의 PersonId와 일치하는 웨이포인트 찾기
                var waypoint = waypointMarkers.FirstOrDefault(w => 
                    w.ObjectId == GetBoxId(box) && 
                    w.Label == box.Label);

                // 웨이포인트가 있으면 Entry 프레임 이후에만 선택 가능
                if (waypoint != null)
                {
                    if (currentFrameIndex < waypoint.EntryFrame || currentFrameIndex > waypoint.ExitFrame)
                        continue;
                }

                // 이미지 좌표로 비교
                if (box.Rectangle.Contains((int)imageLocation.X, (int)imageLocation.Y))
                    return box;
            }
            return null;
        }

        private Color GetColorForLabel(string label)
        {
            return label.ToLower() switch
            {
                "person" => Color.FromArgb(236, 72, 153),
                "vehicle" => Color.FromArgb(59, 130, 246),
                "event" => Color.FromArgb(34, 197, 94),
                _ => Color.Yellow
            };
        }

        private void UpdateBoxCount()
        {
            labelBoxCount.Text = $"박스 개수: {boundingBoxes.Count}";
        }

        private string FormatFrameTime(int frameIndex)
        {
            TimeSpan time = TimeSpan.FromSeconds(frameIndex / fps);
            return time.ToString(@"hh\:mm\:ss");
        }

        private void UpdateWaypointListView()
        {
            listViewWaypoints.Items.Clear();

            foreach (var waypoint in waypointMarkers)
            {
                var item = new ListViewItem(waypoint.EntryTime);
                item.SubItems.Add(waypoint.ExitTime);
                
                // multi-object waypoint인 경우 Entry 프레임의 고유 객체 개수 표시
                if (waypoint.Label == "multi" && waypoint.ObjectId == 0)
                {
                    // Entry 프레임에 있는 고유한 객체(label + ID 조합)의 개수만 카운트
                    var uniqueObjects = boundingBoxes
                        .Where(b => b.FrameIndex == waypoint.EntryFrame)
                        .Select(b => new { b.Label, Id = GetBoxId(b) })
                        .Distinct()
                        .Count();
                    
                    item.SubItems.Add($"{uniqueObjects}개");
                }
                else
                {
                    // 기존 단일 객체 waypoint (하위 호환성)
                    item.SubItems.Add($"1개");
                }
                
                item.ForeColor = waypoint.MarkerColor;
                item.Tag = waypoint;
                listViewWaypoints.Items.Add(item);
            }
        }

        private void UpdateObjectInfo(BoundingBox box)
        {
            string labelText = "";
            if (box.Label == "person")
            {
                labelText = $"Label: person_{box.PersonId:D2}";
            }
            else if (box.Label == "vehicle")
            {
                string[] vehicleTypes = { "car", "motorcycle", "bicycle", "e_scooter" };
                if (box.VehicleId > 0 && box.VehicleId <= vehicleTypes.Length)
                    labelText = $"Label: vehicle_{vehicleTypes[box.VehicleId - 1]}";
                else
                    labelText = $"Label: vehicle_{box.VehicleId}";
            }
            else if (box.Label == "event")
            {
                string[] eventTypes = { "contact", "exchange", "board", "final_exchange" };
                if (box.EventId > 0 && box.EventId <= eventTypes.Length)
                    labelText = $"Label: event_{eventTypes[box.EventId - 1]}";
                else
                    labelText = $"Label: event_{box.EventId}";
            }
            
            labelObjectLabel.Text = labelText;
            labelPrevWaypoint.Text = "Previous Waypoint: C0001.mp4, 00:10:32 - 00:11:05";
            labelNextWaypoint.Text = "Next Waypoint: C0003.mp4, 00:15:21 - 00:16:01";
        }

        private void ShowVideoListForm()
        {
            if (videoFileList.Count == 0)
            {
                MessageBox.Show("로드된 비디오 파일이 없습니다.\n먼저 파일을 선택해주세요.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (videoListForm == null || videoListForm.IsDisposed)
            {
                videoListForm = new Form()
                {
                    Text = "Video List",
                    Width = 400,
                    Height = 500,
                    StartPosition = FormStartPosition.CenterScreen,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                videoListView = new ListView()
                {
                    Dock = DockStyle.Fill,
                    View = View.Details,
                    FullRowSelect = true,
                    GridLines = true
                };

                videoListView.Columns.Add("File Name", 350);
                videoListView.DoubleClick += VideoListView_DoubleClick;

                RefreshVideoListView();

                videoListForm.Controls.Add(videoListView);
                videoListForm.Show();
            }
            else
            {
                videoListForm.BringToFront();
            }
        }

        private void RefreshVideoListView()
        {
            if (videoListView == null)
                return;

            videoListView.Items.Clear();

            for (int i = 0; i < videoFileList.Count; i++)
            {
                string fileName = Path.GetFileName(videoFileList[i]);
                ListViewItem item = new ListViewItem(fileName);
                item.Tag = i;

                if (i == currentVideoIndex)
                {
                    item.BackColor = Color.FromArgb(59, 130, 246);
                    item.ForeColor = Color.White;
                }

                videoListView.Items.Add(item);
            }
        }

        private async void VideoListView_DoubleClick(object sender, EventArgs e)
        {
            if (videoListView.SelectedItems.Count == 0)
                return;

            int selectedIndex = (int)videoListView.SelectedItems[0].Tag;

            if (selectedIndex != currentVideoIndex)
            {
                // 비디오 전환 시 자동 저장 제거 - 수동으로만 저장
                currentVideoIndex = selectedIndex;
                await LoadVideoWithSubtitle(videoFileList[currentVideoIndex]);
                boundingBoxes.Clear();
                waypointMarkers.Clear();
                selectedBox = null;
                // ID는 수동 지정 방식으로 변경됨: 별도 초기화 불필요
                UpdateBoxCount();
                UpdateWaypointListView();
                pictureBoxVideo.Invalidate();
                RefreshVideoListView();
            }
        }

        private void panelLabelPerson_Click(object sender, EventArgs e)
        {
            currentSelectedLabel = "person";
            
            if (selectedBox != null)
            {
                ApplyLabelChange("person", currentAssignedId, selectedBox.Label, GetBoxId(selectedBox), selectedBox.Rectangle);
            }
            else
            {
                MessageBox.Show($"Person 라벨 선택됨. 현재 ID: {currentAssignedId}", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void panelLabelVehicle_Click(object sender, EventArgs e)
        {
            currentSelectedLabel = "vehicle";
            
            if (selectedBox != null)
            {
                ApplyLabelChange("vehicle", currentAssignedId, selectedBox.Label, GetBoxId(selectedBox), selectedBox.Rectangle);
            }
            else
            {
                MessageBox.Show($"Vehicle 라벨 선택됨. 현재 ID: {currentAssignedId}", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void panelLabelEvent_Click(object sender, EventArgs e)
        {
            currentSelectedLabel = "event";
            
            if (selectedBox != null)
            {
                ApplyLabelChange("event", currentAssignedId, selectedBox.Label, GetBoxId(selectedBox), selectedBox.Rectangle);
            }
            else
            {
                MessageBox.Show($"Event 라벨 선택됨. 현재 ID: {currentAssignedId}", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // 라벨 타입 선택 버튼 핸들러
        private void btnLabelPerson_Click(object sender, EventArgs e)
        {
            currentSelectedLabel = "person";
            currentAssignedId = 1; // 기본값 person_01
            
            // 선택된 bbox가 있으면 해당 박스를 person으로 변경
            if (selectedBox != null)
            {
                string oldLabel = selectedBox.Label;
                
                selectedBox.Label = "person";
                SetBoxId(selectedBox, "person", 1); // 기본값 person_01
                
                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel
                });
                
                pictureBoxVideo.Invalidate();
                UpdateBboxListDisplay();
                UpdateObjectInfo(selectedBox);
            }
        }

        private void btnLabelVehicle_Click(object sender, EventArgs e)
        {
            currentSelectedLabel = "vehicle";
            currentAssignedId = 1; // 기본값 vehicle_car
            
            // 선택된 bbox가 있으면 해당 박스를 vehicle로 변경
            if (selectedBox != null)
            {
                string oldLabel = selectedBox.Label;
                
                selectedBox.Label = "vehicle";
                SetBoxId(selectedBox, "vehicle", 1); // 기본값 vehicle_car
                
                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel
                });
                
                pictureBoxVideo.Invalidate();
                UpdateBboxListDisplay();
                UpdateObjectInfo(selectedBox);
            }
        }

        private void btnLabelEvent_Click(object sender, EventArgs e)
        {
            currentSelectedLabel = "event";
            currentAssignedId = 1; // 기본값 event_contact
            
            // 선택된 bbox가 있으면 해당 박스를 event로 변경
            if (selectedBox != null)
            {
                string oldLabel = selectedBox.Label;
                
                selectedBox.Label = "event";
                SetBoxId(selectedBox, "event", 1); // 기본값 event_contact
                
                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel
                });
                
                pictureBoxVideo.Invalidate();
                UpdateBboxListDisplay();
                UpdateObjectInfo(selectedBox);
            }
        }

        // bbox 리스트 업데이트가 필요한지 확인 (리소스 최적화)
        private bool ShouldUpdateBboxList(int frameIndex)
        {
            // 1. Waypoint entry 프레임인 경우
            foreach (var waypoint in waypointMarkers)
            {
                if (waypoint.EntryFrame == frameIndex)
                    return true;
            }
            
            // 2. 현재 프레임의 bbox 개수가 변경된 경우
            int currentBoxCount = boundingBoxes.Count(b => b.FrameIndex == frameIndex);
            if (currentBoxCount != lastRenderedBoxCount)
            {
                lastRenderedBoxCount = currentBoxCount;
                return true;
            }
            
            return false;
        }
        
        // 현재 프레임의 bbox 목록을 동적으로 생성하여 표시
        private void UpdateBboxListDisplay()
        {
            panelBboxList.Controls.Clear();
            
            var currentBoxes = boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex).ToList();
            
            if (currentBoxes.Count == 0)
            {
                Label emptyLabel = new Label
                {
                    Text = "현재 프레임에 bbox가 없습니다.",
                    Font = new System.Drawing.Font("Segoe UI", 9F),
                    ForeColor = System.Drawing.Color.Gray,
                    Location = new System.Drawing.Point(10, 10),
                    Size = new System.Drawing.Size(230, 20),
                    AutoSize = true
                };
                panelBboxList.Controls.Add(emptyLabel);
                return;
            }
            
            int yPos = 5;
            foreach (var box in currentBoxes)
            {
                string displayText = "";
                System.Drawing.Color bgColor = System.Drawing.Color.White;
                System.Drawing.Color fgColor = System.Drawing.Color.Black;
                
                if (box.Label == "person")
                {
                    displayText = $"person_{box.PersonId:D2}";
                    bgColor = System.Drawing.Color.FromArgb(252, 231, 243);
                    fgColor = System.Drawing.Color.FromArgb(157, 23, 77);
                }
                else if (box.Label == "vehicle")
                {
                    string[] vehicleTypes = { "car", "motorcycle", "bicycle", "e_scooter" };
                    if (box.VehicleId > 0 && box.VehicleId <= vehicleTypes.Length)
                        displayText = $"vehicle_{vehicleTypes[box.VehicleId - 1]}";
                    else
                        displayText = $"vehicle_{box.VehicleId}";
                    bgColor = System.Drawing.Color.FromArgb(219, 234, 254);
                    fgColor = System.Drawing.Color.FromArgb(30, 64, 175);
                }
                else if (box.Label == "event")
                {
                    string[] eventTypes = { "contact", "exchange", "board", "final_exchange" };
                    if (box.EventId > 0 && box.EventId <= eventTypes.Length)
                        displayText = $"event_{eventTypes[box.EventId - 1]}";
                    else
                        displayText = $"event_{box.EventId}";
                    bgColor = System.Drawing.Color.FromArgb(220, 252, 231);
                    fgColor = System.Drawing.Color.FromArgb(20, 83, 45);
                }
                
                // 각 bbox 항목 패널 (높이 증가 - 드롭다운 공간)
                Panel itemPanel = new Panel
                {
                    Location = new System.Drawing.Point(5, yPos),
                    Size = new System.Drawing.Size(256, 65),
                    BackColor = bgColor,
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    Tag = box
                };
                
                Label itemLabel = new Label
                {
                    Text = displayText,
                    Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                    ForeColor = fgColor,
                    Location = new System.Drawing.Point(8, 5),
                    Size = new System.Drawing.Size(200, 20),
                    AutoSize = true
                };
                
                // 드롭다운 생성 (라벨 타입에 따라)
                ComboBox comboBox = new ComboBox
                {
                    Location = new System.Drawing.Point(8, 30),
                    Size = new System.Drawing.Size(240, 25),
                    DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList,
                    Font = new System.Drawing.Font("Segoe UI", 8F),
                    Tag = box
                };
                
                // 라벨 타입에 따라 아이템 추가
                if (box.Label == "person")
                {
                    for (int i = 1; i <= 19; i++)
                    {
                        comboBox.Items.Add($"person_{i:D2}");
                    }
                    comboBox.SelectedIndex = box.PersonId - 1;
                }
                else if (box.Label == "vehicle")
                {
                    comboBox.Items.Add("vehicle_car");
                    comboBox.Items.Add("vehicle_motorcycle");
                    comboBox.Items.Add("vehicle_bicycle");
                    comboBox.Items.Add("vehicle_e_scooter");
                    if (box.VehicleId >= 1 && box.VehicleId <= 4)
                        comboBox.SelectedIndex = box.VehicleId - 1;
                }
                else if (box.Label == "event")
                {
                    comboBox.Items.Add("event_contact");
                    comboBox.Items.Add("event_exchange");
                    comboBox.Items.Add("event_board");
                    comboBox.Items.Add("event_final_exchange");
                    if (box.EventId >= 1 && box.EventId <= 4)
                        comboBox.SelectedIndex = box.EventId - 1;
                }
                
                // 드롭다운 변경 이벤트
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    ComboBox cb = (ComboBox)s;
                    BoundingBox targetBox = (BoundingBox)cb.Tag;
                    string selected = cb.SelectedItem?.ToString() ?? "";
                    
                    if (string.IsNullOrEmpty(selected))
                        return;
                    
                    string oldLabel = targetBox.Label;
                    int oldId = GetBoxId(targetBox);
                    Rectangle oldRect = targetBox.Rectangle;
                    
                    // 라벨 업데이트
                    if (selected.StartsWith("person_"))
                    {
                        string[] parts = selected.Split('_');
                        if (parts.Length == 2 && int.TryParse(parts[1], out int personId))
                        {
                            targetBox.Label = "person";
                            SetBoxId(targetBox, "person", personId);
                        }
                    }
                    else if (selected.StartsWith("vehicle_"))
                    {
                        string[] parts = selected.Split('_');
                        if (parts.Length == 2)
                        {
                            string[] vehicleTypes = { "car", "motorcycle", "bicycle", "e_scooter" };
                            int vehicleId = Array.IndexOf(vehicleTypes, parts[1]) + 1;
                            if (vehicleId > 0)
                            {
                                targetBox.Label = "vehicle";
                                SetBoxId(targetBox, "vehicle", vehicleId);
                            }
                        }
                    }
                    else if (selected.StartsWith("event_"))
                    {
                        string eventType = selected.Substring(6); // "event_" 이후의 문자열
                        string[] eventTypes = { "contact", "exchange", "board", "final_exchange" };
                        int eventId = Array.IndexOf(eventTypes, eventType) + 1;
                        if (eventId > 0)
                        {
                            targetBox.Label = "event";
                            SetBoxId(targetBox, "event", eventId);
                        }
                    }
                    
                    AddUndoAction(new UndoAction
                    {
                        Type = UndoActionType.ModifyBox,
                        Box = CloneBoundingBox(targetBox),
                        OriginalLabel = oldLabel,
                        OriginalObjectId = oldId,
                        OriginalRectangle = oldRect
                    });
                    
                    if (selectedBox == targetBox)
                    {
                        UpdateObjectInfo(selectedBox);
                    }
                    
                    UpdateBboxListDisplay();
                    pictureBoxVideo.Invalidate();
                };
                
                itemPanel.Controls.Add(itemLabel);
                itemPanel.Controls.Add(comboBox);
                
                // 클릭 이벤트 - 박스 선택
                EventHandler clickHandler = (s, e) =>
                {
                    selectedBox = box;
                    UpdateObjectInfo(selectedBox);
                    pictureBoxVideo.Invalidate();
                    
                    // 선택된 항목 강조 표시
                    foreach (Control ctrl in panelBboxList.Controls)
                    {
                        if (ctrl is Panel p)
                        {
                            if (p == itemPanel)
                                p.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
                            else
                                p.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                        }
                    }
                };
                
                itemPanel.Click += clickHandler;
                itemLabel.Click += clickHandler;
                
                panelBboxList.Controls.Add(itemPanel);
                yPos += 70;
            }
        }
        
        // 선택한 bbox 삭제 버튼 핸들러
        private void btnDeleteLabel_Click(object sender, EventArgs e)
        {
            if (selectedBox == null)
            {
                MessageBox.Show("삭제할 bbox를 먼저 선택해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(selectedBox) });
            boundingBoxes.Remove(selectedBox);
            selectedBox = null;
            UpdateBoxCount();
            UpdateBboxListDisplay();
            pictureBoxVideo.Invalidate();
        }

        // 박스의 현재 라벨에 해당하는 ID 가져오기
        private int GetBoxId(BoundingBox box)
        {
            if (box.Label == "person") return box.PersonId;
            if (box.Label == "vehicle") return box.VehicleId;
            if (box.Label == "event") return box.EventId;
            return 0;
        }
        
        // 박스의 특정 라벨 타입에 ID 설정
        private void SetBoxId(BoundingBox box, string label, int id)
        {
            if (label == "person") box.PersonId = id;
            else if (label == "vehicle") box.VehicleId = id;
            else if (label == "event") box.EventId = id;
        }

        // 스펙에 맞는 Category ID를 반환 (JSON 내보내기용)
        private int GetCategoryId(string label, int boxId)
        {
            string categoryName = GetCategoryName(label, boxId);
            
            if (CategoryIdMap.ContainsKey(categoryName))
                return CategoryIdMap[categoryName];
            
            // 기본값 처리 (매핑되지 않은 경우)
            if (label == "person") return Math.Min(boxId, 14); // 1~14
            if (label == "vehicle") return Math.Min(15 + (boxId - 1), 18); // 15~18
            if (label == "event") return Math.Min(19 + (boxId - 1), 24); // 19~24
            
            return boxId;
        }

        // 스펙에 맞는 Category Name을 반환 (JSON 내보내기용)
        private string GetCategoryName(string label, int boxId)
        {
            if (label == "person")
            {
                // person은 person_01 ~ person_14 형식
                return $"person_{boxId:D2}";
            }
            else if (label == "vehicle")
            {
                // vehicle은 고유 이름 매핑 (ID 15~18)
                switch (boxId)
                {
                    case 1: return "car";           // ID: 15
                    case 2: return "motorcycle";    // ID: 16
                    case 3: return "e_scooter";     // ID: 17
                    case 4: return "bicycle";       // ID: 18
                    default: return "car"; // 기본값
                }
            }
            else if (label == "event")
            {
                // event는 고유 이름 매핑 (ID 19~22)
                switch (boxId)
                {
                    case 1: return "contact";         // ID: 19
                    case 2: return "exchange";        // ID: 20
                    case 3: return "board";           // ID: 21
                    case 4: return "final_exchange";  // ID: 22
                    default: return "contact"; // 기본값
                }
            }
            
            return $"{label}_{boxId:D2}";
        }

        private void ApplyLabelChange(string newLabel, int newId, string oldLabel, int oldId, Rectangle oldRect)
        {
            if (selectedBox != null)
            {
                selectedBox.Label = newLabel;
                SetBoxId(selectedBox, newLabel, newId);
                labelObjectLabel.Text = $"Label: {newLabel}_{newId:D2}";

                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel,
                    OriginalObjectId = oldId,
                    OriginalRectangle = oldRect
                });

                UpdateBboxListDisplay();
                pictureBoxVideo.Invalidate();
            }
        }

        private void btnAddLabel_Click(object sender, EventArgs e)
        {
            string labelName = ShowInputDialog("새 라벨 추가", "라벨 이름을 입력하세요 (예: person_02):");

            if (string.IsNullOrWhiteSpace(labelName))
                return;

            if (customLabels.Any(l => l.Name == labelName))
            {
                MessageBox.Show("이미 존재하는 라벨입니다.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string labelType;
            int labelId = 0;
            Color labelColor;

            if (labelName.StartsWith("person_"))
            {
                labelType = "person";
                string idPart = labelName.Substring(7);
                if (int.TryParse(idPart, out int parsedId))
                    labelId = parsedId;
                labelColor = Color.FromArgb(252, 231, 243);
            }
            else if (labelName.StartsWith("vehicle_") || labelName == "vehicle")
            {
                labelType = "vehicle";
                labelColor = Color.FromArgb(219, 234, 254);
            }
            else if (labelName.StartsWith("event_") || labelName == "event")
            {
                labelType = "event";
                labelColor = Color.FromArgb(220, 252, 231);
            }
            else
            {
                labelType = "custom";
                labelColor = Color.FromArgb(254, 243, 199);
            }

            Panel labelPanel = new Panel();
            labelPanel.Location = new System.Drawing.Point(12, customLabelYPosition);
            labelPanel.Size = new System.Drawing.Size(260, 40);
            labelPanel.BackColor = labelColor;
            labelPanel.Cursor = Cursors.Hand;

            Label label = new Label();
            label.Text = labelName;
            label.Font = new System.Drawing.Font("Segoe UI", 9F, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(30, 30, 30);
            label.Location = new System.Drawing.Point(12, 10);
            label.Size = new System.Drawing.Size(200, 20);
            label.Cursor = Cursors.Hand;

            labelPanel.Controls.Add(label);

            var customLabel = new CustomLabel
            {
                Name = labelName,
                Type = labelType,
                Id = labelId,
                Panel = labelPanel,
                Label = label
            };
            customLabels.Add(customLabel);

            labelPanel.Click += (s, args) => CustomLabel_Click(customLabel);
            label.Click += (s, args) => CustomLabel_Click(customLabel);

            groupBoxLabels.Controls.Add(labelPanel);
            customLabelYPosition += 50;

            if (customLabelYPosition > groupBoxLabels.Height - 50)
            {
                groupBoxLabels.Height = customLabelYPosition + 50;
            }
        }

        private void CustomLabel_Click(CustomLabel customLabel)
        {
            if (selectedBox != null)
            {
                string oldLabel = selectedBox.Label;
                int oldPersonId = GetBoxId(selectedBox);
                Rectangle oldRect = selectedBox.Rectangle;

                selectedBox.Label = customLabel.Type;
                SetBoxId(selectedBox, customLabel.Type, customLabel.Id);

                labelObjectLabel.Text = $"Label: {customLabel.Name}";

                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel,
                    OriginalObjectId = oldPersonId,
                    OriginalRectangle = oldRect
                });

                pictureBoxVideo.Invalidate();
            }
            else
            {
                MessageBox.Show("먼저 BBox를 선택해주세요.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private string ShowInputDialog(string title, string promptText)
        {
            Form prompt = new Form()
            {
                Width = 400,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label textLabel = new Label() { Left = 20, Top = 20, Width = 350, Text = promptText };
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 340 };
            Button confirmation = new Button() { Text = "확인", Left = 200, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            Button cancel = new Button() { Text = "취소", Left = 290, Width = 80, Top = 80, DialogResult = DialogResult.Cancel };

            confirmation.Click += (sender, e) => { prompt.Close(); };
            cancel.Click += (sender, e) => { prompt.Close(); };

            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }
        #endregion

        #region Timeline
        private void panelTimeline_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int width = panelTimeline.Width;
            int height = panelTimeline.Height;

            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(250, 204, 21)))
            {
                g.FillRectangle(bgBrush, 0, 0, width, height);
            }

            if (totalFrames > 0)
            {
                foreach (var waypoint in waypointMarkers)
                {
                    int startX = (int)(width * ((float)waypoint.EntryFrame / totalFrames));
                    int endX = (int)(width * ((float)waypoint.ExitFrame / totalFrames));
                    int segmentWidth = endX - startX;

                    using (SolidBrush markerBrush = new SolidBrush(waypoint.MarkerColor))
                    {
                        g.FillRectangle(markerBrush, startX, 0, segmentWidth, height);
                    }
                }
            }

            if (entryFrameIndex.HasValue && !exitFrameIndex.HasValue && totalFrames > 0)
            {
                int entryX = (int)(width * ((float)entryFrameIndex.Value / totalFrames));
                using (Pen pen = new Pen(Color.Red, 3))
                {
                    g.DrawLine(pen, entryX, 0, entryX, height);
                }
            }

            if (totalFrames > 0)
            {
                int currentX = (int)(width * timelineProgress);
                using (Pen pen = new Pen(Color.White, 2))
                {
                    g.DrawLine(pen, currentX, 0, currentX, height);
                }
            }
        }

        private void panelTimeline_MouseDown(object sender, MouseEventArgs e)
        {
            if (totalFrames == 0) return;

            isTimelineDragging = true;
            UpdateFrameFromMousePosition(e.X);
        }

        private void panelTimeline_MouseMove(object sender, MouseEventArgs e)
        {
            if (isTimelineDragging && totalFrames > 0)
            {
                UpdateFrameFromMousePosition(e.X);
            }
        }

        private void panelTimeline_MouseUp(object sender, MouseEventArgs e)
        {
            isTimelineDragging = false;
        }

        private void UpdateFrameFromMousePosition(int mouseX)
        {
            if (totalFrames == 0) return;

            float clickPosition = (float)mouseX / panelTimeline.Width;
            clickPosition = Math.Max(0, Math.Min(1, clickPosition));

            int targetFrame = (int)(clickPosition * totalFrames);
            targetFrame = Math.Max(0, Math.Min(totalFrames - 1, targetFrame));

            LoadFrame(targetFrame);
        }
        #endregion

        #region Undo/Redo System
        private void AddUndoAction(UndoAction action)
        {
            undoStack.Push(action);

            if (undoStack.Count > MAX_UNDO_STACK)
            {
                var tempList = undoStack.ToList();
                tempList.RemoveAt(tempList.Count - 1);
                undoStack = new Stack<UndoAction>(tempList.AsEnumerable().Reverse());
            }

            redoStack.Clear();
        }

        private void Undo()
        {
            if (undoStack.Count == 0)
            {
                MessageBox.Show("되돌릴 작업이 없습니다.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var action = undoStack.Pop();

            switch (action.Type)
            {
                case UndoActionType.AddBox:
                    boundingBoxes.Remove(action.Box);
                    if (selectedBox == action.Box)
                        selectedBox = null;
                    break;

                case UndoActionType.RemoveBox:
                    boundingBoxes.Add(action.Box);
                    break;

                case UndoActionType.ModifyBox:
                    var boxToModify = boundingBoxes.FirstOrDefault(b =>
                        b.FrameIndex == action.Box.FrameIndex &&
                        GetBoxId(b) == GetBoxId(action.Box) &&
                        b.Label == action.Box.Label);

                    if (boxToModify != null)
                    {
                        boxToModify.Rectangle = action.OriginalRectangle;
                        boxToModify.Label = action.OriginalLabel;
                        SetBoxId(boxToModify, action.OriginalLabel, action.OriginalObjectId);
                    }
                    break;

                case UndoActionType.Tracking:
                    foreach (var box in action.TrackedBoxes)
                    {
                        boundingBoxes.Remove(box);
                    }
                    break;
            }

            redoStack.Push(action);
            UpdateBoxCount();
            UpdateBboxListDisplay();
            pictureBoxVideo.Invalidate();
        }

        private void Redo()
        {
            if (redoStack.Count == 0)
            {
                MessageBox.Show("다시 실행할 작업이 없습니다.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var action = redoStack.Pop();

            switch (action.Type)
            {
                case UndoActionType.AddBox:
                    boundingBoxes.Add(action.Box);
                    break;

                case UndoActionType.RemoveBox:
                    boundingBoxes.Remove(action.Box);
                    if (selectedBox == action.Box)
                        selectedBox = null;
                    break;

                case UndoActionType.ModifyBox:
                    var boxToModify = boundingBoxes.FirstOrDefault(b =>
                        b.FrameIndex == action.Box.FrameIndex &&
                        GetBoxId(b) == action.OriginalObjectId &&
                        b.Label == action.OriginalLabel);

                    if (boxToModify != null)
                    {
                        boxToModify.Rectangle = action.Box.Rectangle;
                        boxToModify.Label = action.Box.Label;
                        SetBoxId(boxToModify, action.Box.Label, GetBoxId(action.Box));
                    }
                    break;

                case UndoActionType.Tracking:
                    foreach (var box in action.TrackedBoxes)
                    {
                        boundingBoxes.Add(box);
                    }
                    break;
            }

            undoStack.Push(action);
            UpdateBoxCount();
            UpdateBboxListDisplay();
            pictureBoxVideo.Invalidate();
        }
        #endregion

        #region Tracking Algorithm
        private async void PerformTrackingForWaypoint(WaypointMarker waypoint, bool useYolo = false)
        {
            try
            {
                // Entry 프레임의 모든 박스를 찾기 (multi-object waypoint)
                var startBoxes = boundingBoxes.Where(b => b.FrameIndex == waypoint.EntryFrame).ToList();

                if (startBoxes.Count == 0)
                {
                    MessageBox.Show(
                        $"Entry 프레임에 BBox를 찾을 수 없습니다.\n\n" +
                        "추적하려면:\n" +
                        "1. Entry 프레임으로 이동\n" +
                        "2. BBox 그리기\n" +
                        "3. 다시 Ctrl+T 시도",
                        "오류",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // 디버깅: 찾은 박스 정보 확인
                System.Diagnostics.Debug.WriteLine($"[추적 시작] {startBoxes.Count}개의 객체 추적");
                foreach (var box in startBoxes)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {box.Label}_{GetBoxId(box):D2}");
                }
                System.Diagnostics.Debug.WriteLine($"[추적 모드] useYolo: {useYolo}, isYoloAvailable: {isYoloAvailable}");

                // 추적 중 로딩 폼 생성
                Form loadingForm = new Form
                {
                    Width = 350,
                    Height = 120,
                    Text = useYolo ? "YOLO 추적 중" : "OpenCV 추적 중",
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    TopMost = true
                };

                Label loadingLabel = new Label
                {
                    Text = useYolo ? "YOLO 추적 중... 잠시만 기다려주세요." : "OpenCV 추적 중... 잠시만 기다려주세요.",
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                    Location = new System.Drawing.Point(40, 35)
                };

                loadingForm.Controls.Add(loadingLabel);
                loadingForm.Show();
                loadingForm.Refresh();

                List<BoundingBox> allTrackedBoxes = new List<BoundingBox>();

                if (useYolo && isYoloAvailable)
                {
                    System.Diagnostics.Debug.WriteLine($"[추적] {startBoxes.Count}개 객체에 대해 YOLO 추적 시작");
                    
                    // 각 startBox에 대해 개별적으로 YOLO 추적 수행
                    foreach (var startBox in startBoxes)
                    {
                        System.Diagnostics.Debug.WriteLine($"[추적] {startBox.Label}_{GetBoxId(startBox):D2} 추적 중...");
                        
                        var trackedBoxes = await Task.Run(() => trackingEngine.TrackObjects(
                            videoCapture,
                            startBox,
                            waypoint.EntryFrame,
                            waypoint.ExitFrame,
                            fps));
                        
                        allTrackedBoxes.AddRange(trackedBoxes);
                        System.Diagnostics.Debug.WriteLine($"[추적] {startBox.Label}_{GetBoxId(startBox):D2}: {trackedBoxes.Count}개 프레임 추적 완료");
                    }
                }
                else 
                {
                    System.Diagnostics.Debug.WriteLine($"[추적] YOLO 사용 불가 - useYolo: {useYolo}, isYoloAvailable: {isYoloAvailable}");
                    loadingForm.Close();
                    MessageBox.Show(
                        "YOLO 모델을 사용할 수 없습니다.\n" +
                        $"useYolo: {useYolo}\n" +
                        $"isYoloAvailable: {isYoloAvailable}",
                        "정보",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                loadingForm.Close();

                // 추적된 박스 추가
                foreach (var box in allTrackedBoxes)
                {
                    boundingBoxes.Add(box);
                }

                // 추적이 성공적으로 완료되면 Entry 프레임의 사용자가 지정한 초기 박스 삭제
                if (allTrackedBoxes.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[추적 완료] Entry 프레임의 초기 박스 {startBoxes.Count}개 삭제 중...");
                    
                    foreach (var startBox in startBoxes)
                    {
                        if (boundingBoxes.Contains(startBox))
                        {
                            boundingBoxes.Remove(startBox);
                            System.Diagnostics.Debug.WriteLine($"  - 삭제: {startBox.Label}_{GetBoxId(startBox):D2} (Frame: {startBox.FrameIndex})");
                        }
                    }
                    
                    AddUndoAction(new UndoAction { Type = UndoActionType.Tracking, TrackedBoxes = allTrackedBoxes });
                }

                UpdateBoxCount();
                UpdateBboxListDisplay();
                MessageBox.Show(
                    $"추적 완료! {startBoxes.Count}개 객체, 총 {allTrackedBoxes.Count}개 BBox가 추가되었습니다.",
                    "성공",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                LoadFrame(waypoint.ExitFrame);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"추적 중 오류 발생: {ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        #endregion

        #region JSON Load/Export
        private void LoadLabelingData(string videoFilePath)
        {
            try
            {
                string videoDir = Path.GetDirectoryName(videoFilePath);
                if (string.IsNullOrEmpty(videoDir) || !Directory.Exists(videoDir))
                    return;

                string saveDir = Path.Combine(videoDir, "labels");
                if (!Directory.Exists(saveDir))
                    return;

                string fileName = Path.GetFileNameWithoutExtension(videoFilePath) + "_labels.json";
                string loadPath = Path.Combine(saveDir, fileName);

                if (!File.Exists(loadPath))
                    return;

                string json = File.ReadAllText(loadPath);
                var labelingData = JsonConvert.DeserializeObject<LabelingDataExtended>(json);

                if (labelingData == null || labelingData.Annotations == null)
                    return;

                boundingBoxes.Clear();
                categoryMap.Clear();
                selectedBox = null;
                nextAnnotationId = 1;
                // ID는 수동 지정 방식으로 변경됨: 별도 초기화 불필요

                // ImageId → FrameNumber 매핑 생성
                var imageIdToFrameNumber = new Dictionary<int, int>();
                if (labelingData.Images != null)
                {
                    foreach (var image in labelingData.Images)
                    {
                        imageIdToFrameNumber[image.Id] = image.FrameNumber;
                    }
                }

                if (labelingData.Categories != null)
                {
                    foreach (var category in labelingData.Categories)
                    {
                        categoryMap[category.Id] = category;
                    }
                }

                foreach (var annotation in labelingData.Annotations)
                {
                    if (annotation.Bbox == null || annotation.Bbox.Length < 4)
                        continue;

                    int trackId = annotation.TrackId;
                    string label = "person";

                    // CategoryId 범위로 라벨 결정 (더 정확함)
                    int catId = annotation.CategoryId;
                    if (catId >= 1 && catId <= 14)
                    {
                        label = "person";
                    }
                    else if (catId >= 15 && catId <= 18)
                    {
                        label = "vehicle";
                    }
                    else if (catId >= 19 && catId <= 24)
                    {
                        label = "event";
                    }
                    else if (categoryMap.ContainsKey(catId))
                    {
                        // fallback: 카테고리 이름으로 판단
                        string categoryName = categoryMap[catId].Name;
                        if (categoryName.Contains("car") || categoryName.Contains("motorcycle") || 
                            categoryName.Contains("scooter") || categoryName.Contains("bicycle"))
                            label = "vehicle";
                        else if (categoryName.Contains("contact") || categoryName.Contains("close") || 
                                 categoryName.Contains("signal") || categoryName.Contains("board") ||
                                 categoryName.Contains("final") || categoryName.Contains("TURN"))
                            label = "event";
                        else if (categoryName.StartsWith("person"))
                            label = "person";
                    }

                    // ImageId로 실제 프레임 번호 찾기
                    int frameNumber = annotation.ImageId; // 기본값
                    if (imageIdToFrameNumber.ContainsKey(annotation.ImageId))
                    {
                        frameNumber = imageIdToFrameNumber[annotation.ImageId];
                    }

                    var box = new BoundingBox
                    {
                        FrameIndex = frameNumber, // 실제 프레임 번호 사용
                        Rectangle = new Rectangle(annotation.Bbox[0], annotation.Bbox[1], annotation.Bbox[2], annotation.Bbox[3]),
                        Label = label,
                        PersonId = label == "person" ? trackId : 0,
                        VehicleId = label == "vehicle" ? trackId : 0,
                        EventId = label == "event" ? trackId : 0,
                        Action = "waypoint"
                    };

                    boundingBoxes.Add(box);

                    if (annotation.Id >= nextAnnotationId)
                        nextAnnotationId = annotation.Id + 1;

                    // 웨이포인트 정보 복원 (multi-object waypoint 지원)
                    if (annotation.TrackInfo != null && 
                        annotation.TrackInfo.Entry != null && 
                        annotation.TrackInfo.Exit != null)
                    {
                        int entryFrame = annotation.TrackInfo.Entry.Frame;
                        int exitFrame = annotation.TrackInfo.Exit.Frame;

                        // 같은 entry/exit 프레임을 가진 웨이포인트가 있는지 확인 (multi-object)
                        bool waypointExists = waypointMarkers.Any(w => 
                            w.EntryFrame == entryFrame && 
                            w.ExitFrame == exitFrame);

                        if (!waypointExists)
                        {
                            // Entry 프레임에 몇 개의 객체가 있는지 확인
                            int objectCount = boundingBoxes.Count(b => b.FrameIndex == entryFrame);
                            
                            var waypoint = new WaypointMarker
                            {
                                ObjectId = 0, // multi-object waypoint
                                Label = "multi",
                                EntryFrame = entryFrame,
                                ExitFrame = exitFrame,
                                EntryTime = FormatFrameTime(entryFrame),
                                ExitTime = FormatFrameTime(exitFrame),
                                MarkerColor = markerColors[waypointMarkers.Count % markerColors.Length]
                            };

                            waypointMarkers.Add(waypoint);
                        }
                    }
                }

                // 웨이포인트 리스트뷰 갱신
                UpdateWaypointListView();

                UpdateBoxCount();
                pictureBoxVideo.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"라벨링 데이터 로드 오류: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveCurrentLabelingData()
        {
            if (string.IsNullOrEmpty(currentVideoFile) || boundingBoxes.Count == 0)
                return;

            try
            {
                string videoDir = Path.GetDirectoryName(currentVideoFile);
                if (string.IsNullOrEmpty(videoDir) || !Directory.Exists(videoDir))
                {
                    MessageBox.Show("비디오 파일의 디렉토리를 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string saveDir = Path.Combine(videoDir, "labels");
                
                // 디렉토리 생성 시 예외 처리
                try
                {
                    if (!Directory.Exists(saveDir))
                    {
                        Directory.CreateDirectory(saveDir);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"라벨 저장 디렉토리 생성 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string fileName = Path.GetFileNameWithoutExtension(currentVideoFile) + "_labels.json";
                string savePath = Path.Combine(saveDir, fileName);

                ExportToJsonExtended(savePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"라벨링 데이터 저장 중 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportToJsonExtended(string filePath)
        {
            try
            {
                var images = new List<ImageInfo>();
                var annotations = new List<AnnotationData>();
                var categories = new Dictionary<int, CategoryData>();

                var frameGroups = boundingBoxes.GroupBy(b => b.FrameIndex).OrderBy(g => g.Key);
                int imageId = 0;

                foreach (var frameGroup in frameGroups)
                {
                    double frameSeconds = frameGroup.Key / fps;
                    DateTime frameTime = DateTime.Now.AddSeconds(frameSeconds);

                    // 자막에서 타임스탬프 추출 시도
                    string subtitleTimestamp = GetSubtitleTimestampForFrame(frameGroup.Key);
                    string timestamp = subtitleTimestamp ?? frameTime.ToString("yyyy-MM-ddTHH:mm:ss.fff");

                    var imageInfo = new ImageInfo
                    {
                        Id = imageId,
                        Height = (int)videoCapture.Get(VideoCaptureProperties.FrameHeight),
                        Width = (int)videoCapture.Get(VideoCaptureProperties.FrameWidth),
                        FrameNumber = frameGroup.Key,
                        Timestamp = timestamp
                    };

                    images.Add(imageInfo);

                    foreach (var box in frameGroup)
                    {
                        // 박스의 라벨 타입에 맞는 ID 가져오기
                        int boxId = GetBoxId(box);
                        
                        // 스펙에 맞는 Category ID와 Name 사용
                        int categoryId = GetCategoryId(box.Label, boxId);
                        string categoryName = GetCategoryName(box.Label, boxId);
                        
                        if (!categories.ContainsKey(categoryId))
                        {
                            categories[categoryId] = new CategoryData
                            {
                                Id = categoryId,
                                Name = categoryName,
                                Supercategory = box.Label
                            };
                        }

                        // Entry와 Exit 프레임 계산 (같은 라벨과 ID를 가진 박스들)
                        int entryFrame = boundingBoxes
                            .Where(b => b.Label == box.Label && GetBoxId(b) == boxId)
                            .Min(b => b.FrameIndex);
                        int exitFrame = boundingBoxes
                            .Where(b => b.Label == box.Label && GetBoxId(b) == boxId)
                            .Max(b => b.FrameIndex);

                        // 자막에서 타임스탬프 추출 시도
                        string entryTimestamp = GetSubtitleTimestampForFrame(entryFrame);
                        string exitTimestamp = GetSubtitleTimestampForFrame(exitFrame);

                        // 자막 타임스탬프가 없으면 계산된 시간 사용
                        double entrySeconds = entryFrame / fps;
                        double exitSeconds = exitFrame / fps;
                        DateTime entryTime = DateTime.Now.AddSeconds(entrySeconds);
                        DateTime exitTime = DateTime.Now.AddSeconds(exitSeconds);

                        var annotation = new AnnotationData
                        {
                            Id = nextAnnotationId++,
                            ImageId = imageId,
                            CategoryId = categoryId,
                            Bbox = new int[] { box.Rectangle.X, box.Rectangle.Y, box.Rectangle.Width, box.Rectangle.Height },
                            Area = box.Rectangle.Width * box.Rectangle.Height,
                            Iscrowd = 0,
                            TrackId = boxId,
                            TrackInfo = new TrackInfo
                            {
                                Entry = new TrackEntry
                                {
                                    Frame = entryFrame,
                                    Timestamp = entryTimestamp ?? entryTime.ToString("yyyy-MM-ddTHH:mm:ss.fff")
                                },
                                Exit = new TrackEntry
                                {
                                    Frame = exitFrame,
                                    Timestamp = exitTimestamp ?? exitTime.ToString("yyyy-MM-ddTHH:mm:ss.fff")
                                },
                                CurrentClipCount = 1
                            }
                        };

                        annotations.Add(annotation);
                    }

                    imageId++;
                }

                var labelingData = new LabelingDataExtended
                {
                    Info = new VideoInfoExtended
                    {
                        Description = "Extended COCO with Tracking",
                        Version = "1.0",
                        Year = DateTime.Now.Year,
                        DateCreated = DateTime.Now.ToString("yyyy-MM-dd"),
                        VideoFile = Path.GetFileName(currentVideoFile)
                    },
                    Licenses = new List<object>(),
                    Images = images,
                    Annotations = annotations,
                    Categories = categories.Values.ToList()
                };

                string json = JsonConvert.SerializeObject(labelingData, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"JSON 내보내기 오류: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Keyboard Shortcuts
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+1~14: ID 수동 지정 (영상 로드 여부와 무관하게 동작)
            if (e.Control && !e.Shift && !e.Alt)
            {
                int? assignedId = null;
                
                if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1) assignedId = 1;
                else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2) assignedId = 2;
                else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3) assignedId = 3;
                else if (e.KeyCode == Keys.D4 || e.KeyCode == Keys.NumPad4) assignedId = 4;
                else if (e.KeyCode == Keys.D5 || e.KeyCode == Keys.NumPad5) assignedId = 5;
                else if (e.KeyCode == Keys.D6 || e.KeyCode == Keys.NumPad6) assignedId = 6;
                else if (e.KeyCode == Keys.D7 || e.KeyCode == Keys.NumPad7) assignedId = 7;
                else if (e.KeyCode == Keys.D8 || e.KeyCode == Keys.NumPad8) assignedId = 8;
                else if (e.KeyCode == Keys.D9 || e.KeyCode == Keys.NumPad9) assignedId = 9;
                
                if (assignedId.HasValue)
                {
                    currentAssignedId = assignedId.Value;
                    MessageBox.Show($"{currentSelectedLabel} ID를 {currentAssignedId}로 설정했습니다.", 
                        "ID 설정", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Handled = true;
                    return;
                }
            }
            
            // Ctrl+Shift+1~5로 10~14 지정
            if (e.Control && e.Shift && !e.Alt)
            {
                int? assignedId = null;
                
                if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1) assignedId = 10;
                else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2) assignedId = 11;
                else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3) assignedId = 12;
                else if (e.KeyCode == Keys.D4 || e.KeyCode == Keys.NumPad4) assignedId = 13;
                else if (e.KeyCode == Keys.D5 || e.KeyCode == Keys.NumPad5) assignedId = 14;
                
                if (assignedId.HasValue)
                {
                    currentAssignedId = assignedId.Value;
                    MessageBox.Show($"{currentSelectedLabel} ID를 {currentAssignedId}로 설정했습니다.", 
                        "ID 설정", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Handled = true;
                    return;
                }
            }
            
            // 영상이 로드되지 않은 경우 키 이벤트 무시
            if (videoCapture == null || !videoCapture.IsOpened())
                return;

            // 모든 버튼이 TabStop = false이므로 포커스 문제 없음
            if (e.KeyCode == Keys.Space)
            {
                btnPlay_Click(sender, e);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Left)
            {
                // 5초씩 뒤로 이동 (fps * 5 프레임)
                int framesToMove = (int)(fps * 5);
                int newFrame = Math.Max(0, currentFrameIndex - framesToMove);
                LoadFrame(newFrame);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                // 5초씩 앞으로 이동 (fps * 5 프레임)
                int framesToMove = (int)(fps * 5);
                int newFrame = Math.Min(totalFrames - 1, currentFrameIndex + framesToMove);
                LoadFrame(newFrame);
                e.Handled = true;
            }
            else if (selectedBox != null && (e.KeyCode == Keys.W || e.KeyCode == Keys.A || e.KeyCode == Keys.S || e.KeyCode == Keys.D))
            {
                int moveAmount = e.Shift ? 10 : 2;
                Rectangle rect = selectedBox.Rectangle;

                switch (e.KeyCode)
                {
                    case Keys.W: rect.Y -= moveAmount; break;
                    case Keys.A: rect.X -= moveAmount; break;
                    case Keys.S: rect.Y += moveAmount; break;
                    case Keys.D: rect.X += moveAmount; break;
                }

                selectedBox.Rectangle = rect;
                pictureBoxVideo.Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.G && selectedBox != null)
            {
                AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(selectedBox) });
                boundingBoxes.Remove(selectedBox);
                selectedBox = null;
                UpdateBoxCount();
                pictureBoxVideo.Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete && selectedBox != null)
            {
                AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(selectedBox) });
                boundingBoxes.Remove(selectedBox);
                selectedBox = null;
                UpdateBoxCount();
                UpdateBboxListDisplay();
                pictureBoxVideo.Invalidate();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Z)
            {
                if (e.Shift)
                {
                    // Ctrl+Shift+Z는 이미 위에서 ID 설정으로 처리됨
                    // Redo는 Ctrl+Y로만 사용
                }
                else
                {
                    Undo();
                    e.Handled = true;
                }
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                Redo();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.E && !e.Control && !e.Alt)
            {
                SetEntryMarker();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.X && !e.Control && !e.Alt)
            {
                SetExitMarkerAndCreateWaypoint();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.T)
            {
                if (waypointMarkers.Count > 0)
                {
                    var lastWaypoint = waypointMarkers[waypointMarkers.Count - 1];

                    // YOLO 사용 여부 결정
                    bool useYolo = isYoloAvailable;

                    if (useYolo)
                    {
                        var result = MessageBox.Show(
                            "YOLO 추적을 사용하시겠습니까?\n\n" +
                            "[예] YOLO 기반 추적 (권장)\n" +
                            "[아니오] 기본 OpenCV 추적",
                            "추적 모드 선택",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        useYolo = (result == DialogResult.Yes);
                    }

                    PerformTrackingForWaypoint(lastWaypoint, useYolo);
                }
                else
                {
                    MessageBox.Show(
                        "추적을 위해서는:\n" +
                        "1. Entry 마커 설정 (E키)\n" +
                        "2. Exit 프레임으로 이동\n" +
                        "3. Exit 마커 설정 (X키)\n" +
                        "→ Waypoint가 생성됩니다\n" +
                        "4. Ctrl+T로 추적 시작",
                        "정보",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.D1 && !e.Control && !e.Alt)
            {
                btnSelectAll_Click(sender, e);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.D2 && !e.Control && !e.Alt)
            {
                btnEdit_Click(sender, e);
                e.Handled = true;
            }
            else if (selectedBox != null && e.Control && !e.Alt && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9)
            {
                int id = e.KeyCode - Keys.D0;
                AssignPersonId(id);
                e.Handled = true;
            }
            else if (selectedBox != null && e.Control && !e.Alt && e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad9)
            {
                int id = e.KeyCode - Keys.NumPad0;
                AssignPersonId(id);
                e.Handled = true;
            }
            else if (selectedBox != null && e.Alt && !e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9)
            {
                int id = (e.KeyCode - Keys.D0) + 10;
                AssignPersonId(id);
                e.Handled = true;
            }
            else if (selectedBox != null && e.Alt && !e.Control && e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad9)
            {
                int id = (e.KeyCode - Keys.NumPad0) + 10;
                AssignPersonId(id);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                playbackSpeed = 1.0;
                if (isPlaying)
                    lastFrameTime = DateTime.Now.Ticks / 10000;
                UpdateTimeLabels();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                playbackSpeed = 4.0;
                if (isPlaying)
                    lastFrameTime = DateTime.Now.Ticks / 10000;
                UpdateTimeLabels();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.D)
            {
                playbackSpeed = 8.0;
                if (isPlaying)
                    lastFrameTime = DateTime.Now.Ticks / 10000;
                UpdateTimeLabels();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.F)
            {
                playbackSpeed = 16.0;
                if (isPlaying)
                    lastFrameTime = DateTime.Now.Ticks / 10000;
                UpdateTimeLabels();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                selectedBox = null;
                pictureBoxVideo.Invalidate();
                e.Handled = true;
            }
        }

        private void AssignPersonId(int id)
        {
            if (selectedBox == null)
                return;

            int oldPersonId = GetBoxId(selectedBox);
            string oldLabel = selectedBox.Label;
            Rectangle oldRect = selectedBox.Rectangle;

            SetBoxId(selectedBox, selectedBox.Label, id);

            AddUndoAction(new UndoAction
            {
                Type = UndoActionType.ModifyBox,
                Box = CloneBoundingBox(selectedBox),
                OriginalObjectId = oldPersonId,
                OriginalLabel = oldLabel,
                OriginalRectangle = oldRect
            });

            labelObjectLabel.Text = $"Label: {selectedBox.Label}_{id:D2}";
            pictureBoxVideo.Invalidate();

            currentMode = DrawMode.Draw;
            btnEdit.BackColor = Color.FromArgb(59, 130, 246);
            btnSelectAll.BackColor = SystemColors.Control;
            pictureBoxVideo.Cursor = Cursors.Cross;

            selectedBox = null;
        }
        #endregion

        #region SRT Subtitle Extraction
        private async Task<bool> ExtractSrtFromVideo(string videoPath)
        {
            // FFmpeg가 없으면 자막 추출 건너뛰기
            if (!isFFmpegAvailable)
            {
                return false;
            }

            try
            {
                string videoDir = Path.GetDirectoryName(videoPath);
                string videoName = Path.GetFileNameWithoutExtension(videoPath);
                string srtPath = Path.Combine(videoDir, $"{videoName}.srt");

                // 기존 SRT 파일이 있으면 삭제
                if (File.Exists(srtPath))
                {
                    File.Delete(srtPath);
                }

                // FFmpeg를 사용하여 자막 추출 (비디오 내부에 포함된 자막 스트림)
                var ffTask = FFMpegArguments
                    .FromFileInput(videoPath)
                    .OutputToFile(srtPath, true, options => options
                        .WithCustomArgument("-map 0:s:0")  // 첫 번째 자막 스트림 선택
                        .WithCustomArgument("-c:s srt")    // SRT 형식으로 출력
                    )
                    .ProcessAsynchronously();

                var result = await ffTask;

                if (result && File.Exists(srtPath))
                {
                    currentSrtFile = srtPath;
                    await LoadSrtFile(srtPath);
                    return true;
                }
                else
                {
                    // 자막 스트림이 없는 경우 (정상 상황)
                    return false;
                }
            }
            catch (Exception ex)
            {
                // 자막이 없거나 추출 실패 시 조용히 실패
                System.Diagnostics.Debug.WriteLine($"자막 추출 실패: {ex.Message}");
                return false;
            }
        }

        private async Task LoadSrtFile(string srtPath)
        {
            try
            {
                subtitleEntries.Clear();
                string[] lines = await File.ReadAllLinesAsync(srtPath);

                for (int i = 0; i < lines.Length; i++)
                {
                    // 빈 줄 건너뛰기
                    if (string.IsNullOrWhiteSpace(lines[i]))
                        continue;

                    // 인덱스 번호 확인
                    if (int.TryParse(lines[i], out int index))
                    {
                        i++; // 다음 줄로 이동

                        // 시간 정보 파싱
                        if (i < lines.Length && lines[i].Contains("-->"))
                        {
                            string[] timeParts = lines[i].Split(new[] { " --> " }, StringSplitOptions.None);
                            if (timeParts.Length == 2)
                            {
                                TimeSpan startTime = ParseSrtTime(timeParts[0]);
                                TimeSpan endTime = ParseSrtTime(timeParts[1]);

                                i++; // 다음 줄로 이동

                                // 자막 텍스트 수집
                                List<string> textLines = new List<string>();
                                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                                {
                                    textLines.Add(lines[i]);
                                    i++;
                                }

                                if (textLines.Count > 0)
                                {
                                    subtitleEntries.Add(new SubtitleEntry
                                    {
                                        Index = index,
                                        StartTime = startTime,
                                        EndTime = endTime,
                                        Text = string.Join(" ", textLines)
                                    });
                                }
                            }
                        }
                    }
                }

                MessageBox.Show($"자막 파일을 성공적으로 로드했습니다.\n총 {subtitleEntries.Count}개의 자막 항목을 찾았습니다.", 
                    "자막 로드 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"자막 파일 로드 중 오류가 발생했습니다:\n{ex.Message}", 
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private TimeSpan ParseSrtTime(string timeString)
        {
            // SRT 시간 형식: 00:00:00,000
            string[] parts = timeString.Split(',');
            if (parts.Length == 2)
            {
                string[] timeParts = parts[0].Split(':');
                if (timeParts.Length == 3)
                {
                    int hours = int.Parse(timeParts[0]);
                    int minutes = int.Parse(timeParts[1]);
                    int seconds = int.Parse(timeParts[2]);
                    int milliseconds = int.Parse(parts[1]);

                    return new TimeSpan(0, hours, minutes, seconds, milliseconds);
                }
            }
            return TimeSpan.Zero;
        }

        private string GetCurrentSubtitle()
        {
            if (subtitleEntries.Count == 0)
                return "";

            double currentSeconds = currentFrameIndex / fps;
            TimeSpan currentTime = TimeSpan.FromSeconds(currentSeconds);

            var currentSubtitle = subtitleEntries.FirstOrDefault(s => 
                currentTime >= s.StartTime && currentTime <= s.EndTime);

            return currentSubtitle?.Text ?? "";
        }

        // 자막에서 날짜-시간 형식(YYYY-MM-DD HH:mm:ss) 추출
        private string ExtractTimestampFromSubtitle(string subtitleText)
        {
            if (string.IsNullOrEmpty(subtitleText))
                return null;

            // 정규식 패턴: YYYY-MM-DD HH:mm:ss
            var regex = new System.Text.RegularExpressions.Regex(@"\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}");
            var match = regex.Match(subtitleText);

            if (match.Success)
            {
                // ISO 8601 형식으로 변환 (YYYY-MM-DDTHH:mm:ss)
                string timestamp = match.Value.Replace(" ", "T");
                return timestamp;
            }

            return null;
        }

        // 특정 프레임의 자막 타임스탬프 가져오기
        private string GetSubtitleTimestampForFrame(int frameIndex)
        {
            if (subtitleEntries.Count == 0)
                return null;

            double frameSeconds = frameIndex / fps;
            TimeSpan frameTime = TimeSpan.FromSeconds(frameSeconds);

            var subtitle = subtitleEntries.FirstOrDefault(s => 
                frameTime >= s.StartTime && frameTime <= s.EndTime);

            if (subtitle != null)
            {
                return ExtractTimestampFromSubtitle(subtitle.Text);
            }

            return null;
        }
        #endregion

        #region Coordinate Transformation
        // PictureBox의 Zoom 모드에서 실제 이미지가 표시되는 영역 계산
        private RectangleF GetImageDisplayRectangle()
        {
            if (pictureBoxVideo.Image == null)
                return RectangleF.Empty;

            float imageAspect = (float)pictureBoxVideo.Image.Width / pictureBoxVideo.Image.Height;
            float controlAspect = (float)pictureBoxVideo.Width / pictureBoxVideo.Height;

            float renderWidth, renderHeight;
            float renderX = 0, renderY = 0;

            if (imageAspect > controlAspect)
            {
                // 이미지가 더 넓음 - 좌우에 맞춤
                renderWidth = pictureBoxVideo.Width;
                renderHeight = pictureBoxVideo.Width / imageAspect;
                renderY = (pictureBoxVideo.Height - renderHeight) / 2f;
            }
            else
            {
                // 이미지가 더 높음 - 상하에 맞춤
                renderHeight = pictureBoxVideo.Height;
                renderWidth = pictureBoxVideo.Height * imageAspect;
                renderX = (pictureBoxVideo.Width - renderWidth) / 2f;
            }

            return new RectangleF(renderX, renderY, renderWidth, renderHeight);
        }

        // 이미지 좌표 → 뷰(PictureBox) 좌표 변환
        private PointF ImageToView(PointF imagePoint)
        {
            if (pictureBoxVideo.Image == null)
                return imagePoint;

            var displayRect = GetImageDisplayRectangle();
            
            float scaleX = displayRect.Width / pictureBoxVideo.Image.Width;
            float scaleY = displayRect.Height / pictureBoxVideo.Image.Height;

            return new PointF(
                displayRect.X + imagePoint.X * scaleX,
                displayRect.Y + imagePoint.Y * scaleY
            );
        }

        private RectangleF ImageToView(RectangleF imageRect)
        {
            var topLeft = ImageToView(new PointF(imageRect.X, imageRect.Y));
            var bottomRight = ImageToView(new PointF(imageRect.Right, imageRect.Bottom));
            
            return new RectangleF(
                topLeft.X,
                topLeft.Y,
                bottomRight.X - topLeft.X,
                bottomRight.Y - topLeft.Y
            );
        }

        // 뷰(PictureBox) 좌표 → 이미지 좌표 변환
        private PointF ViewToImage(PointF viewPoint)
        {
            if (pictureBoxVideo.Image == null)
                return viewPoint;

            var displayRect = GetImageDisplayRectangle();
            
            float scaleX = pictureBoxVideo.Image.Width / displayRect.Width;
            float scaleY = pictureBoxVideo.Image.Height / displayRect.Height;

            return new PointF(
                (viewPoint.X - displayRect.X) * scaleX,
                (viewPoint.Y - displayRect.Y) * scaleY
            );
        }

        private RectangleF ViewToImage(RectangleF viewRect)
        {
            var topLeft = ViewToImage(new PointF(viewRect.X, viewRect.Y));
            var bottomRight = ViewToImage(new PointF(viewRect.Right, viewRect.Bottom));
            
            return new RectangleF(
                topLeft.X,
                topLeft.Y,
                bottomRight.X - topLeft.X,
                bottomRight.Y - topLeft.Y
            );
        }
        #endregion

        #region Helper Methods
        private BoundingBox CloneBoundingBox(BoundingBox box)
        {
            return new BoundingBox
            {
                FrameIndex = box.FrameIndex,
                Rectangle = new Rectangle(box.Rectangle.Location, box.Rectangle.Size),
                Label = box.Label,
                PersonId = box.PersonId,
                VehicleId = box.VehicleId,
                EventId = box.EventId,
                Action = box.Action,
                VehicleName = box.VehicleName,
                EventName = box.EventName
            };
        }
        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // 프로그램 종료 시 자동 저장 제거 - 수동으로만 저장

            if (videoCapture != null)
            {
                videoCapture.Release();
                videoCapture.Dispose();
            }

            if (currentFrame != null)
                currentFrame.Dispose();

            // YOLO 엔진 정리
            if (trackingEngine is YoloTrackingEngine yoloEngine)
                yoloEngine.Dispose();

            if (videoListForm != null && !videoListForm.IsDisposed)
                videoListForm.Dispose();
        }
    }

    #region Legacy JSON Classes (호환성 유지)

    public class LabelingData
    {
        [JsonProperty("info")] public VideoInfo Info { get; set; }
        [JsonProperty("categories")] public List<Category> Categories { get; set; }
        [JsonProperty("items")] public List<Item> Items { get; set; }
    }

    public class VideoInfo
    {
        [JsonProperty("video_file")] public string VideoFile { get; set; }
        [JsonProperty("date_created")] public string DateCreated { get; set; }
        [JsonProperty("cctv_id")] public string CctvId { get; set; }
    }

    public class Category
    {
        [JsonProperty("labels")] public List<LabelDef> Labels { get; set; }
    }

    public class LabelDef
    {
        [JsonProperty("name")] public string Name { get; set; }
    }

    public class Item
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("annotations")] public List<Annotation> Annotations { get; set; }
    }

    public class Annotation
    {
        [JsonProperty("image_id")] public int ImageId { get; set; }
        [JsonProperty("attributes")] public Dictionary<string, object> Attributes { get; set; }
        [JsonProperty("label_id")] public int LabelId { get; set; }
        [JsonProperty("attr")] public Dictionary<string, object> Attr { get; set; }
    }

    #endregion
}