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
        public int OriginalPersonId { get; set; }
        public List<BoundingBox> TrackedBoxes { get; set; }
    }

    public class WaypointMarker
    {
        public int EntryFrame { get; set; }
        public int ExitFrame { get; set; }
        public Color MarkerColor { get; set; }
        public string EntryTime { get; set; }
        public string ExitTime { get; set; }
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

            for (int i = startFrame; i <= endFrame; i++)
            {
                if (!videoCapture.Read(frame) || frame.Empty())
                    break;

                try
                {
                    // Mat을 임시 파일로 저장
                    Cv2.ImWrite(_tempImagePath, frame);

                    // YoloSharp의 Detect 메서드에 파일 경로 전달
                    var result = _predictor.Detect(_tempImagePath);

                    // 결과에서 BoundingBox 추출 (열거형으로 접근)
                    foreach (var detection in result)
                    {
                        var trackedBox = new BoundingBox
                        {
                            FrameIndex = i,
                            Rectangle = new Rectangle(
                                (int)detection.Bounds.Left,
                                (int)detection.Bounds.Top,
                                (int)detection.Bounds.Width,
                                (int)detection.Bounds.Height
                            ),
                            Label = detection.Name?.Name ?? startBox.Label,
                            PersonId = startBox.PersonId,
                            Action = "waypoint"
                        };

                        trackedBoxes.Add(trackedBox);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"프레임 {i} 추론 오류: {ex.Message}");
                    continue;
                }
            }

            frame.Dispose();

            // 임시 파일 정리
            try
            {
                if (File.Exists(_tempImagePath))
                    File.Delete(_tempImagePath);
            }
            catch { }

            return trackedBoxes;
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

        private TrackingEngine trackingEngine = null;  
        private bool isYoloAvailable = false;
        private string yoloModelPath = Path.Combine(Application.StartupPath, @"..\..\..\..\yolov8n.onnx");

        // SRT 자막 관련
        private string currentSrtFile = "";
        private List<SubtitleEntry> subtitleEntries = new List<SubtitleEntry>();
        private bool isFFmpegAvailable = false;


        public Form1()
        {
            InitializeComponent();
            UpdateBoxCount();
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
                    selectedBox = null;
                    UpdateBoxCount();
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
            string subtitleText = GetCurrentSubtitle();
            
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
            entryFrameIndex = currentFrameIndex;
            TimeSpan time = TimeSpan.FromSeconds(currentFrameIndex / fps);
            btnEntry.Text = $"Entry: {time:hh\\:mm\\:ss}";
            panelTimeline.Invalidate();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            if (!entryFrameIndex.HasValue)
            {
                MessageBox.Show("먼저 Entry를 설정해주세요.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            exitFrameIndex = currentFrameIndex;
            TimeSpan exitTime = TimeSpan.FromSeconds(currentFrameIndex / fps);
            TimeSpan entryTime = TimeSpan.FromSeconds(entryFrameIndex.Value / fps);

            btnExit.Text = $"Exit: {exitTime:hh\\:mm\\:ss}";

            var waypoint = new WaypointMarker
            {
                EntryFrame = entryFrameIndex.Value,
                ExitFrame = exitFrameIndex.Value,
                MarkerColor = markerColors[currentColorIndex % markerColors.Length],
                EntryTime = entryTime.ToString(@"hh\:mm\:ss"),
                ExitTime = exitTime.ToString(@"hh\:mm\:ss")
            };

            waypointMarkers.Add(waypoint);
            currentColorIndex++;

            var item = new ListViewItem(waypoint.EntryTime);
            item.SubItems.Add(waypoint.ExitTime);
            item.SubItems.Add("●");
            item.ForeColor = waypoint.MarkerColor;
            item.Tag = waypoint;
            listViewWaypoints.Items.Add(item);

            entryFrameIndex = null;
            exitFrameIndex = null;
            btnEntry.Text = "Entry: 00:00:00";
            btnExit.Text = "Exit: 00:00:00";

            panelTimeline.Invalidate();

            var result = MessageBox.Show(
                "Waypoint가 생성되었습니다.\n자동 추적을 수행하시겠습니까?",
                "Auto Tracking",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                PerformTrackingForWaypoint(waypoint);
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
                var boxesToDelete = boundingBoxes
                    .Where(b => b.FrameIndex >= waypoint.EntryFrame && b.FrameIndex <= waypoint.ExitFrame)
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
                if (!entryFrameIndex.HasValue)
                {
                    MessageBox.Show("먼저 Entry 마커를 설정해주세요. (E키)", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                isDrawing = true;
                drawStartPoint = e.Location;

                drawingBox = new BoundingBox
                {
                    FrameIndex = currentFrameIndex,
                    Rectangle = new Rectangle(e.Location, new System.Drawing.Size(0, 0)),
                    Label = "person",
                    PersonId = 1,
                    Action = "waypoint"
                };
            }
            else if (currentMode == DrawMode.Select)
            {
                selectedBox = GetBoundingBoxAt(e.Location);

                if (selectedBox != null)
                {
                    isDragging = true;
                    dragOffset = new System.Drawing.Point(e.X - selectedBox.Rectangle.X, e.Y - selectedBox.Rectangle.Y);
                    UpdateObjectInfo(selectedBox);
                }
            }
        }

        private void pictureBoxVideo_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing && drawingBox != null)
            {
                int width = e.X - drawStartPoint.X;
                int height = e.Y - drawStartPoint.Y;

                drawingBox.Rectangle = new Rectangle(
                    Math.Min(drawStartPoint.X, e.X),
                    Math.Min(drawStartPoint.Y, e.Y),
                    Math.Abs(width),
                    Math.Abs(height)
                );

                pictureBoxVideo.Invalidate();
            }
            else if (isDragging && selectedBox != null)
            {
                selectedBox.Rectangle = new Rectangle(
                    e.X - dragOffset.X,
                    e.Y - dragOffset.Y,
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

        private void pictureBoxVideo_Paint(object sender, PaintEventArgs e)
        {
            if (pictureBoxVideo.Image == null)
                return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            foreach (var box in boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex))
            {
                Color boxColor = GetColorForLabel(box.Label);
                using (Pen pen = new Pen(boxColor, 3))
                {
                    if (box == selectedBox)
                        pen.Width = 5;
                    g.DrawRectangle(pen, box.Rectangle);
                }

                string labelText = $"{box.Label}_{box.PersonId:D2}";
                using (Font font = new Font("Segoe UI", 10F, FontStyle.Bold))
                {
                    SizeF textSize = g.MeasureString(labelText, font);
                    Rectangle labelBg = new Rectangle(
                        box.Rectangle.X,
                        box.Rectangle.Y - (int)textSize.Height - 4,
                        (int)textSize.Width + 8,
                        (int)textSize.Height + 4
                    );

                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(200, boxColor)))
                        g.FillRectangle(bgBrush, labelBg);

                    using (SolidBrush textBrush = new SolidBrush(Color.White))
                        g.DrawString(labelText, font, textBrush, box.Rectangle.X + 4, box.Rectangle.Y - textSize.Height - 2);
                }
            }

            if (isDrawing && drawingBox != null)
            {
                Color boxColor = GetColorForLabel(drawingBox.Label);
                using (Pen pen = new Pen(boxColor, 3) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, drawingBox.Rectangle);
            }
        }

        private BoundingBox GetBoundingBoxAt(System.Drawing.Point location)
        {
            foreach (var box in boundingBoxes.Where(b => b.FrameIndex == currentFrameIndex).Reverse())
            {
                if (box.Rectangle.Contains(location))
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

        private void UpdateObjectInfo(BoundingBox box)
        {
            labelObjectLabel.Text = $"Label: {box.Label}_{box.PersonId:D2}";
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
                selectedBox = null;
                UpdateBoxCount();
                pictureBoxVideo.Invalidate();
                RefreshVideoListView();
            }
        }

        private void panelLabelPerson_Click(object sender, EventArgs e)
        {
            if (selectedBox != null)
            {
                ApplyLabelChange("person", 1, selectedBox.Label, selectedBox.PersonId, selectedBox.Rectangle);
            }
            else
            {
                MessageBox.Show("먼저 BBox를 선택해주세요.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void panelLabelVehicle_Click(object sender, EventArgs e)
        {
            if (selectedBox != null)
            {
                ApplyLabelChange("vehicle", 0, selectedBox.Label, selectedBox.PersonId, selectedBox.Rectangle);
            }
            else
            {
                MessageBox.Show("먼저 BBox를 선택해주세요.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void panelLabelEvent_Click(object sender, EventArgs e)
        {
            if (selectedBox != null)
            {
                ApplyLabelChange("event", 0, selectedBox.Label, selectedBox.PersonId, selectedBox.Rectangle);
            }
            else
            {
                MessageBox.Show("먼저 BBox를 선택해주세요.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ApplyLabelChange(string newLabel, int newPersonId, string oldLabel, int oldPersonId, Rectangle oldRect)
        {
            if (selectedBox != null)
            {
                selectedBox.Label = newLabel;
                selectedBox.PersonId = newPersonId;
                labelObjectLabel.Text = $"Label: {newLabel}_{newPersonId:D2}";

                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel,
                    OriginalPersonId = oldPersonId,
                    OriginalRectangle = oldRect
                });

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
                int oldPersonId = selectedBox.PersonId;
                Rectangle oldRect = selectedBox.Rectangle;

                selectedBox.Label = customLabel.Type;
                selectedBox.PersonId = customLabel.Type == "person" ? customLabel.Id : 0;

                labelObjectLabel.Text = $"Label: {customLabel.Name}";

                AddUndoAction(new UndoAction
                {
                    Type = UndoActionType.ModifyBox,
                    Box = CloneBoundingBox(selectedBox),
                    OriginalLabel = oldLabel,
                    OriginalPersonId = oldPersonId,
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
                        b.PersonId == action.Box.PersonId);

                    if (boxToModify != null)
                    {
                        boxToModify.Rectangle = action.OriginalRectangle;
                        boxToModify.Label = action.OriginalLabel;
                        boxToModify.PersonId = action.OriginalPersonId;
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
                        b.PersonId == action.OriginalPersonId);

                    if (boxToModify != null)
                    {
                        boxToModify.Rectangle = action.Box.Rectangle;
                        boxToModify.Label = action.Box.Label;
                        boxToModify.PersonId = action.Box.PersonId;
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
            pictureBoxVideo.Invalidate();
        }
        #endregion

        #region Tracking Algorithm
        private void PerformTrackingForWaypoint(WaypointMarker waypoint, bool useYolo = false)
        {
            try
            {
                var startBox = boundingBoxes.FirstOrDefault(b => b.FrameIndex == waypoint.EntryFrame);

                if (startBox == null)
                {
                    MessageBox.Show(
                        "Entry 프레임에 BBox를 찾을 수 없습니다.\n\n" +
                        "추적하려면:\n" +
                        "1. Entry 프레임으로 이동\n" +
                        "2. BBox 그리기\n" +
                        "3. 다시 Ctrl+T 시도",
                        "오류",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                List<BoundingBox> trackedBoxes = new List<BoundingBox>();

                if (useYolo && isYoloAvailable)
                {
                    // YOLO 추적 사용
                    trackedBoxes = trackingEngine.TrackObjects(
                        videoCapture,
                        startBox,
                        waypoint.EntryFrame,
                        waypoint.ExitFrame,
                        fps);
                }
                else 
                {
                    MessageBox.Show(
                        "YOLO 모델을 사용할 수 없습니다.\n",
                        "정보",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                // 추적된 박스 추가
                foreach (var box in trackedBoxes)
                {
                    boundingBoxes.Add(box);
                }

                if (trackedBoxes.Count > 0)
                {
                    AddUndoAction(new UndoAction { Type = UndoActionType.Tracking, TrackedBoxes = trackedBoxes });
                }

                UpdateBoxCount();
                MessageBox.Show(
                    $"추적 완료! {trackedBoxes.Count} 프레임에 BBox가 추가되었습니다.",
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

                    int personId = annotation.TrackId;
                    string label = "person";

                    if (categoryMap.ContainsKey(annotation.CategoryId))
                    {
                        string categoryName = categoryMap[annotation.CategoryId].Name;
                        if (categoryName.StartsWith("person"))
                            label = "person";
                        else if (categoryName.StartsWith("vehicle"))
                            label = "vehicle";
                        else if (categoryName.StartsWith("event"))
                            label = "event";
                    }

                    var box = new BoundingBox
                    {
                        FrameIndex = annotation.ImageId,
                        Rectangle = new Rectangle(annotation.Bbox[0], annotation.Bbox[1], annotation.Bbox[2], annotation.Bbox[3]),
                        Label = label,
                        PersonId = personId,
                        Action = "waypoint"
                    };

                    boundingBoxes.Add(box);

                    if (annotation.Id >= nextAnnotationId)
                        nextAnnotationId = annotation.Id + 1;
                }

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
                        int categoryId = box.PersonId;
                        if (!categories.ContainsKey(categoryId))
                        {
                            categories[categoryId] = new CategoryData
                            {
                                Id = categoryId,
                                Name = $"{box.Label}_{box.PersonId:D2}",
                                Supercategory = box.Label
                            };
                        }

                        // Entry와 Exit 프레임 계산
                        int entryFrame = boundingBoxes.Where(b => b.PersonId == box.PersonId).Min(b => b.FrameIndex);
                        int exitFrame = boundingBoxes.Where(b => b.PersonId == box.PersonId).Max(b => b.FrameIndex);

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
                            TrackId = box.PersonId,
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
            // 영상이 로드되지 않은 경우 키 이벤트 무시
            if (videoCapture == null || !videoCapture.IsOpened())
                return;

            // 특정 컨트롤이 포커스를 받고 있으면 해당 키만 무시
            if (e.KeyCode == Keys.Space && (btnPlay.Focused || btnRewind.Focused || btnForward.Focused))
                return;
            if ((e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) && 
                (btnEntry.Focused || btnExit.Focused))
                return;

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
                pictureBoxVideo.Invalidate();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Z && !e.Shift)
            {
                Undo();
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.Z)
            {
                Redo();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                Redo();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.E && !e.Control && !e.Alt)
            {
                btnEntry_Click(sender, e);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.X && !e.Control && !e.Alt)
            {
                btnExit_Click(sender, e);
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

            int oldPersonId = selectedBox.PersonId;
            string oldLabel = selectedBox.Label;
            Rectangle oldRect = selectedBox.Rectangle;

            selectedBox.PersonId = id;

            AddUndoAction(new UndoAction
            {
                Type = UndoActionType.ModifyBox,
                Box = CloneBoundingBox(selectedBox),
                OriginalPersonId = oldPersonId,
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

        #region Helper Methods
        private BoundingBox CloneBoundingBox(BoundingBox box)
        {
            return new BoundingBox
            {
                FrameIndex = box.FrameIndex,
                Rectangle = new Rectangle(box.Rectangle.Location, box.Rectangle.Size),
                Label = box.Label,
                PersonId = box.PersonId,
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