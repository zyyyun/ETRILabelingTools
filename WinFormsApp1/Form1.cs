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
            
            var trackedBoxes = new List<BoundingBox>();
            videoCapture.Set(VideoCaptureProperties.PosFrames, startFrame);
            Mat frame = new Mat();

            // 이전 프레임 박스 초기화: 사용자 지정 startBox로 시작
            Rectangle previousRect = startBox.Rectangle;
            string fixedLabel = startBox.Label;
            int fixedIdPerson = startBox.PersonId;
            int fixedIdVehicle = startBox.VehicleId;
            int fixedIdEvent = startBox.EventId;

            for (int i = startFrame; i <= endFrame; i++)
            {
                if (!videoCapture.Read(frame) || frame.Empty())
                    break;

                try
                {
                    // Mat을 임시 파일로 저장 (YoloSharp는 파일 입력을 요구)
                    Cv2.ImWrite(_tempImagePath, frame);

                    var detections = _predictor.Detect(_tempImagePath);

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
        private WaypointMarker selectedWaypoint = null; // ✅ 선택된 Waypoint 추적
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

        // ✅ 창 이동 및 크기 조절 관련
        private bool isMovingWindow = false;
        private System.Drawing.Point windowMoveStartPoint;
        private const int RESIZE_BORDER_WIDTH = 8; // 크기 조절 감지 영역 너비


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
        private WaypointMarker? lastRenderedWaypoint = null;
        
        // Labels 패널 접기/펼치기 상태
        private bool isPersonExpanded = false;
        private bool isVehicleExpanded = false;
        private bool isEventExpanded = false;
        
        // 성능 최적화: 프레임별 박스 캐시
        private Dictionary<int, List<BoundingBox>> frameBoxCache = new Dictionary<int, List<BoundingBox>>();
        private int lastCachedFrameForPaint = -1;
        private List<BoundingBox> cachedCurrentFrameBoxes = new List<BoundingBox>();
        
        // 성능 최적화: 재사용 가능한 Font 객체
        private Font labelFont = new Font("Segoe UI", 10F, FontStyle.Bold);

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

            // ✅ Timeline 패널에 더블 버퍼링 활성화 (깜빡임 방지)
            EnableDoubleBuffering(panelTimeline);

            // ✅ 헤더 드래그로 창 이동 기능 활성화
            SetupWindowDragHandlers();
        }

        /// <summary>
        /// Panel에 더블 버퍼링을 활성화하는 헬퍼 메서드
        /// </summary>
        private void EnableDoubleBuffering(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic,
                null, control, new object[] { true });
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

            // ✅ 창 상태 변경 시 최대화/복원 버튼 아이콘 업데이트
            this.Resize += Form1_Resize;
            UpdateMaximizeButtonIcon();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            UpdateMaximizeButtonIcon();
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
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
            }
        }

        private void btnMinimize_Click(object sender, EventArgs e) => this.WindowState = FormWindowState.Minimized;

        /// <summary>
        /// 최대화/복원 버튼 아이콘 업데이트
        /// </summary>
        private void UpdateMaximizeButtonIcon()
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                btnMaximize.Text = "❐"; // 복원 아이콘
            }
            else
            {
                btnMaximize.Text = "□"; // 최대화 아이콘
            }
        }

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
                // ✅ 저장 전 모든 웨이포인트의 Event 박스 자동 전파
                foreach (var waypoint in waypointMarkers)
                {
                    // Entry 프레임에 Event 박스가 있는지 확인
                    var eventBoxesAtEntry = boundingBoxes
                        .Where(b => b.FrameIndex == waypoint.EntryFrame && b.Label == "event")
                        .ToList();
                    
                    if (eventBoxesAtEntry.Count > 0)
                    {
                        PropagateAllEventBoxesInRange(waypoint.EntryFrame, waypoint.ExitFrame);
                    }
                }
                
                InvalidateBoxCache();
                UpdateBoxCount();

                
                // 비동기로 저장 작업 수행
                await Task.Run(() => SaveCurrentLabelingData());
                
                loadingForm.Close();

                // ✅ JSON 저장 후 자동 재로드
                string videoDir = Path.GetDirectoryName(currentVideoFile);
                string labelsDir = Path.Combine(videoDir, "labels");
                string fileName = Path.GetFileNameWithoutExtension(currentVideoFile) + "_labels.json";
                string jsonFilePath = Path.Combine(labelsDir, fileName);
                
                if (File.Exists(jsonFilePath))
                {
                    LoadLabelingData(currentVideoFile); // JSON 재로드
                }
                
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

                // ✅ 폴더 선택 시: 데이터 백업 → 초기화 → 로드 (실패 시 복원)
                var backupBoxes = new List<BoundingBox>(boundingBoxes);
                var backupWaypoints = new List<WaypointMarker>(waypointMarkers);
                var backupSelectedBox = selectedBox;
                var backupUndoStack = new Stack<UndoAction>(undoStack.Reverse());
                var backupRedoStack = new Stack<UndoAction>(redoStack.Reverse());
                
                boundingBoxes.Clear();
                waypointMarkers.Clear();
                selectedBox = null;
                undoStack.Clear();
                redoStack.Clear();
                lastRenderedWaypoint = null;
                
                try
                {
                    currentVideoIndex = 0;
                    await LoadVideoWithSubtitle(videoFileList[0]);
                    
                    // LoadVideoWithSubtitle 내부의 LoadLabelingData에서 새 데이터가 로드됨
                    UpdateBoxCount();
                    UpdateWaypointListView();
                    pictureBoxVideo.Invalidate();

                    MessageBox.Show($"총 {videoFileList.Count}개의 영상 파일을 불러왔습니다.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    // ❌ 로드 실패 시 이전 데이터 복원
                    boundingBoxes.Clear();
                    boundingBoxes.AddRange(backupBoxes);
                    waypointMarkers.Clear();
                    waypointMarkers.AddRange(backupWaypoints);
                    selectedBox = backupSelectedBox;
                    undoStack.Clear();
                    foreach (var action in backupUndoStack) undoStack.Push(action);
                    redoStack.Clear();
                    foreach (var action in backupRedoStack) redoStack.Push(action);
                    
                    UpdateBoxCount();
                    UpdateWaypointListView();
                    pictureBoxVideo.Invalidate();
                    
                    MessageBox.Show($"영상 로드 실패:\n{ex.Message}\n\n이전 작업 내용이 복원되었습니다.", 
                        "폴더 로드 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
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
                
                // ✅ 영상 로드 후 자동 재생 시작
                if (!isPlaying)
                {
                    btnPlay_Click(null, EventArgs.Empty);
                }
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
            
            // x264 대신 실제 재생 속도(1.0x, 2.0x 등)로 표기
            string speedInfo = $"{playbackSpeed:0.##}x";
            if (!string.IsNullOrEmpty(subtitleText))
            {
                labelTimeInfo.Text = $"{currentTime:hh\\:mm\\:ss} / {totalTime:hh\\:mm\\:ss} {speedInfo}\n자막: {subtitleText}";
            }
            else
            {
                labelTimeInfo.Text = $"{currentTime:hh\\:mm\\:ss} / {totalTime:hh\\:mm\\:ss} {speedInfo}";
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
            // E키와 동일한 기능: Entry 마커 설정
            SetEntryMarker();
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
            // X키와 동일한 기능: Exit 마커 설정 및 Waypoint 생성
            SetExitMarkerAndCreateWaypoint();
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

            // ✅ Exit 프레임이 Entry 프레임보다 앞에 있으면 막기
            if (currentFrameIndex <= entryFrameIndex.Value)
            {
                TimeSpan currentTimeCheck = TimeSpan.FromSeconds(currentFrameIndex / fps);
                TimeSpan entryTimeCheck = TimeSpan.FromSeconds(entryFrameIndex.Value / fps);
                
                MessageBox.Show(
                    $"Exit 프레임은 Entry 프레임보다 뒤에 있어야 합니다.\n\n" +
                    $"Entry: {entryTimeCheck:hh\\:mm\\:ss} (프레임 {entryFrameIndex.Value})\n" +
                    $"현재: {currentTimeCheck:hh\\:mm\\:ss} (프레임 {currentFrameIndex})\n\n" +
                    $"Entry 프레임 이후로 이동한 후 Exit를 설정해주세요.",
                    "Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Entry 프레임의 Person 또는 Vehicle 박스 찾기
            var entryPersonBoxes = boundingBoxes.Where(b => b.FrameIndex == entryFrameIndex.Value && b.Label == "person").ToList();
            var entryVehicleBoxes = boundingBoxes.Where(b => b.FrameIndex == entryFrameIndex.Value && b.Label == "vehicle").ToList();
            
            if (entryPersonBoxes.Count == 0 && entryVehicleBoxes.Count == 0)
            {
                MessageBox.Show("Entry 프레임에 Person 또는 Vehicle 박스가 없습니다.\n박스를 그린 후 X키를 눌러주세요.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            exitFrameIndex = currentFrameIndex;
            TimeSpan exitTime = TimeSpan.FromSeconds(currentFrameIndex / fps);
            TimeSpan entryTime = TimeSpan.FromSeconds(entryFrameIndex.Value / fps);

            btnExit.Text = $"Exit: {exitTime:hh\\:mm\\:ss}";

            // ✅ 생성된 Waypoint 리스트
            List<WaypointMarker> createdWaypoints = new List<WaypointMarker>();

            // ✅ 1. Person 박스들에 대해 각각 개별 Waypoint 생성
            foreach (var personBox in entryPersonBoxes)
            {
                int personId = personBox.PersonId;
                
                var waypoint = new WaypointMarker
                {
                    EntryFrame = entryFrameIndex.Value,
                    ExitFrame = exitFrameIndex.Value,
                    MarkerColor = System.Drawing.Color.FromArgb(255, 107, 107), // 빨강
                    EntryTime = entryTime.ToString(@"hh\:mm\:ss"),
                    ExitTime = exitTime.ToString(@"hh\:mm\:ss"),
                    ObjectId = personId,
                    Label = "person"
                };
                
                waypointMarkers.Add(waypoint);
                createdWaypoints.Add(waypoint);
                System.Diagnostics.Debug.WriteLine($"[Waypoint 생성] Person ID={personId}, {entryFrameIndex.Value}~{exitFrameIndex.Value}");
            }

            // ✅ 2. Vehicle 박스들에 대해 각각 개별 Waypoint 생성
            foreach (var vehicleBox in entryVehicleBoxes)
            {
                int vehicleId = vehicleBox.VehicleId;
                
                var waypoint = new WaypointMarker
                {
                    EntryFrame = entryFrameIndex.Value,
                    ExitFrame = exitFrameIndex.Value,
                    MarkerColor = System.Drawing.Color.FromArgb(107, 158, 255), // 파랑
                    EntryTime = entryTime.ToString(@"hh\:mm\:ss"),
                    ExitTime = exitTime.ToString(@"hh\:mm\:ss"),
                    ObjectId = vehicleId,
                    Label = "vehicle"
                };
                
                waypointMarkers.Add(waypoint);
                createdWaypoints.Add(waypoint);
                System.Diagnostics.Debug.WriteLine($"[Waypoint 생성] Vehicle ID={vehicleId}, {entryFrameIndex.Value}~{exitFrameIndex.Value}");
            }

            // ✅ 3. UI 업데이트 및 Entry/Exit 초기화
            UpdateWaypointListView();
            
            entryFrameIndex = null;
            exitFrameIndex = null;
            btnEntry.Text = "Entry";
            btnExit.Text = "Exit";
            
            panelTimeline.Invalidate();

            // ✅ 4. 자동 추적 확인 (생성된 Waypoint가 있을 때만)
            if (createdWaypoints.Count > 0)
            {
                string summary = $"{createdWaypoints.Count}개의 Waypoint가 생성되었습니다.\n" +
                                $"(Person: {entryPersonBoxes.Count}개, Vehicle: {entryVehicleBoxes.Count}개)";
                
                var result = MessageBox.Show(
                    $"{summary}\n\n자동 추적을 수행하시겠습니까?",
                    "Waypoint 생성 완료",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    // ✅ 순차적으로 추적 실행 (동시 실행으로 인한 충돌 방지)
                    PerformSequentialTracking(createdWaypoints);
                }
            }
        }

        private void listViewPersonWaypoints_Click(object sender, EventArgs e)
        {
            // Person Waypoint 항목을 클릭하면 해당 Entry 프레임으로 이동
            if (listViewPersonWaypoints.SelectedItems.Count > 0)
            {
                var selectedItem = listViewPersonWaypoints.SelectedItems[0];
                var waypoint = selectedItem.Tag as WaypointMarker;

                if (waypoint != null)
                {
                    selectedWaypoint = waypoint; // ✅ 선택된 waypoint 저장
                    panelTimeline.Invalidate(); // ✅ Timeline 다시 그리기
                    LoadFrame(waypoint.EntryFrame);
                }
            }
            else
            {
                selectedWaypoint = null; // ✅ 선택 해제
                panelTimeline.Invalidate();
            }
        }

        private void listViewVehicleWaypoints_Click(object sender, EventArgs e)
        {
            // Vehicle Waypoint 항목을 클릭하면 해당 프레임으로 이동 (단일 프레임)
            if (listViewVehicleWaypoints.SelectedItems.Count > 0)
            {
                var selectedItem = listViewVehicleWaypoints.SelectedItems[0];
                var waypoint = selectedItem.Tag as WaypointMarker;

                if (waypoint != null)
                {
                    selectedWaypoint = waypoint; // ✅ 선택된 waypoint 저장
                    panelTimeline.Invalidate(); // ✅ Timeline 다시 그리기
                    LoadFrame(waypoint.EntryFrame); // Entry = Exit (단일 프레임)
                }
            }
            else
            {
                selectedWaypoint = null; // ✅ 선택 해제
                panelTimeline.Invalidate();
            }
        }

        private void listViewEventWaypoints_Click(object sender, EventArgs e)
        {
            // Event Waypoint 항목을 클릭하면 해당 Entry 프레임으로 이동
            if (listViewEventWaypoints.SelectedItems.Count > 0)
            {
                var selectedItem = listViewEventWaypoints.SelectedItems[0];
                var waypoint = selectedItem.Tag as WaypointMarker;

                if (waypoint != null)
                {
                    selectedWaypoint = waypoint; // ✅ 선택된 waypoint 저장
                    panelTimeline.Invalidate(); // ✅ Timeline 다시 그리기
                    LoadFrame(waypoint.EntryFrame);
                }
            }
            else
            {
                selectedWaypoint = null; // ✅ 선택 해제
                panelTimeline.Invalidate();
            }
        }

        private void btnDeletePersonWaypoint_Click(object sender, EventArgs e)
        {
            if (listViewPersonWaypoints.SelectedItems.Count == 0)
            {
                MessageBox.Show("삭제할 Person Waypoint를 선택해주세요.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = listViewPersonWaypoints.SelectedItems[0];
            var waypoint = selectedItem.Tag as WaypointMarker;

            if (waypoint != null)
            {
                var result = MessageBox.Show(
                    $"선택한 Person Waypoint를 삭제하시겠습니까?\n\n" +
                    $"Entry: {waypoint.EntryTime}\n" +
                    $"Exit: {waypoint.ExitTime}\n\n" +
                    $"⚠️ 주의: 해당 구간의 Person 박스가 삭제됩니다.",
                    "Person Waypoint 삭제 확인",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;

                // ✅ Person 박스만 삭제 (ObjectId로 정확히 필터링)
                var boxesToDelete = boundingBoxes
                    .Where(b => 
                        b.Label == "person" &&
                        b.PersonId == waypoint.ObjectId &&
                        b.FrameIndex >= waypoint.EntryFrame && 
                        b.FrameIndex <= waypoint.ExitFrame)
                    .ToList();

                foreach (var box in boxesToDelete)
                {
                    AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(box) });
                    boundingBoxes.Remove(box);
                }

                if (selectedBox != null && boxesToDelete.Contains(selectedBox))
                    selectedBox = null;

                waypointMarkers.Remove(waypoint);
                listViewPersonWaypoints.Items.Remove(selectedItem);
                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();
                panelTimeline.Invalidate();
                pictureBoxVideo.Invalidate();

                MessageBox.Show(
                    "✅ Person Waypoint가 삭제되었습니다.\n\n" +
                    $"삭제된 박스: {boxesToDelete.Count}개",
                    "삭제 완료",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void btnDeleteVehicleWaypoint_Click(object sender, EventArgs e)
        {
            if (listViewVehicleWaypoints.SelectedItems.Count == 0)
            {
                MessageBox.Show("삭제할 Vehicle Waypoint를 선택해주세요.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = listViewVehicleWaypoints.SelectedItems[0];
            var waypoint = selectedItem.Tag as WaypointMarker;

            if (waypoint != null)
            {
                var result = MessageBox.Show(
                    $"선택한 Vehicle Waypoint를 삭제하시겠습니까?\n\n" +
                    $"프레임: {waypoint.EntryTime}\n\n" +
                    $"⚠️ 주의: 해당 프레임의 Vehicle 박스가 삭제됩니다.",
                    "Vehicle Waypoint 삭제 확인",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;

                // ✅ Vehicle 박스만 삭제 (ObjectId로 정확히 필터링)
                var boxesToDelete = boundingBoxes
                    .Where(b => 
                        b.Label == "vehicle" &&
                        b.VehicleId == waypoint.ObjectId &&
                        b.FrameIndex == waypoint.EntryFrame)
                    .ToList();

                foreach (var box in boxesToDelete)
                {
                    AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(box) });
                    boundingBoxes.Remove(box);
                }

                if (selectedBox != null && boxesToDelete.Contains(selectedBox))
                    selectedBox = null;

                waypointMarkers.Remove(waypoint);
                listViewVehicleWaypoints.Items.Remove(selectedItem);
                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();
                panelTimeline.Invalidate();
                pictureBoxVideo.Invalidate();

                MessageBox.Show(
                    "✅ Vehicle Waypoint가 삭제되었습니다.\n\n" +
                    $"삭제된 박스: {boxesToDelete.Count}개",
                    "삭제 완료",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void btnDeleteEventWaypoint_Click(object sender, EventArgs e)
        {
            if (listViewEventWaypoints.SelectedItems.Count == 0)
            {
                MessageBox.Show("삭제할 Event Waypoint를 선택해주세요.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = listViewEventWaypoints.SelectedItems[0];
            var waypoint = selectedItem.Tag as WaypointMarker;

            if (waypoint != null)
            {
                var result = MessageBox.Show(
                    $"선택한 Event Waypoint를 삭제하시겠습니까?\n\n" +
                    $"Entry: {waypoint.EntryTime}\n" +
                    $"Exit: {waypoint.ExitTime}\n\n" +
                    $"⚠️ 주의: 해당 구간의 Event 박스가 삭제됩니다.",
                    "Event Waypoint 삭제 확인",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;

                // ✅ Event 박스만 삭제 (ObjectId로 정확히 필터링)
                var boxesToDelete = boundingBoxes
                    .Where(b => 
                        b.Label == "event" &&
                        b.EventId == waypoint.ObjectId &&
                        b.FrameIndex >= waypoint.EntryFrame && 
                        b.FrameIndex <= waypoint.ExitFrame)
                    .ToList();

                foreach (var box in boxesToDelete)
                {
                    AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(box) });
                    boundingBoxes.Remove(box);
                }

                if (selectedBox != null && boxesToDelete.Contains(selectedBox))
                    selectedBox = null;

                waypointMarkers.Remove(waypoint);
                listViewEventWaypoints.Items.Remove(selectedItem);
                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();
                panelTimeline.Invalidate();
                pictureBoxVideo.Invalidate();

                MessageBox.Show(
                    "✅ Event Waypoint가 삭제되었습니다.\n\n" +
                    $"삭제된 박스: {boxesToDelete.Count}개",
                    "삭제 완료",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
                    UpdateBboxListDisplay(); // 선택 후 우측 패널 동기화
                    pictureBoxVideo.Invalidate();
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
                    InvalidateBoxCache();
                    AddUndoAction(new UndoAction { Type = UndoActionType.AddBox, Box = drawingBox });

                    selectedBox = drawingBox;
                    UpdateObjectInfo(selectedBox);
                    UpdateBoxCount();
                    UpdateBboxListDisplay();
                    
                    // Event bbox 생성 시 자동으로 Waypoint 추가 및 영상 끝까지 전파 (초록 색상)
                    if (drawingBox.Label == "event")
                    {
                        CreateEventWaypoint(drawingBox);
                        PropagateEventBoxToEnd(drawingBox); // 영상 끝까지 전파
                    }
                }

                drawingBox = null;
                isDrawing = false;
                pictureBoxVideo.Invalidate();
            }
            else if (isDragging)
            {
                isDragging = false;
                
                // 박스 이동/수정 완료 시에도 Event 박스 전파
                if (selectedBox != null && selectedBox.Label == "event")
                {
                    PropagateEventBoxFromCurrentFrame(selectedBox);
                }
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
            // panelVideoControls 크기가 변경될 때 레이아웃 조정
            if (panelVideoControls != null)
            {
                // groupBoxObjectInfo: 오른쪽에 고정
                if (groupBoxObjectInfo != null)
                {
                    groupBoxObjectInfo.Location = new System.Drawing.Point(
                        panelVideoControls.Width - groupBoxObjectInfo.Width - 16, // 우측 여백 16px
                        16 // 상단 여백
                    );
                }
            }
        }

        private void pictureBoxVideo_Paint(object sender, PaintEventArgs e)
        {
            if (pictureBoxVideo.Image == null)
                return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 성능 최적화: 현재 프레임의 박스를 캐시에서 가져오기
            if (lastCachedFrameForPaint != currentFrameIndex)
            {
                cachedCurrentFrameBoxes = boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex).ToList();
                lastCachedFrameForPaint = currentFrameIndex;
            }

            foreach (var box in cachedCurrentFrameBoxes)
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

                // 라벨 텍스트 생성 (성능 최적화: 캐싱된 배열 사용)
                string labelText = GetBoxLabelText(box);
                
                // 성능 최적화: 재사용 가능한 Font 사용
                SizeF textSize = g.MeasureString(labelText, labelFont);
                RectangleF labelBg = new RectangleF(
                    viewRect.X,
                    viewRect.Y - textSize.Height - 4,
                    textSize.Width + 8,
                    textSize.Height + 4
                );

                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(200, boxColor)))
                    g.FillRectangle(bgBrush, labelBg);

                using (SolidBrush textBrush = new SolidBrush(Color.White))
                    g.DrawString(labelText, labelFont, textBrush, viewRect.X + 4, viewRect.Y - textSize.Height - 2);
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
            listViewPersonWaypoints.Items.Clear();
            listViewVehicleWaypoints.Items.Clear();
            listViewEventWaypoints.Items.Clear();

            foreach (var waypoint in waypointMarkers)
            {
                var item = new ListViewItem(waypoint.EntryTime);
                item.SubItems.Add(waypoint.ExitTime);
                
                // Label별로 category name 표시
                if (waypoint.Label == "person")
                {
                    // Person: Entry~Exit 구간의 Person 박스 category name 수집
                    var personBoxes = boundingBoxes
                        .Where(b => b.Label == "person" && 
                                   b.FrameIndex >= waypoint.EntryFrame && 
                                   b.FrameIndex <= waypoint.ExitFrame)
                        .ToList();
                    
                    if (personBoxes.Count > 0)
                    {
                        var firstBox = personBoxes.First();
                        string categoryName = GetPersonCategoryName(firstBox.PersonId);
                        item.SubItems.Add(categoryName);
                    }
                    else
                    {
                        item.SubItems.Add("person");
                    }
                    
                    item.ForeColor = waypoint.MarkerColor;
                    item.Tag = waypoint;
                    listViewPersonWaypoints.Items.Add(item);
                }
                else if (waypoint.Label == "vehicle")
                {
                    // Vehicle: 단일 프레임의 Vehicle category name 표시
                    var vehicleBox = boundingBoxes
                        .FirstOrDefault(b => b.Label == "vehicle" && b.FrameIndex == waypoint.EntryFrame);
                    
                    if (vehicleBox != null)
                    {
                        string categoryName = GetVehicleCategoryName(vehicleBox.VehicleId);
                        item.SubItems.Add(categoryName);
                    }
                    else
                    {
                        item.SubItems.Add("vehicle");
                    }
                    
                    item.ForeColor = waypoint.MarkerColor;
                    item.Tag = waypoint;
                    listViewVehicleWaypoints.Items.Add(item);
                }
                else if (waypoint.Label == "event")
                {
                    // Event: Entry~Exit 구간의 Event category name 표시
                    var eventBox = boundingBoxes
                        .FirstOrDefault(b => b.Label == "event" && 
                                           b.FrameIndex >= waypoint.EntryFrame && 
                                           b.FrameIndex <= waypoint.ExitFrame);
                    
                    if (eventBox != null)
                    {
                        string categoryName = GetEventCategoryName(eventBox.EventId);
                        item.SubItems.Add(categoryName);
                    }
                    else
                    {
                        item.SubItems.Add("event");
                    }
                    
                    item.ForeColor = waypoint.MarkerColor;
                    item.Tag = waypoint;
                    listViewEventWaypoints.Items.Add(item);
                }
            }
        }

        private string GetPersonCategoryName(int personId)
        {
            string[] personTypes = { "person_standing", "person_sitting", "person_lying_down", "person_moving", 
                                    "person_moving_slowly", "person_unspecified_position", "person_running", 
                                    "person_squatting", "person_running_toward_camera", "person_etc_standing", 
                                    "person_etc_lying", "person_etc_sitting", "person_etc_moving", "person_etc_posture" };
            if (personId > 0 && personId <= personTypes.Length)
                return personTypes[personId - 1];
            return $"person_{personId:D2}";
        }

        private string GetVehicleCategoryName(int vehicleId)
        {
            string[] vehicleTypes = { "car", "motorcycle", "bicycle", "e_scooter" };
            if (vehicleId > 0 && vehicleId <= vehicleTypes.Length)
                return vehicleTypes[vehicleId - 1];
            return $"vehicle_{vehicleId}";
        }

        private string GetEventCategoryName(int eventId)
        {
            string[] eventTypes = { "contact", "exchange", "board", "final_exchange" };
            if (eventId > 0 && eventId <= eventTypes.Length)
                return eventTypes[eventId - 1];
            return $"event_{eventId}";
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
                // ✅ 비디오 전환 시: 데이터 백업 → 초기화 → 로드 (실패 시 복원)
                var backupBoxes = new List<BoundingBox>(boundingBoxes);
                var backupWaypoints = new List<WaypointMarker>(waypointMarkers);
                var backupSelectedBox = selectedBox;
                var backupUndoStack = new Stack<UndoAction>(undoStack.Reverse());
                var backupRedoStack = new Stack<UndoAction>(redoStack.Reverse());
                
                boundingBoxes.Clear();
                waypointMarkers.Clear();
                selectedBox = null;
                undoStack.Clear();
                redoStack.Clear();
                lastRenderedWaypoint = null;
                
                try
                {
                    currentVideoIndex = selectedIndex;
                    await LoadVideoWithSubtitle(videoFileList[currentVideoIndex]);
                    
                    // LoadVideoWithSubtitle 내부의 LoadLabelingData에서 새 데이터가 로드됨
                    UpdateBoxCount();
                    UpdateWaypointListView();
                    pictureBoxVideo.Invalidate();
                    RefreshVideoListView();
                }
                catch (Exception ex)
                {
                    // ❌ 로드 실패 시 이전 데이터 복원
                    boundingBoxes.Clear();
                    boundingBoxes.AddRange(backupBoxes);
                    waypointMarkers.Clear();
                    waypointMarkers.AddRange(backupWaypoints);
                    selectedBox = backupSelectedBox;
                    undoStack.Clear();
                    foreach (var action in backupUndoStack) undoStack.Push(action);
                    redoStack.Clear();
                    foreach (var action in backupRedoStack) redoStack.Push(action);
                    
                    UpdateBoxCount();
                    UpdateWaypointListView();
                    pictureBoxVideo.Invalidate();
                    
                    MessageBox.Show($"영상 로드 실패:\n{ex.Message}\n\n이전 작업 내용이 복원되었습니다.", 
                        "영상 전환 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
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
                
                // ✅ Event → Person 변경 시 전파된 Event 박스 삭제
                if (oldLabel == "event")
                {
                    RemovePropagatedEventBoxes(selectedBox);
                }
                
                selectedBox.Label = "person";
                SetBoxId(selectedBox, "person", 1); // 기본값 person_01
                
                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel
                });
                
                InvalidateBoxCache();
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
                
                // ✅ Event → Vehicle 변경 시 전파된 Event 박스 삭제
                if (oldLabel == "event")
                {
                    RemovePropagatedEventBoxes(selectedBox);
                }
                
                selectedBox.Label = "vehicle";
                SetBoxId(selectedBox, "vehicle", 1); // 기본값 vehicle_car
                
                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel
                });
                
                InvalidateBoxCache();
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
                
                InvalidateBoxCache();
                pictureBoxVideo.Invalidate();
                UpdateBboxListDisplay();
                UpdateObjectInfo(selectedBox);
                
                // ✅ Event로 변경 시 자동 전파
                if (oldLabel != "event")
                {
                    System.Diagnostics.Debug.WriteLine($"[라벨 변경] {oldLabel} → event, 전파 시작");
                    CreateEventWaypoint(selectedBox);
                    PropagateEventBoxToEnd(selectedBox);
                }
            }
        }

        // Labels 패널 접기/펼치기 토글 함수들
        private void TogglePersonPanel(object sender, EventArgs e)
        {
            isPersonExpanded = !isPersonExpanded;
            panelPersonList.Visible = isPersonExpanded;
            labelPersonList.Text = isPersonExpanded ? "▽ person" : "> person";
            
            // 패널이 펼쳐질 때만 내용 업데이트
            if (isPersonExpanded)
            {
                UpdatePersonListDisplay();
            }
            
            // 레이아웃 재계산 (아래 요소들의 위치 조정)
            UpdateLabelsLayoutAfterToggle();
        }

        private void ToggleVehiclePanel(object sender, EventArgs e)
        {
            isVehicleExpanded = !isVehicleExpanded;
            panelVehicleList.Visible = isVehicleExpanded;
            labelVehicleList.Text = isVehicleExpanded ? "▽ vehicle" : "> vehicle";
            
            // 패널이 펼쳐질 때만 내용 업데이트
            if (isVehicleExpanded)
            {
                UpdateVehicleListDisplay();
            }
            
            // 레이아웃 재계산 (아래 요소들의 위치 조정)
            UpdateLabelsLayoutAfterToggle();
        }

        private void ToggleEventPanel(object sender, EventArgs e)
        {
            isEventExpanded = !isEventExpanded;
            panelEventList.Visible = isEventExpanded;
            labelEventList.Text = isEventExpanded ? "▽ event" : "> event";
            
            // 패널이 펼쳐질 때만 내용 업데이트
            if (isEventExpanded)
            {
                UpdateEventListDisplay();
            }
            
            // 레이아웃 재계산 (아래 요소들의 위치 조정)
            UpdateLabelsLayoutAfterToggle();
        }
        
        // Labels 패널 레이아웃 업데이트 (토글 시 요소들의 위치 동적 조정)
        private void UpdateLabelsLayoutAfterToggle()
        {
            const int startY = 70;
            const int toggleHeight = 30;
            const int panelHeight = 100;
            int currentY = startY;
            
            // Person 섹션
            currentY += toggleHeight; // Person 토글 버튼 다음
            if (isPersonExpanded)
                currentY += panelHeight; // Person 패널이 펼쳐져 있으면 그 높이만큼 추가
            
            // Vehicle 섹션
            labelVehicleList.Location = new System.Drawing.Point(8, currentY);
            currentY += toggleHeight;
            panelVehicleList.Location = new System.Drawing.Point(8, currentY);
            if (isVehicleExpanded)
                currentY += panelHeight;
            
            // Event 섹션
            labelEventList.Location = new System.Drawing.Point(8, currentY);
            currentY += toggleHeight;
            panelEventList.Location = new System.Drawing.Point(8, currentY);
            if (isEventExpanded)
                currentY += panelHeight;
            
            // 하단 버튼들
            currentY += 5; // 약간의 여백
            btnDeleteLabel.Location = new System.Drawing.Point(8, currentY);
            currentY += 42; // 삭제 버튼 높이 + 여백
            btnExportJsonInLabels.Location = new System.Drawing.Point(8, currentY);
            
            // Labels 패널 전체 높이 조정
            currentY += 50; // JSON 저장 버튼 높이 + 여백
            groupBoxLabels.Height = Math.Max(260, currentY);
        }

        // 성능 최적화: 박스 라벨 텍스트 생성 (재사용 가능한 배열 사용)
        private static readonly string[] VehicleTypes = { "car", "motorcycle", "bicycle", "e_scooter" };
        private static readonly string[] EventTypes = { "contact", "exchange", "board", "final_exchange" };
        
        private string GetBoxLabelText(BoundingBox box)
        {
            if (box.Label == "person")
            {
                return $"person_{box.PersonId:D2}";
            }
            else if (box.Label == "vehicle")
            {
                if (box.VehicleId > 0 && box.VehicleId <= VehicleTypes.Length)
                    return $"vehicle_{VehicleTypes[box.VehicleId - 1]}";
                else
                    return $"vehicle_{box.VehicleId}";
            }
            else if (box.Label == "event")
            {
                if (box.EventId > 0 && box.EventId <= EventTypes.Length)
                    return $"event_{EventTypes[box.EventId - 1]}";
                else
                    return $"event_{box.EventId}";
            }
            return "";
        }
        
        // 성능 최적화: 박스 데이터가 변경되면 캐시 무효화
        private void InvalidateBoxCache()
        {
            lastCachedFrameForPaint = -1;
            cachedCurrentFrameBoxes.Clear();
        }
        
        // bbox 리스트 업데이트가 필요한지 확인 (리소스 최적화)
        private bool ShouldUpdateBboxList(int frameIndex)
        {
            // 현재 프레임이 속한 waypoint 찾기
            var currentWaypoint = waypointMarkers.FirstOrDefault(w =>
                frameIndex >= w.EntryFrame &&
                frameIndex <= w.ExitFrame);
            
            // Waypoint가 변경되었는지 확인
            bool waypointChanged = false;
            
            if (currentWaypoint == null && lastRenderedWaypoint == null)
            {
                // 둘 다 null이면 변경 없음
                waypointChanged = false;
            }
            else if (currentWaypoint == null || lastRenderedWaypoint == null)
            {
                // 하나만 null이면 변경됨
                waypointChanged = true;
            }
            else
            {
                // 둘 다 null이 아니면 EntryFrame과 ExitFrame으로 비교
                waypointChanged = (currentWaypoint.EntryFrame != lastRenderedWaypoint.EntryFrame ||
                                  currentWaypoint.ExitFrame != lastRenderedWaypoint.ExitFrame);
            }
            
            if (waypointChanged)
            {
                lastRenderedWaypoint = currentWaypoint;
                return true;
            }
            
            return false;
        }
        
        // 3개 bbox 목록을 동적으로 생성하여 표시 (현재 프레임 기준)
        private void UpdateBboxListDisplay()
        {
            // 펼쳐진 패널만 업데이트 (성능 최적화)
            if (isPersonExpanded)
                UpdatePersonListDisplay();
            if (isVehicleExpanded)
                UpdateVehicleListDisplay();
            if (isEventExpanded)
                UpdateEventListDisplay();
        }
        
        // Person 리스트 표시 (현재 프레임의 Person bbox)
        private void UpdatePersonListDisplay()
        {
            panelPersonList.Controls.Clear();
            
            var currentBoxes = boundingBoxes
                .Where(b => b.FrameIndex == currentFrameIndex && b.Label == "person")
                .ToList();
            
            if (currentBoxes.Count == 0)
            {
                Label emptyLabel = new Label
                {
                    Text = "현재 프레임에 Person 없음",
                    Font = new System.Drawing.Font("Segoe UI", 8F),
                    ForeColor = System.Drawing.Color.Gray,
                    Location = new System.Drawing.Point(5, 5),
                    AutoSize = true
                };
                panelPersonList.Controls.Add(emptyLabel);
                return;
            }
            
            int yPos = 5;
            foreach (var box in currentBoxes)
            {
                var currentBox = box;
                
                Panel itemPanel = new Panel
                {
                    Location = new System.Drawing.Point(5, yPos),
                    Size = new System.Drawing.Size(260, 65),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackColor = System.Drawing.Color.FromArgb(252, 231, 243),
                    Cursor = Cursors.Hand
                };
                
                Label itemLabel = new Label
                {
                    Text = $"person_{currentBox.PersonId:D2}",
                    Location = new System.Drawing.Point(8, 8),
                    Size = new System.Drawing.Size(244, 20),
                    Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.FromArgb(157, 23, 77),
                    BackColor = System.Drawing.Color.Transparent
                };
                
                ComboBox comboBox = new ComboBox
                {
                    Location = new System.Drawing.Point(8, 32),
                    Size = new System.Drawing.Size(244, 25),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new System.Drawing.Font("Segoe UI", 8F)
                };
                
                // ComboBox 호버 시 스크롤 방지
                comboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
                
                for (int i = 1; i <= 14; i++)
                {
                    comboBox.Items.Add($"person_{i:D2}");
                }
                comboBox.SelectedItem = $"person_{currentBox.PersonId:D2}";
                
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    if (comboBox.SelectedItem != null)
                    {
                        string selected = comboBox.SelectedItem.ToString();
                        if (selected.StartsWith("person_"))
                        {
                            int newId = int.Parse(selected.Substring(7));
                            SetBoxId(currentBox, "person", newId);
                            UpdateObjectInfo(currentBox);
                            UpdateBboxListDisplay();
                            pictureBoxVideo.Invalidate();
                        }
                    }
                };
                
                itemPanel.Controls.Add(itemLabel);
                itemPanel.Controls.Add(comboBox);
                
                EventHandler clickHandler = (s, e) =>
                {
                    selectedBox = currentBox;
                    UpdateObjectInfo(selectedBox);
                    pictureBoxVideo.Invalidate();
                    
                    foreach (Control ctrl in panelPersonList.Controls)
                    {
                        if (ctrl is Panel p)
                        {
                            p.BorderStyle = (p == itemPanel) 
                                ? System.Windows.Forms.BorderStyle.Fixed3D 
                                : System.Windows.Forms.BorderStyle.FixedSingle;
                        }
                    }
                };
                
                itemPanel.Click += clickHandler;
                itemLabel.Click += clickHandler;
                
                panelPersonList.Controls.Add(itemPanel);
                yPos += 70;
            }
        }
        
        // Vehicle 리스트 표시 (현재 프레임의 Vehicle bbox)
        private void UpdateVehicleListDisplay()
        {
            panelVehicleList.Controls.Clear();
            
            var currentBoxes = boundingBoxes
                .Where(b => b.FrameIndex == currentFrameIndex && b.Label == "vehicle")
                .ToList();
            
            if (currentBoxes.Count == 0)
            {
                Label emptyLabel = new Label
                {
                    Text = "현재 프레임에 Vehicle 없음",
                    Font = new System.Drawing.Font("Segoe UI", 8F),
                    ForeColor = System.Drawing.Color.Gray,
                    Location = new System.Drawing.Point(5, 5),
                    AutoSize = true
                };
                panelVehicleList.Controls.Add(emptyLabel);
                return;
            }
            
            int yPos = 5;
            foreach (var box in currentBoxes)
            {
                var currentBox = box;
                string[] vehicleTypes = { "car", "motorcycle", "e_scooter", "bicycle" };
                string vehicleName = currentBox.VehicleId > 0 && currentBox.VehicleId <= vehicleTypes.Length 
                    ? vehicleTypes[currentBox.VehicleId - 1] 
                    : currentBox.VehicleId.ToString();
                
                Panel itemPanel = new Panel
                {
                    Location = new System.Drawing.Point(5, yPos),
                    Size = new System.Drawing.Size(260, 65),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackColor = System.Drawing.Color.FromArgb(219, 234, 254),
                    Cursor = Cursors.Hand
                };
                
                Label itemLabel = new Label
                {
                    Text = $"vehicle_{vehicleName}",
                    Location = new System.Drawing.Point(8, 8),
                    Size = new System.Drawing.Size(244, 20),
                    Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.FromArgb(30, 64, 175),
                    BackColor = System.Drawing.Color.Transparent
                };
                
                ComboBox comboBox = new ComboBox
                {
                    Location = new System.Drawing.Point(8, 32),
                    Size = new System.Drawing.Size(244, 25),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new System.Drawing.Font("Segoe UI", 8F)
                };
                
                // ComboBox 호버 시 스크롤 방지
                comboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
                
                comboBox.Items.AddRange(new object[] { "vehicle_car", "vehicle_motorcycle", "vehicle_e_scooter", "vehicle_bicycle" });
                comboBox.SelectedItem = $"vehicle_{vehicleName}";
                
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    if (comboBox.SelectedItem != null)
                    {
                        string selected = comboBox.SelectedItem.ToString();
                        if (selected.StartsWith("vehicle_"))
                        {
                            string vType = selected.Substring(8);
                            int vehicleId = Array.IndexOf(vehicleTypes, vType) + 1;
                            if (vehicleId > 0)
                            {
                                SetBoxId(currentBox, "vehicle", vehicleId);
                                UpdateObjectInfo(currentBox);
                                UpdateBboxListDisplay();
                                pictureBoxVideo.Invalidate();
                            }
                        }
                    }
                };
                
                itemPanel.Controls.Add(itemLabel);
                itemPanel.Controls.Add(comboBox);
                
                EventHandler clickHandler = (s, e) =>
                {
                    selectedBox = currentBox;
                    UpdateObjectInfo(selectedBox);
                    pictureBoxVideo.Invalidate();
                    
                    foreach (Control ctrl in panelVehicleList.Controls)
                    {
                        if (ctrl is Panel p)
                        {
                            p.BorderStyle = (p == itemPanel) 
                                ? System.Windows.Forms.BorderStyle.Fixed3D 
                                : System.Windows.Forms.BorderStyle.FixedSingle;
                        }
                    }
                };
                
                itemPanel.Click += clickHandler;
                itemLabel.Click += clickHandler;
                
                panelVehicleList.Controls.Add(itemPanel);
                yPos += 70;
            }
        }
        
        // Event 리스트 표시 (현재 프레임의 Event bbox)
        private void UpdateEventListDisplay()
        {
            panelEventList.Controls.Clear();
            
            var currentBoxes = boundingBoxes
                .Where(b => b.FrameIndex == currentFrameIndex && b.Label == "event")
                .ToList();
            
            if (currentBoxes.Count == 0)
            {
                Label emptyLabel = new Label
                {
                    Text = "현재 프레임에 Event 없음",
                    Font = new System.Drawing.Font("Segoe UI", 8F),
                    ForeColor = System.Drawing.Color.Gray,
                    Location = new System.Drawing.Point(5, 5),
                    AutoSize = true
                };
                panelEventList.Controls.Add(emptyLabel);
                return;
            }
            
            int yPos = 5;
            foreach (var box in currentBoxes)
            {
                var currentBox = box;
                string[] eventTypes = { "contact", "exchange", "board", "final_exchange" };
                string eventName = currentBox.EventId > 0 && currentBox.EventId <= eventTypes.Length 
                    ? eventTypes[currentBox.EventId - 1] 
                    : currentBox.EventId.ToString();
                
                Panel itemPanel = new Panel
                {
                    Location = new System.Drawing.Point(5, yPos),
                    Size = new System.Drawing.Size(260, 65),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackColor = System.Drawing.Color.FromArgb(220, 252, 231),
                    Cursor = Cursors.Hand
                };
                
                Label itemLabel = new Label
                {
                    Text = $"event_{eventName}",
                    Location = new System.Drawing.Point(8, 8),
                    Size = new System.Drawing.Size(244, 20),
                    Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.FromArgb(20, 83, 45),
                    BackColor = System.Drawing.Color.Transparent
                };
                
                ComboBox comboBox = new ComboBox
                {
                    Location = new System.Drawing.Point(8, 32),
                    Size = new System.Drawing.Size(244, 25),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new System.Drawing.Font("Segoe UI", 8F)
                };
                
                // ComboBox 호버 시 스크롤 방지
                comboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
                
                comboBox.Items.AddRange(new object[] { "event_contact", "event_exchange", "event_board", "event_final_exchange" });
                comboBox.SelectedItem = $"event_{eventName}";
                
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    if (comboBox.SelectedItem != null)
                    {
                        string selected = comboBox.SelectedItem.ToString();
                        if (selected.StartsWith("event_"))
                        {
                            string eType = selected.Substring(6);
                            int eventId = Array.IndexOf(eventTypes, eType) + 1;
                            if (eventId > 0)
                            {
                                int oldEventId = currentBox.EventId;
                                SetBoxId(currentBox, "event", eventId);
                                
                                // Event 타입 변경 시 동일한 EventId와 Rectangle을 가진 박스만 업데이트
                                var waypoint = waypointMarkers.FirstOrDefault(w =>
                                    currentBox.FrameIndex >= w.EntryFrame &&
                                    currentBox.FrameIndex <= w.ExitFrame);
                                
                                if (waypoint != null)
                                {
                                    var relatedBoxes = boundingBoxes.Where(b =>
                                        b.Label == "event" &&
                                        b.EventId == oldEventId &&
                                        b.Rectangle.X == currentBox.Rectangle.X &&
                                        b.Rectangle.Y == currentBox.Rectangle.Y &&
                                        b.Rectangle.Width == currentBox.Rectangle.Width &&
                                        b.Rectangle.Height == currentBox.Rectangle.Height &&
                                        b.FrameIndex >= waypoint.EntryFrame &&
                                        b.FrameIndex <= waypoint.ExitFrame).ToList();
                                    
                                    foreach (var relatedBox in relatedBoxes)
                                    {
                                        SetBoxId(relatedBox, "event", eventId);
                                    }
                                }
                                
                                UpdateObjectInfo(currentBox);
                                UpdateBboxListDisplay();
                                pictureBoxVideo.Invalidate();
                            }
                        }
                    }
                };
                
                itemPanel.Controls.Add(itemLabel);
                itemPanel.Controls.Add(comboBox);
                
                EventHandler clickHandler = (s, e) =>
                {
                    selectedBox = currentBox;
                    UpdateObjectInfo(selectedBox);
                    pictureBoxVideo.Invalidate();
                    
                    foreach (Control ctrl in panelEventList.Controls)
                    {
                        if (ctrl is Panel p)
                        {
                            p.BorderStyle = (p == itemPanel) 
                                ? System.Windows.Forms.BorderStyle.Fixed3D 
                                : System.Windows.Forms.BorderStyle.FixedSingle;
                        }
                    }
                };
                
                itemPanel.Click += clickHandler;
                itemLabel.Click += clickHandler;
                
                panelEventList.Controls.Add(itemPanel);
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
            InvalidateBoxCache();
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
            if (label == "event") return Math.Min(19 + (boxId - 1), 22); // 19~22 (4개)
            
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
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int width = panelTimeline.Width;
            int height = panelTimeline.Height;

            // 배경색 (노란색)
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(250, 204, 21)))
            {
                g.FillRectangle(bgBrush, 0, 0, width, height);
            }

            if (totalFrames > 0)
            {
                // ✅ Waypoint 구간별 색상 표시 (항상 원래 색상 유지)
                foreach (var waypoint in waypointMarkers)
                {
                    int startX = (int)(width * ((float)waypoint.EntryFrame / totalFrames));
                    int endX = (int)(width * ((float)waypoint.ExitFrame / totalFrames));
                    int segmentWidth = endX - startX;

                    // 반투명 원래 색상으로 구간 표시
                    Color segmentColor = Color.FromArgb(180, waypoint.MarkerColor);
                    
                    using (SolidBrush markerBrush = new SolidBrush(segmentColor))
                    {
                        g.FillRectangle(markerBrush, startX, 0, segmentWidth, height);
                    }
                }

                // ✅ Entry/Exit 마커 표시 (작은 사각형)
                foreach (var waypoint in waypointMarkers)
                {
                    int entryX = (int)(width * ((float)waypoint.EntryFrame / totalFrames));
                    int exitX = (int)(width * ((float)waypoint.ExitFrame / totalFrames));
                    
                    int markerSize = 8; // 마커 크기
                    int entryMarkerY = 0; // Entry 마커: 상단 정렬
                    int exitMarkerY = height - markerSize; // Exit 마커: 하단 정렬

                    // ✅ 선택된 waypoint인 경우 진한 빨간색으로 표시
                    bool isSelected = (selectedWaypoint != null && 
                                      waypoint.Label == selectedWaypoint.Label && 
                                      waypoint.ObjectId == selectedWaypoint.ObjectId &&
                                      waypoint.EntryFrame == selectedWaypoint.EntryFrame &&
                                      waypoint.ExitFrame == selectedWaypoint.ExitFrame);
                    
                    Color markerColor = isSelected ? Color.FromArgb(220, 38, 38) : waypoint.MarkerColor;

                    // Entry 마커 (진한 색상, 상단)
                    using (SolidBrush entryBrush = new SolidBrush(markerColor))
                    {
                        g.FillRectangle(entryBrush, entryX - markerSize / 2, entryMarkerY, markerSize, markerSize);
                    }
                    
                    // Entry 마커 테두리
                    using (Pen entryPen = new Pen(Color.White, 2))
                    {
                        g.DrawRectangle(entryPen, entryX - markerSize / 2, entryMarkerY, markerSize, markerSize);
                    }

                    // Exit 마커 (진한 색상, 하단)
                    using (SolidBrush exitBrush = new SolidBrush(markerColor))
                    {
                        g.FillRectangle(exitBrush, exitX - markerSize / 2, exitMarkerY, markerSize, markerSize);
                    }
                    
                    // Exit 마커 테두리
                    using (Pen exitPen = new Pen(Color.White, 2))
                    {
                        g.DrawRectangle(exitPen, exitX - markerSize / 2, exitMarkerY, markerSize, markerSize);
                    }
                }
            }

            // ✅ Entry 설정 중일 때 빨간 선 표시
            if (entryFrameIndex.HasValue && !exitFrameIndex.HasValue && totalFrames > 0)
            {
                int entryX = (int)(width * ((float)entryFrameIndex.Value / totalFrames));
                using (Pen pen = new Pen(Color.Red, 3))
                {
                    g.DrawLine(pen, entryX, 0, entryX, height);
                }
            }

            // ✅ 현재 재생 위치 표시 (흰색 선)
            if (totalFrames > 0)
            {
                int currentX = (int)(width * timelineProgress);
                using (Pen pen = new Pen(Color.White, 3))
                {
                    g.DrawLine(pen, currentX, 0, currentX, height);
                }
            }
        }

        private void panelTimeline_MouseDown(object sender, MouseEventArgs e)
        {
            if (totalFrames == 0) return;

            // ✅ 먼저 마커 클릭 여부 확인 (Entry/Exit 프레임으로 이동)
            if (TryNavigateToMarker(e.X, e.Y))
            {
                return; // 마커를 클릭했으면 드래그 시작하지 않음
            }

            // 마커가 아니면 기존 타임라인 드래그 시작
            isTimelineDragging = true;
            UpdateFrameFromMousePosition(e.X);
        }

        /// <summary>
        /// 클릭 위치가 마커 영역인지 확인하고, 맞으면 해당 프레임으로 이동
        /// </summary>
        private bool TryNavigateToMarker(int mouseX, int mouseY)
        {
            if (totalFrames == 0) return false;

            int width = panelTimeline.Width;
            int height = panelTimeline.Height;
            int markerSize = 8;
            int clickTolerance = 6; // 클릭 허용 범위 (마커보다 약간 넓게)

            foreach (var waypoint in waypointMarkers)
            {
                // Entry 마커 위치 계산 (상단)
                int entryX = (int)(width * ((float)waypoint.EntryFrame / totalFrames));
                int entryLeft = entryX - markerSize / 2;
                int entryRight = entryX + markerSize / 2;
                int entryTop = 0;
                int entryBottom = markerSize;

                // Exit 마커 위치 계산 (하단)
                int exitX = (int)(width * ((float)waypoint.ExitFrame / totalFrames));
                int exitLeft = exitX - markerSize / 2;
                int exitRight = exitX + markerSize / 2;
                int exitTop = height - markerSize;
                int exitBottom = height;

                // Entry 마커 클릭 확인 (상단)
                if (mouseX >= entryLeft - clickTolerance && mouseX <= entryRight + clickTolerance &&
                    mouseY >= entryTop - clickTolerance && mouseY <= entryBottom + clickTolerance)
                {
                    LoadFrame(waypoint.EntryFrame);
                    System.Diagnostics.Debug.WriteLine($"[Timeline] Entry 마커 클릭: {waypoint.Label}, Frame {waypoint.EntryFrame}");
                    return true;
                }

                // Exit 마커 클릭 확인 (하단)
                if (mouseX >= exitLeft - clickTolerance && mouseX <= exitRight + clickTolerance &&
                    mouseY >= exitTop - clickTolerance && mouseY <= exitBottom + clickTolerance)
                {
                    LoadFrame(waypoint.ExitFrame);
                    System.Diagnostics.Debug.WriteLine($"[Timeline] Exit 마커 클릭: {waypoint.Label}, Frame {waypoint.ExitFrame}");
                    return true;
                }
            }

            return false; // 마커가 아님
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
                    InvalidateBoxCache();
                    if (selectedBox == action.Box)
                        selectedBox = null;
                    break;

                case UndoActionType.RemoveBox:
                    boundingBoxes.Add(action.Box);
                    InvalidateBoxCache();
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
                        InvalidateBoxCache();
                    }
                    break;

                case UndoActionType.Tracking:
                    foreach (var box in action.TrackedBoxes)
                    {
                        boundingBoxes.Remove(box);
                    }
                    InvalidateBoxCache();
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
                    InvalidateBoxCache();
                    break;

                case UndoActionType.RemoveBox:
                    boundingBoxes.Remove(action.Box);
                    InvalidateBoxCache();
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
                        InvalidateBoxCache();
                    }
                    break;

                case UndoActionType.Tracking:
                    foreach (var box in action.TrackedBoxes)
                    {
                        boundingBoxes.Add(box);
                    }
                    InvalidateBoxCache();
                    break;
            }

            undoStack.Push(action);
            UpdateBoxCount();
            UpdateBboxListDisplay();
            pictureBoxVideo.Invalidate();
        }
        #endregion

        #region Event Box Propagation
        /// <summary>
        /// Entry 프레임에 있는 모든 Event 박스를 Exit 프레임까지 자동 전파
        /// (Exit 마커 설정 시 호출됨)
        /// </summary>
        private void PropagateAllEventBoxesInRange(int entryFrame, int exitFrame)
        {
            // Entry 프레임의 모든 Event 박스 찾기
            var eventBoxesAtEntry = boundingBoxes
                .Where(b => b.FrameIndex == entryFrame && b.Label == "event")
                .ToList();

            if (eventBoxesAtEntry.Count == 0)
                return; // Event 박스가 없으면 전파할 필요 없음

            int totalPropagated = 0;

            foreach (var eventBox in eventBoxesAtEntry)
            {
                // Entry 다음 프레임부터 Exit까지 전파
                for (int frame = entryFrame + 1; frame <= exitFrame; frame++)
                {
                    // 이미 해당 프레임에 동일한 Event 박스가 있는지 확인
                    bool exists = boundingBoxes.Any(b =>
                        b.FrameIndex == frame &&
                        b.Label == "event" &&
                        b.EventId == eventBox.EventId);

                    if (!exists)
                    {
                        var newBox = new BoundingBox
                        {
                            Rectangle = eventBox.Rectangle,
                            Label = eventBox.Label,
                            FrameIndex = frame,
                            PersonId = eventBox.PersonId,
                            VehicleId = eventBox.VehicleId,
                            EventId = eventBox.EventId,
                            Action = "waypoint"
                        };
                        boundingBoxes.Add(newBox);
                        totalPropagated++;
                    }
                }
            }

            if (totalPropagated > 0)
            {
                InvalidateBoxCache();
            }
        }

        /// <summary>
        /// Vehicle bbox 생성 시 자동으로 Waypoint 추가 (단일 프레임)
        /// </summary>
        private void CreateVehicleWaypoint(BoundingBox box)
        {
            if (box.Label != "vehicle") return;

            TimeSpan time = TimeSpan.FromSeconds(box.FrameIndex / fps);
            string timeString = time.ToString(@"hh\:mm\:ss");

            var waypoint = new WaypointMarker
            {
                EntryFrame = box.FrameIndex,
                ExitFrame = box.FrameIndex, // 단일 프레임
                MarkerColor = System.Drawing.Color.FromArgb(107, 158, 255), // 파랑
                EntryTime = timeString,
                ExitTime = timeString,
                ObjectId = 0,
                Label = "vehicle"
            };

            waypointMarkers.Add(waypoint);
            UpdateWaypointListView();
            panelTimeline.Invalidate();
        }

        /// <summary>
        /// Event bbox 생성 시 자동으로 Waypoint 추가 (현재 프레임 ~ 영상 끝 또는 Q키 종료 시점)
        /// </summary>
        private void CreateEventWaypoint(BoundingBox box)
        {
            if (box.Label != "event") return;

            TimeSpan entryTime = TimeSpan.FromSeconds(box.FrameIndex / fps);
            TimeSpan exitTime = TimeSpan.FromSeconds((totalFrames - 1) / fps); // 영상 끝

            // ✅ Event Waypoint 생성 (초록 색상, EventId 저장)
            var waypoint = new WaypointMarker
            {
                EntryFrame = box.FrameIndex,
                ExitFrame = totalFrames - 1, // 영상 끝 (Q키로 조기 종료 가능)
                MarkerColor = System.Drawing.Color.FromArgb(107, 255, 107), // 초록
                EntryTime = entryTime.ToString(@"hh\:mm\:ss"),
                ExitTime = exitTime.ToString(@"hh\:mm\:ss"),
                ObjectId = box.EventId,
                Label = "event"
            };

            waypointMarkers.Add(waypoint);
            UpdateWaypointListView();
            panelTimeline.Invalidate();
        }

        /// <summary>
        /// Event bbox를 현재 프레임부터 영상 끝까지 자동 전파
        /// </summary>
        private void PropagateEventBoxToEnd(BoundingBox box)
        {
            if (box.Label != "event") return;

            int startFrame = box.FrameIndex + 1;
            int endFrame = totalFrames - 1;

            if (startFrame > endFrame) return;

            System.Diagnostics.Debug.WriteLine($"[Event 전파 시작] 프레임 {box.FrameIndex}에서 생성된 Event를 {startFrame}~{endFrame}까지 전파 ({endFrame - startFrame + 1}개 프레임)");

            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                // 같은 EventId와 위치를 가진 박스가 이미 있는지 확인
                bool exists = boundingBoxes.Any(b =>
                    b.FrameIndex == frame &&
                    b.Label == "event" &&
                    b.EventId == box.EventId &&
                    b.Rectangle.X == box.Rectangle.X &&
                    b.Rectangle.Y == box.Rectangle.Y &&
                    b.Rectangle.Width == box.Rectangle.Width &&
                    b.Rectangle.Height == box.Rectangle.Height);

                if (!exists)
                {
                    var copiedBox = new BoundingBox
                    {
                        FrameIndex = frame,
                        Rectangle = new Rectangle(box.Rectangle.X, box.Rectangle.Y, box.Rectangle.Width, box.Rectangle.Height),
                        Label = "event",
                        PersonId = 0,
                        VehicleId = 0,
                        EventId = box.EventId,
                        Action = box.Action,
                        VehicleName = box.VehicleName,
                        EventName = box.EventName
                    };
                    boundingBoxes.Add(copiedBox);
                }
            }

            InvalidateBoxCache();
            UpdateBoxCount();
            UpdateBboxListDisplay();
            System.Diagnostics.Debug.WriteLine($"[Event 전파 완료] {endFrame - startFrame + 1}개 프레임에 Event 박스 전파됨");
        }

        /// <summary>
        /// Event → Person/Vehicle 변경 시 전파된 Event 박스 삭제
        /// </summary>
        private void RemovePropagatedEventBoxes(BoundingBox box)
        {
            if (box.Label != "event")
            {
                System.Diagnostics.Debug.WriteLine($"[전파 박스 삭제 중단] Label이 event가 아님: {box.Label}");
                return;
            }

            // 현재 프레임 이후의 같은 EventId, Rectangle을 가진 박스들 찾기
            var boxesToRemove = boundingBoxes.Where(b =>
                b.Label == "event" &&
                b.EventId == box.EventId &&
                b.Rectangle.X == box.Rectangle.X &&
                b.Rectangle.Y == box.Rectangle.Y &&
                b.Rectangle.Width == box.Rectangle.Width &&
                b.Rectangle.Height == box.Rectangle.Height &&
                b.FrameIndex > box.FrameIndex).ToList();

            if (boxesToRemove.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[전파 박스 삭제] {boxesToRemove.Count}개 박스 삭제 시작");
                
                foreach (var boxToRemove in boxesToRemove)
                {
                    boundingBoxes.Remove(boxToRemove);
                }

                // 관련된 Event Waypoint 삭제
                var eventWaypoint = waypointMarkers.FirstOrDefault(w =>
                    w.Label == "event" &&
                    w.EntryFrame == box.FrameIndex);

                if (eventWaypoint != null)
                {
                    waypointMarkers.Remove(eventWaypoint);
                    System.Diagnostics.Debug.WriteLine($"[전파 박스 삭제] Event Waypoint 삭제 완료");
                }

                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();
                UpdateWaypointListView();
                panelTimeline.Invalidate();
                System.Diagnostics.Debug.WriteLine($"[전파 박스 삭제 완료] {boxesToRemove.Count}개 박스 삭제됨");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[전파 박스 삭제] 삭제할 박스 없음");
            }
        }

        /// <summary>
        /// Event 박스가 새로 생성되었을 때 현재 프레임부터 Waypoint Exit까지 전파
        /// (Waypoint 중간 프레임에서 생성된 Event도 자동으로 Exit까지 전파됨)
        /// </summary>
        private void PropagateEventBoxIfNeeded(BoundingBox box)
        {
            System.Diagnostics.Debug.WriteLine($"[Event 전파 시작] FrameIndex={box.FrameIndex}, Label={box.Label}, EventId={box.EventId}");
            
            // Event 라벨이 아니면 전파하지 않음
            if (box.Label != "event")
            {
                System.Diagnostics.Debug.WriteLine($"[Event 전파 중단] Event 라벨이 아님: {box.Label}");
                return;
            }

            // 현재 Waypoint 목록 확인
            System.Diagnostics.Debug.WriteLine($"[Event 전파] 현재 Waypoint 개수: {waypointMarkers.Count}");
            foreach (var wm in waypointMarkers)
            {
                System.Diagnostics.Debug.WriteLine($"  - Waypoint: Entry={wm.EntryFrame}, Exit={wm.ExitFrame}");
            }

            // 현재 박스가 속한 Waypoint 찾기
            var waypoint = waypointMarkers.FirstOrDefault(w =>
                box.FrameIndex >= w.EntryFrame &&
                box.FrameIndex <= w.ExitFrame);

            if (waypoint == null)
            {
                // Waypoint가 없으면 전파 불가 (Entry/Exit 마커가 아직 설정되지 않음)
                System.Diagnostics.Debug.WriteLine($"[Event 전파 중단] 프레임 {box.FrameIndex}에 해당하는 Waypoint가 없어 전파하지 않음");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[Event 전파] Waypoint 발견: Entry={waypoint.EntryFrame}, Exit={waypoint.ExitFrame}");

            int startFrame = box.FrameIndex + 1; // 다음 프레임부터
            int endFrame = waypoint.ExitFrame;

            // 현재 프레임이 Exit 프레임이면 전파할 필요 없음
            if (box.FrameIndex >= endFrame)
            {
                System.Diagnostics.Debug.WriteLine($"[Event 전파 중단] 프레임 {box.FrameIndex}가 Exit 프레임({endFrame})이므로 전파 불필요");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[Event 전파 시작] 프레임 {box.FrameIndex}에서 생성된 Event를 {startFrame}~{endFrame}까지 전파 ({endFrame - startFrame + 1}개 프레임)");

            int createdCount = 0;
            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                // 이미 동일한 EventId와 Rectangle을 가진 박스가 존재하는지 확인
                bool exists = boundingBoxes.Any(b =>
                    b.FrameIndex == frame &&
                    b.Label == "event" &&
                    b.EventId == box.EventId &&
                    b.Rectangle.X == box.Rectangle.X &&
                    b.Rectangle.Y == box.Rectangle.Y &&
                    b.Rectangle.Width == box.Rectangle.Width &&
                    b.Rectangle.Height == box.Rectangle.Height);

                if (!exists)
                {
                    var newBox = new BoundingBox
                    {
                        Rectangle = box.Rectangle,
                        Label = box.Label,
                        FrameIndex = frame,
                        PersonId = box.PersonId,
                        VehicleId = box.VehicleId,
                        EventId = box.EventId,
                        Action = "waypoint"
                    };
                    boundingBoxes.Add(newBox);
                    createdCount++;
                }
            }

            if (createdCount > 0)
            {
                InvalidateBoxCache();
                UpdateBoxCount();
                System.Diagnostics.Debug.WriteLine($"[Event 생성 전파 완료] {createdCount}개 프레임에 박스 생성됨 ({startFrame}~{endFrame})");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Event 생성 전파] 생성할 박스 없음 (이미 존재)");
            }
        }

        /// <summary>
        /// Event 박스가 중간 프레임에서 수정되었을 때 해당 프레임부터 Exit까지 전파
        /// </summary>
        private void PropagateEventBoxFromCurrentFrame(BoundingBox box)
        {
            // Event 라벨이 아니면 전파하지 않음
            if (box.Label != "event")
                return;

            // 현재 박스가 속한 Waypoint 찾기
            var waypoint = waypointMarkers.FirstOrDefault(w =>
                box.FrameIndex >= w.EntryFrame &&
                box.FrameIndex <= w.ExitFrame);

            if (waypoint == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Event 전파 실패] 프레임 {box.FrameIndex}에 해당하는 Waypoint를 찾을 수 없습니다.");
                return;
            }

            int startFrame = box.FrameIndex + 1; // 다음 프레임부터
            int endFrame = waypoint.ExitFrame;

            // 현재 프레임이 Exit 프레임이면 전파할 필요 없음
            if (box.FrameIndex >= endFrame)
                return;

            System.Diagnostics.Debug.WriteLine($"[Event 전파] 프레임 {box.FrameIndex}에서 수정 감지, {startFrame}~{endFrame}까지 전파 시작");

            // 현재 프레임 이후의 동일한 Event 박스들을 찾아서 업데이트
            var boxesToUpdate = boundingBoxes.Where(b =>
                b.FrameIndex > box.FrameIndex &&
                b.FrameIndex <= endFrame &&
                b.Label == "event" &&
                b.EventId == box.EventId).ToList();

            int updatedCount = 0;
            foreach (var targetBox in boxesToUpdate)
            {
                targetBox.Rectangle = box.Rectangle;
                updatedCount++;
            }

            // 업데이트된 박스가 없으면 새로 생성
            if (updatedCount == 0)
            {
                for (int frame = startFrame; frame <= endFrame; frame++)
                {
                    bool exists = boundingBoxes.Any(b =>
                        b.FrameIndex == frame &&
                        b.Label == "event" &&
                        b.EventId == box.EventId);

                    if (!exists)
                    {
                        var newBox = new BoundingBox
                        {
                            Rectangle = box.Rectangle,
                            Label = box.Label,
                            FrameIndex = frame,
                            PersonId = box.PersonId,
                            VehicleId = box.VehicleId,
                            EventId = box.EventId,
                            Action = "waypoint"
                        };
                        boundingBoxes.Add(newBox);
                        updatedCount++;
                    }
                }
            }

            if (updatedCount > 0)
            {
                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();
                
                System.Diagnostics.Debug.WriteLine($"[Event 전파 완료] {updatedCount}개 프레임 업데이트됨 ({startFrame}~{endFrame})");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Event 전파] 업데이트할 박스 없음 (이미 존재하거나 범위 밖)");
            }
        }
        #endregion

        #region Tracking Algorithm
        
        // ✅ 여러 Waypoint를 순차적으로 추적 (동시 실행 방지)
        private async void PerformSequentialTracking(List<WaypointMarker> waypoints)
        {
            try
            {
                int totalCount = waypoints.Count;
                int currentIndex = 0;
                int totalBoxesAdded = 0;

                foreach (var waypoint in waypoints)
                {
                    currentIndex++;
                    System.Diagnostics.Debug.WriteLine($"[순차 추적] {currentIndex}/{totalCount} - {waypoint.Label} ID={waypoint.ObjectId}");
                    
                    int beforeCount = boundingBoxes.Count;
                    
                    // ✅ 각 Waypoint를 순차적으로 추적 (await으로 대기)
                    await PerformTrackingForWaypointAsync(waypoint, true);
                    
                    int afterCount = boundingBoxes.Count;
                    totalBoxesAdded += (afterCount - beforeCount);
                }

                // ✅ 모든 추적 완료 후 JSON 저장 및 재로드 (한 번만)
                if (!string.IsNullOrEmpty(currentVideoFile))
                {
                    string videoDir = Path.GetDirectoryName(currentVideoFile);
                    string saveDir = Path.Combine(videoDir, "labels");
                    
                    if (!Directory.Exists(saveDir))
                    {
                        Directory.CreateDirectory(saveDir);
                    }
                    
                    string fileName = Path.GetFileNameWithoutExtension(currentVideoFile) + "_labels.json";
                    string jsonFilePath = Path.Combine(saveDir, fileName);
                    
                    // JSON 저장
                    await Task.Run(() => ExportToJsonExtended(jsonFilePath));
                    System.Diagnostics.Debug.WriteLine($"[JSON 저장] {jsonFilePath}");
                    
                    // JSON 재로드하여 추적 데이터 기반으로 표시
                    if (File.Exists(jsonFilePath))
                    {
                        LoadLabelingData(jsonFilePath);
                        System.Diagnostics.Debug.WriteLine($"[JSON 재로드] 완료");
                    }

                    MessageBox.Show(
                        $"✅ {totalCount}개 Waypoint 추적 완료!\n\n" +
                        $"총 {totalBoxesAdded}개 BBox 추가됨\n" +
                        $"💾 JSON 저장 및 재로드 완료\n\n" +
                        $"저장 위치: {jsonFilePath}",
                        "추적 완료",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"✅ {totalCount}개 Waypoint 추적 완료!\n\n" +
                        $"총 {totalBoxesAdded}개 BBox 추가됨",
                        "추적 완료",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"순차 추적 중 오류 발생:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async Task PerformTrackingForWaypointAsync(WaypointMarker waypoint, bool useYolo = false)
        {
            try
            {
                // ✅ Entry 프레임에서 waypoint의 ObjectId와 Label에 해당하는 박스만 찾기
                List<BoundingBox> startBoxes = new List<BoundingBox>();
                
                if (waypoint.Label == "person")
                {
                    startBoxes = boundingBoxes
                        .Where(b => b.FrameIndex == waypoint.EntryFrame && 
                                   b.Label == "person" && 
                                   b.PersonId == waypoint.ObjectId)
                        .ToList();
                }
                else if (waypoint.Label == "vehicle")
                {
                    startBoxes = boundingBoxes
                        .Where(b => b.FrameIndex == waypoint.EntryFrame && 
                                   b.Label == "vehicle" && 
                                   b.VehicleId == waypoint.ObjectId)
                        .ToList();
                }
                else if (waypoint.Label == "event")
                {
                    startBoxes = boundingBoxes
                        .Where(b => b.FrameIndex == waypoint.EntryFrame && 
                                   b.Label == "event" && 
                                   b.EventId == waypoint.ObjectId)
                        .ToList();
                }
                
                System.Diagnostics.Debug.WriteLine($"[추적 시작] {waypoint.Label} ID={waypoint.ObjectId}, startBoxes={startBoxes.Count}");

                if (startBoxes.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[추적 스킵] {waypoint.Label} ID={waypoint.ObjectId} - Entry 프레임에 박스 없음");
                    return;
                }

                // ✅ Event는 자동 추적 안함 (전파 기능만 사용)
                if (waypoint.Label == "event")
                {
                    System.Diagnostics.Debug.WriteLine($"[추적 스킵] Event는 자동 추적 안함");
                    return;
                }

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
                    
                    // 각 startBox에 대해 개별적으로 YOLO 추적 수행
                    foreach (var startBox in startBoxes)
                    {
                        
                        var trackedBoxes = await Task.Run(() => trackingEngine.TrackObjects(
                            videoCapture,
                            startBox,
                            waypoint.EntryFrame,
                            waypoint.ExitFrame,
                            fps));
                        
                        allTrackedBoxes.AddRange(trackedBoxes);
                    }
                }
                else 
                {
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
                InvalidateBoxCache();

                // 추적이 성공적으로 완료되면 Entry 프레임의 사용자가 지정한 초기 박스 삭제
                if (allTrackedBoxes.Count > 0)
                {
                    foreach (var startBox in startBoxes)
                    {
                        if (boundingBoxes.Contains(startBox))
                        {
                            boundingBoxes.Remove(startBox);
                        }
                    }
                    
                    InvalidateBoxCache();
                    AddUndoAction(new UndoAction { Type = UndoActionType.Tracking, TrackedBoxes = allTrackedBoxes });
                }

                UpdateBoxCount();
                UpdateBboxListDisplay();
                
                // ✅ 개별 waypoint 추적 완료 로그
                System.Diagnostics.Debug.WriteLine($"[추적 완료] {waypoint.Label} ID={waypoint.ObjectId}, BBox 추가={allTrackedBoxes.Count}개");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[추적 오류] {waypoint.Label} ID={waypoint.ObjectId}: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"추적 중 오류 발생:\n\n" +
                    $"Waypoint: {waypoint.Label} ID={waypoint.ObjectId}\n" +
                    $"Error: {ex.Message}\n\n" +
                    $"StackTrace:\n{ex.StackTrace}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                throw; // 예외를 상위로 전파하여 순차 추적이 중단되도록
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

                // ✅ JSON 로드 시 모든 기존 데이터 초기화
                boundingBoxes.Clear();
                waypointMarkers.Clear();
                categoryMap.Clear();
                selectedBox = null;
                undoStack.Clear();
                redoStack.Clear();
                lastRenderedWaypoint = null;
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
                    else if (catId >= 19 && catId <= 22)  // ✅ 19~22로 수정 (4개 이벤트)
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
                        else if (categoryName.Contains("contact") || categoryName.Contains("exchange") || 
                                 categoryName.Contains("board") || categoryName.Contains("final"))
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

                    // EventId 계산 로직 수정: CategoryId에서 역산
                    int personId = 0;
                    int vehicleId = 0;
                    int eventId = 0;
                    
                    if (label == "person")
                    {
                        personId = trackId;
                    }
                    else if (label == "vehicle")
                    {
                        // Vehicle: CategoryId 15~18 → VehicleId 1~4
                        vehicleId = catId >= 15 && catId <= 18 ? (catId - 14) : trackId;
                    }
                    else if (label == "event")
                    {
                        // Event: CategoryId 19~22 → EventId 1~4
                        eventId = catId >= 19 && catId <= 22 ? (catId - 18) : trackId;
                    }

                    var box = new BoundingBox
                    {
                        FrameIndex = frameNumber, // 실제 프레임 번호 사용
                        Rectangle = new Rectangle(annotation.Bbox[0], annotation.Bbox[1], annotation.Bbox[2], annotation.Bbox[3]),
                        Label = label,
                        PersonId = personId,
                        VehicleId = vehicleId,
                        EventId = eventId,
                        Action = "waypoint"
                    };

                    boundingBoxes.Add(box);

                    if (annotation.Id >= nextAnnotationId)
                        nextAnnotationId = annotation.Id + 1;

                    // ✅ 웨이포인트 정보 복원 (Label + ObjectId별로 분리)
                    if (annotation.TrackInfo != null && 
                        annotation.TrackInfo.Entry != null && 
                        annotation.TrackInfo.Exit != null)
                    {
                        int entryFrame = annotation.TrackInfo.Entry.Frame;
                        int exitFrame = annotation.TrackInfo.Exit.Frame;

                        // ObjectId 결정 (Label에 따라)
                        int objectId = 0;
                        if (box.Label == "person") objectId = box.PersonId;
                        else if (box.Label == "vehicle") objectId = box.VehicleId;
                        else if (box.Label == "event") objectId = box.EventId;

                        // ✅ 같은 Label, ObjectId, Entry, Exit를 가진 Waypoint가 이미 있는지 확인
                        // → 이렇게 해야 같은 객체(예: person_01)의 여러 Waypoint가 통합되지 않음
                        bool waypointExists = waypointMarkers.Any(w => 
                            w.Label == box.Label &&
                            w.ObjectId == objectId &&
                            w.EntryFrame == entryFrame && 
                            w.ExitFrame == exitFrame);

                        if (!waypointExists)
                        {
                            System.Drawing.Color waypointColor;
                            
                            // Label별로 색상 지정
                            if (box.Label == "person")
                            {
                                waypointColor = System.Drawing.Color.FromArgb(255, 107, 107); // 빨강
                            }
                            else if (box.Label == "vehicle")
                            {
                                waypointColor = System.Drawing.Color.FromArgb(107, 158, 255); // 파랑
                            }
                            else if (box.Label == "event")
                            {
                                waypointColor = System.Drawing.Color.FromArgb(107, 255, 107); // 초록
                            }
                            else
                            {
                                waypointColor = markerColors[waypointMarkers.Count % markerColors.Length];
                            }
                            
                            var waypoint = new WaypointMarker
                            {
                                ObjectId = objectId, // ✅ PersonId/VehicleId/EventId 저장
                                Label = box.Label, // Person/Vehicle/Event 라벨 유지
                                EntryFrame = entryFrame,
                                ExitFrame = exitFrame,
                                EntryTime = FormatFrameTime(entryFrame),
                                ExitTime = FormatFrameTime(exitFrame),
                                MarkerColor = waypointColor
                            };

                            waypointMarkers.Add(waypoint);
                            System.Diagnostics.Debug.WriteLine($"[JSON 로드] {box.Label} Waypoint 복원: ID={objectId}, {entryFrame}~{exitFrame}");
                        }
                    }
                }

                // ✅ UI 전체 갱신: Waypoint, BboxList, BoxCount
                UpdateWaypointListView();
                UpdateBboxListDisplay(); // Labels 패널도 갱신
                
                InvalidateBoxCache();
                UpdateBoxCount();
                pictureBoxVideo.Invalidate();
                
                System.Diagnostics.Debug.WriteLine($"[JSON 로드 완료] 박스 {boundingBoxes.Count}개, Waypoint {waypointMarkers.Count}개 로드됨");
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

        /// <summary>
        /// 현재 비디오에 대한 JSON 파일 삭제
        /// </summary>
        private void DeleteJsonFileForCurrentVideo()
        {
            try
            {
                if (string.IsNullOrEmpty(currentVideoFile))
                    return;

                string videoDir = Path.GetDirectoryName(currentVideoFile);
                if (string.IsNullOrEmpty(videoDir) || !Directory.Exists(videoDir))
                    return;

                string saveDir = Path.Combine(videoDir, "labels");
                if (!Directory.Exists(saveDir))
                    return;

                string fileName = Path.GetFileNameWithoutExtension(currentVideoFile) + "_labels.json";
                string jsonPath = Path.Combine(saveDir, fileName);

                if (File.Exists(jsonPath))
                {
                    File.Delete(jsonPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"JSON 파일 삭제 중 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                        // ✅ 수정: 현재 박스가 속한 특정 Waypoint를 찾아서 그 Entry/Exit 사용
                        int entryFrame = box.FrameIndex;
                        int exitFrame = box.FrameIndex;
                        
                        // 현재 박스가 속한 Waypoint 찾기
                        var matchingWaypoint = waypointMarkers.FirstOrDefault(w => 
                            w.Label == box.Label &&
                            w.ObjectId == boxId &&
                            box.FrameIndex >= w.EntryFrame &&
                            box.FrameIndex <= w.ExitFrame);
                        
                        if (matchingWaypoint != null)
                        {
                            // Waypoint가 있으면 그 Entry/Exit 사용
                            entryFrame = matchingWaypoint.EntryFrame;
                            exitFrame = matchingWaypoint.ExitFrame;
                            System.Diagnostics.Debug.WriteLine($"[JSON 저장] {box.Label} ID={boxId}, Frame={box.FrameIndex} → Waypoint {entryFrame}~{exitFrame}");
                        }
                        else
                        {
                            // Waypoint가 없으면 같은 ObjectId의 모든 박스 범위 사용
                            var sameObjectBoxes = boundingBoxes
                                .Where(b => b.Label == box.Label && GetBoxId(b) == boxId)
                                .ToList();
                            
                            if (sameObjectBoxes.Any())
                            {
                                entryFrame = sameObjectBoxes.Min(b => b.FrameIndex);
                                exitFrame = sameObjectBoxes.Max(b => b.FrameIndex);
                            }
                            System.Diagnostics.Debug.WriteLine($"[JSON 저장] {box.Label} ID={boxId}, Frame={box.FrameIndex} → Waypoint 없음, 범위: {entryFrame}~{exitFrame}");
                        }

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
        /// <summary>
        /// Q키: Event 종료 - 현재 프레임부터 Exit까지 Event 박스 삭제
        /// </summary>
        private void TerminateEventFromCurrentFrame()
        {
            // 현재 프레임에 Event 박스가 있는지 확인
            var eventBoxesAtFrame = boundingBoxes
                .Where(b => b.FrameIndex == currentFrameIndex && b.Label == "event")
                .ToList();
            
            if (eventBoxesAtFrame.Count == 0)
            {
                MessageBox.Show(
                    "현재 프레임에 Event 박스가 없습니다.\n\n" +
                    "Event 박스가 있는 프레임으로 이동 후 Q키를 눌러주세요.",
                    "Event 없음",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            
            // 여러 Event가 있을 경우 선택 다이얼로그
            BoundingBox targetEvent = null;
            if (eventBoxesAtFrame.Count == 1)
            {
                targetEvent = eventBoxesAtFrame[0];
            }
            else
            {
                // 여러 Event 중 선택
                string[] eventTypes = { "contact", "exchange", "board", "final_exchange" };
                var eventNames = eventBoxesAtFrame.Select(b => 
                {
                    string name = b.EventId > 0 && b.EventId <= eventTypes.Length 
                        ? eventTypes[b.EventId - 1] 
                        : b.EventId.ToString();
                    return $"{name}_{b.EventId:D2}";
                }).ToArray();
                
                // 간단하게 첫 번째 것 선택 (나중에 다이얼로그로 변경 가능)
                targetEvent = eventBoxesAtFrame[0];
            }
            
            // 현재 Event의 Waypoint 찾기 (Event Label, EventId로 매칭)
            var eventWaypoint = waypointMarkers.FirstOrDefault(w =>
                w.Label == "event" &&
                currentFrameIndex >= w.EntryFrame &&
                currentFrameIndex <= w.ExitFrame);
            
            if (eventWaypoint == null)
            {
                MessageBox.Show(
                    "Event Waypoint를 찾을 수 없습니다.",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            
            // 현재 프레임부터 영상 끝까지 삭제 (Q키로 종료)
            var boxesToDelete = boundingBoxes
                .Where(b => 
                    b.Label == "event" &&
                    b.EventId == targetEvent.EventId &&
                    b.Rectangle.X == targetEvent.Rectangle.X &&
                    b.Rectangle.Y == targetEvent.Rectangle.Y &&
                    b.Rectangle.Width == targetEvent.Rectangle.Width &&
                    b.Rectangle.Height == targetEvent.Rectangle.Height &&
                    b.FrameIndex >= currentFrameIndex)
                .ToList();
            
            if (boxesToDelete.Count == 0)
            {
                MessageBox.Show(
                    "삭제할 Event 박스가 없습니다.",
                    "알림",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            
            // 삭제 확인
            string[] eventTypes2 = { "contact", "exchange", "board", "final_exchange" };
            string eventName = targetEvent.EventId > 0 && targetEvent.EventId <= eventTypes2.Length 
                ? eventTypes2[targetEvent.EventId - 1] 
                : targetEvent.EventId.ToString();
            
            var result = MessageBox.Show(
                $"'{eventName}' Event를 현재 프레임({currentFrameIndex})에서 종료하시겠습니까?\n\n" +
                $"삭제될 프레임: {currentFrameIndex} ~ 영상 끝 ({boxesToDelete.Count}개)",
                "Event 종료",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                foreach (var box in boxesToDelete)
                {
                    boundingBoxes.Remove(box);
                }
                
                // Event Waypoint의 ExitFrame을 현재 프레임 -1로 업데이트
                eventWaypoint.ExitFrame = currentFrameIndex - 1;
                TimeSpan exitTime = TimeSpan.FromSeconds((currentFrameIndex - 1) / fps);
                eventWaypoint.ExitTime = exitTime.ToString(@"hh\:mm\:ss");
                
                InvalidateBoxCache();
                UpdateBoxCount();
                UpdateBboxListDisplay();
                UpdateWaypointListView(); // Waypoint ListView 업데이트
                panelTimeline.Invalidate();
                pictureBoxVideo.Invalidate();
                
                MessageBox.Show(
                    $"'{eventName}' Event가 프레임 {currentFrameIndex}에서 종료되었습니다.\n\n" +
                    $"삭제된 박스: {boxesToDelete.Count}개",
                    "완료",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        #endregion

        #region Keyboard Shortcuts
        
        /// <summary>
        /// 방향키 등 특수 키를 Form 레벨에서 먼저 처리하여 패널 포커스 문제 해결
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // 영상이 로드되지 않은 경우에도 Ctrl 조합은 처리
            bool isVideoLoaded = videoCapture != null && videoCapture.IsOpened();
            
            // 방향키: 영상 로드된 경우만 처리
            if (isVideoLoaded)
            {
                if (keyData == Keys.Left)
                {
                    // 5초씩 뒤로 이동
                    int framesToMove = (int)(fps * 5);
                    int newFrame = Math.Max(0, currentFrameIndex - framesToMove);
                    LoadFrame(newFrame);
                    return true; // 이벤트 처리 완료
                }
                else if (keyData == Keys.Right)
                {
                    // 5초씩 앞으로 이동
                    int framesToMove = (int)(fps * 5);
                    int newFrame = Math.Min(totalFrames - 1, currentFrameIndex + framesToMove);
                    LoadFrame(newFrame);
                    return true; // 이벤트 처리 완료
                }
            }
            
            // 처리하지 못한 키는 기본 동작 수행
            return base.ProcessCmdKey(ref msg, keyData);
        }
        
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            // F1/F2/F3: Person/Vehicle/Event 라벨 선택 (영상 로드 여부와 무관)
            if (!e.Control && !e.Shift && !e.Alt)
            {
                if (e.KeyCode == Keys.F1)
                {
                    btnLabelPerson_Click(sender, e);
                    e.Handled = true;
                    return;
                }
                else if (e.KeyCode == Keys.F2)
                {
                    btnLabelVehicle_Click(sender, e);
                    e.Handled = true;
                    return;
                }
                else if (e.KeyCode == Keys.F3)
                {
                    btnLabelEvent_Click(sender, e);
                    e.Handled = true;
                    return;
                }
            }
            
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

            // 방향키는 ProcessCmdKey에서 처리하므로 여기서는 제외
            // Space bar - 재생/일시정지
            if (e.KeyCode == Keys.Space)
            {
                btnPlay_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Shift && e.KeyCode == Keys.OemPeriod) // Shift + > (> 키)
            {
                // 속도 빠르게 (1.0x → 16.0x)
                if (playbackSpeed < 1.0) playbackSpeed = 1.0;
                else if (playbackSpeed < 4.0) playbackSpeed = 4.0;
                else if (playbackSpeed < 8.0) playbackSpeed = 8.0;
                else if (playbackSpeed < 16.0) playbackSpeed = 16.0;
                
                if (isPlaying)
                    lastFrameTime = DateTime.Now.Ticks / 10000;
                UpdateTimeLabels();
                e.Handled = true;
            }
            else if (e.Shift && e.KeyCode == Keys.Oemcomma) // Shift + < (< 키)
            {
                // 속도 느리게 (16.0x → 1.0x)
                if (playbackSpeed > 8.0) playbackSpeed = 8.0;
                else if (playbackSpeed > 4.0) playbackSpeed = 4.0;
                else if (playbackSpeed > 1.0) playbackSpeed = 1.0;
                else playbackSpeed = 0.5;
                
                if (isPlaying)
                    lastFrameTime = DateTime.Now.Ticks / 10000;
                UpdateTimeLabels();
                e.Handled = true;
            }
            else if (e.Shift && e.KeyCode == Keys.OemQuestion) // Shift + / 키
            {
                // 속도 초기화
                playbackSpeed = 1.0;
                if (isPlaying)
                    lastFrameTime = DateTime.Now.Ticks / 10000;
                UpdateTimeLabels();
                MessageBox.Show("재생 속도를 1.0x로 초기화했습니다.", "속도 초기화", MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Handled = true;
            }
            else if (selectedBox != null && !e.Control && (e.KeyCode == Keys.W || e.KeyCode == Keys.A || e.KeyCode == Keys.S || e.KeyCode == Keys.D))
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
                    // Ctrl+Shift+Z: Redo
                    Redo();
                    e.Handled = true;
                }
                else
                {
                    // Ctrl+Z: Undo
                    Undo();
                    e.Handled = true;
                }
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                // Ctrl+Y: Redo
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
            else if (e.KeyCode == Keys.Q && !e.Control && !e.Alt)
            {
                // Q키: Event 종료 (현재 프레임부터 Exit까지 삭제)
                TerminateEventFromCurrentFrame();
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

                    // ✅ 단일 waypoint도 순차 추적 함수 사용
                    if (useYolo)
                    {
                        PerformSequentialTracking(new List<WaypointMarker> { lastWaypoint });
                    }
                    else
                    {
                        MessageBox.Show(
                            "현재는 YOLO 추적만 지원합니다.",
                            "정보",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
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
            else if (e.Control && e.KeyCode == Keys.S)
            {
                // Ctrl+S: JSON 저장 및 추출
                btnExportJson_Click(sender, e);
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

        #region 창 이동 및 크기 조절

        /// <summary>
        /// 헤더 드래그로 창 이동 기능 설정
        /// </summary>
        private void SetupWindowDragHandlers()
        {
            // panelHeader를 드래그하면 창이 이동
            panelHeader.MouseDown += PanelHeader_MouseDown;
            panelHeader.MouseMove += PanelHeader_MouseMove;
            panelHeader.MouseUp += PanelHeader_MouseUp;
            panelHeader.DoubleClick += PanelHeader_DoubleClick;

            // labelTitle도 드래그 가능하게
            labelTitle.MouseDown += PanelHeader_MouseDown;
            labelTitle.MouseMove += PanelHeader_MouseMove;
            labelTitle.MouseUp += PanelHeader_MouseUp;
            labelTitle.DoubleClick += PanelHeader_DoubleClick;
        }

        private void PanelHeader_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 최대화 상태가 아닐 때만 이동 가능
                if (this.WindowState != FormWindowState.Maximized)
                {
                    isMovingWindow = true;
                    windowMoveStartPoint = e.Location;
                }
            }
        }

        private void PanelHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMovingWindow)
            {
                // 마우스 이동량 계산
                System.Drawing.Point currentScreenPoint = Control.MousePosition;
                System.Drawing.Point offset = new System.Drawing.Point(
                    currentScreenPoint.X - windowMoveStartPoint.X,
                    currentScreenPoint.Y - windowMoveStartPoint.Y
                );

                this.Location = offset;
            }
        }

        private void PanelHeader_MouseUp(object sender, MouseEventArgs e)
        {
            isMovingWindow = false;
        }

        /// <summary>
        /// 헤더 더블클릭으로 최대화/복원 토글
        /// </summary>
        private void PanelHeader_DoubleClick(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
            }
            // Resize 이벤트가 자동으로 UpdateMaximizeButtonIcon을 호출함
        }

        /// <summary>
        /// Windows 메시지를 가로채서 창 테두리 크기 조절 기능 추가
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTCLIENT = 1;
            const int HTLEFT = 10;
            const int HTRIGHT = 11;
            const int HTTOP = 12;
            const int HTTOPLEFT = 13;
            const int HTTOPRIGHT = 14;
            const int HTBOTTOM = 15;
            const int HTBOTTOMLEFT = 16;
            const int HTBOTTOMRIGHT = 17;

            if (m.Msg == WM_NCHITTEST && this.WindowState != FormWindowState.Maximized)
            {
                base.WndProc(ref m);

                // 마우스 위치 가져오기
                System.Drawing.Point pos = this.PointToClient(new System.Drawing.Point(m.LParam.ToInt32()));

                int width = this.ClientSize.Width;
                int height = this.ClientSize.Height;

                // 모서리 및 테두리 영역 감지
                if (pos.X <= RESIZE_BORDER_WIDTH && pos.Y <= RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTTOPLEFT;
                }
                else if (pos.X >= width - RESIZE_BORDER_WIDTH && pos.Y <= RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTTOPRIGHT;
                }
                else if (pos.X <= RESIZE_BORDER_WIDTH && pos.Y >= height - RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTBOTTOMLEFT;
                }
                else if (pos.X >= width - RESIZE_BORDER_WIDTH && pos.Y >= height - RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                }
                else if (pos.X <= RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTLEFT;
                }
                else if (pos.X >= width - RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTRIGHT;
                }
                else if (pos.Y <= RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTTOP;
                }
                else if (pos.Y >= height - RESIZE_BORDER_WIDTH)
                {
                    m.Result = (IntPtr)HTBOTTOM;
                }
            }
            else
            {
                base.WndProc(ref m);
            }
        }

        #endregion
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