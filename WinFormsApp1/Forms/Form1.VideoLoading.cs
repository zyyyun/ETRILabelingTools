using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace WinFormsApp1
{
    public partial class Form1
    {
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

                // ? 폴더 선택 시: 데이터 백업 → 초기화 → 로드 (실패 시 복원)
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
                    // ? 로드 실패 시 이전 데이터 복원
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
            var loadRequest = BeginVideoLoadRequest();

            try
            {
                await LoadVideo(filePath, loadRequest.CancellationToken);
                ThrowIfVideoLoadSuperseded(loadRequest);

                string videoDir = Path.GetDirectoryName(filePath);
                string videoName = Path.GetFileNameWithoutExtension(filePath);
                string externalSrtPath = Path.Combine(videoDir, $"{videoName}.srt");

                if (File.Exists(externalSrtPath))
                {
                    currentSrtFile = externalSrtPath;
                    await LoadSrtFile(externalSrtPath, loadRequest.CancellationToken);
                    ThrowIfVideoLoadSuperseded(loadRequest);
                    return;
                }

                await ExtractSrtFromVideo(filePath, loadRequest.CancellationToken);
                ThrowIfVideoLoadSuperseded(loadRequest);
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[Video Load] Cancelled: {filePath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"비디오 로드 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                CompleteVideoLoadRequest(loadRequest.RequestId);
            }
        }
        private async Task LoadVideo(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                // ? 비디오 로드 시 YOLO 탐지 캐시 초기화 및 탐지 중지
                try
                {
                    System.Diagnostics.Debug.WriteLine("[비디오 로드] YOLO 탐지 캐시 초기화 시작");
                    StopYoloDetection();

                    // 디바운스 타이머도 확실히 정리
                    detectionDebounceTimer?.Dispose();
                    detectionDebounceTimer = null;

                    lock (yoloDetectionCacheLock)
                    {
                        int cacheCount = yoloDetectionCache.Count;
                        yoloDetectionCache.Clear();
                        System.Diagnostics.Debug.WriteLine($"[비디오 로드] YOLO 탐지 캐시 초기화 완료 (기존 캐시: {cacheCount}개)");
                    }
                    showYoloDetections = false;
                    if (btnToggleYoloDetections != null)
                    {
                        btnToggleYoloDetections.Text = "YOLO 탐지";
                        btnToggleYoloDetections.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
                    }
                }
                catch (Exception yoloEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[비디오 로드] YOLO 탐지 초기화 오류: {yoloEx.Message}");
                    // 오류가 있어도 비디오 로드는 계속 진행
                }

                if (videoCapture != null)
                {
                    videoCapture.Release();
                    videoCapture.Dispose();
                }

                try
                {
                    videoCapture = new VideoCapture(filePath);
                }
                catch (TypeInitializationException tiex)
                {
                    string errorMsg = "OpenCvSharp 네이티브 DLL 초기화 실패:\n\n" +
                                    $"{tiex.Message}\n\n" +
                                    "가능한 원인:\n" +
                                    "1. Visual C++ 재배포 가능 패키지가 설치되지 않았습니다.\n" +
                                    "   (Microsoft Visual C++ 2015-2022 Redistributable 설치 필요)\n" +
                                    "2. OpenCvSharpExtern.dll 또는 관련 DLL이 누락되었습니다.\n" +
                                    "3. 플랫폼 아키텍처 불일치 (x64 필요)";
                    if (tiex.InnerException != null)
                    {
                        errorMsg += $"\n\n내부 예외: {tiex.InnerException.Message}";
                    }
                    MessageBox.Show(errorMsg, "OpenCvSharp 초기화 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                catch (DllNotFoundException dllEx)
                {
                    string errorMsg = "필수 DLL을 찾을 수 없습니다:\n\n" +
                                    $"{dllEx.Message}\n\n" +
                                    "OpenCvSharpExtern.dll 또는 opencv_videoio_ffmpeg4110_64.dll이\n" +
                                    "실행 파일과 같은 폴더에 있는지 확인하세요.";
                    MessageBox.Show(errorMsg, "DLL 누락 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

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
                await LoadLabelingData(filePath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading video: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadFrame(int frameIndex)
        {
            try
            {
                // ? YOLO 추적/탐지 중에는 프레임 이동 차단 (중요!)
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine($"[프레임 이동 차단] YOLO 추적/탐지 중이므로 프레임 {frameIndex}로 이동 불가");
                    return;
                }

                // ? 프레임이 실제로 이동하는지 확인
                bool frameChanged = (currentFrameIndex != frameIndex);

                // ? YOLO 탐지 토글이 ON이고 프레임이 이동하면 자동으로 OFF로 변경
                if (showYoloDetections && frameChanged)
                {
                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지 토글] 프레임 이동 감지 ({currentFrameIndex} -> {frameIndex}), 탐지 토글 자동 OFF");
                    showYoloDetections = false;
                    StopYoloDetection();

                    // 디바운스 타이머도 정리
                    detectionDebounceTimer?.Dispose();
                    detectionDebounceTimer = null;

                    // 캐시 정리
                    lock (yoloDetectionCacheLock)
                    {
                        yoloDetectionCache.Clear();
                    }

                    // UI 업데이트
                    if (btnToggleYoloDetections != null)
                    {
                        btnToggleYoloDetections.Text = "YOLO 탐지";
                        btnToggleYoloDetections.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
                    }
                    pictureBoxVideo?.Invalidate();
                }

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

            // ? YOLO 탐지 토글이 ON일 경우 프레임 이동 시 자동으로 탐지 수행 (300ms 디바운싱)
            // (프레임이 이동하지 않았거나 이미 OFF로 변경되었을 수 있으므로 재확인)
            if (showYoloDetections && isYoloAvailable)
            {
                try
                {
                    lock (yoloDetectionCacheLock)
                    {
                        // 현재 프레임의 탐지 결과가 있으면 UI만 업데이트
                        if (yoloDetectionCache.ContainsKey(frameIndex))
                        {
                            pictureBoxVideo?.Invalidate();
                        }
                        else
                        {
                            // 탐지 결과가 없으면 디바운스 타이머로 지연 탐지
                            // ? 기존 타이머 안전하게 취소
                            System.Threading.Timer oldTimer = detectionDebounceTimer;
                            detectionDebounceTimer = null;
                            if (oldTimer != null)
                            {
                                try
                                {
                                    oldTimer.Change(Timeout.Infinite, Timeout.Infinite); // 타이머 중지
                                    oldTimer.Dispose();
                                }
                                catch (Exception timerDisposeEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 기존 타이머 해제 오류: {timerDisposeEx.Message}");
                                }
                            }

                            // 새 타이머 시작 (디바운싱 - 300ms)
                            int targetFrame = frameIndex; // 프레임 인덱스 캡처
                            pendingDetectionFrame = targetFrame;

                            // ? 타이머 콜백에서는 Task.Run으로 비동기 실행 (async void 방지)
                            detectionDebounceTimer = new System.Threading.Timer((state) =>
                            {
                                int checkFrame = targetFrame;

                                // ? Task.Run으로 비동기 실행 (예외 처리 가능)
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        // ? 빠른 체크 (락 없이)
                                        if (checkFrame != currentFrameIndex || checkFrame != pendingDetectionFrame)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 디바운스 타이머: 프레임 {checkFrame} 탐지 취소 (프레임 변경됨)");
                                            return;
                                        }

                                        // ? 캐시 확인 (락 사용)
                                        bool needsDetection = false;
                                        lock (yoloDetectionCacheLock)
                                        {
                                            needsDetection = !yoloDetectionCache.ContainsKey(checkFrame);
                                        }

                                        if (!needsDetection)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 디바운스 타이머: 프레임 {checkFrame}은 이미 캐시에 있음");
                                            return;
                                        }

                                        // ? 비동기로 탐지 시작
                                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 디바운스(300ms) 후 탐지: 프레임 {checkFrame}");
                                        pendingDetectionFrame = -1; // 대기 프레임 초기화
                                        await DetectCurrentFrameOnlyAsync(); // ? 비동기 버전 사용
                                    }
                                    catch (Exception timerEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 디바운스 타이머 오류: {timerEx.Message}\n{timerEx.StackTrace}");
                                    }
                                });
                            }, null, DETECTION_DEBOUNCE_MS, Timeout.Infinite);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YOLO 탐지] 프레임 이동 시 탐지 오류: {ex.Message}\n{ex.StackTrace}");
                    // 오류 시 계속 진행
                }
            }

            // ? 프레임 전환 시 선택 박스 재바인딩 또는 해제
            if (selectedBox != null && selectedBox.FrameIndex != frameIndex)
            {
                var selLabel = selectedBox.Label;
                int selId = GetBoxId(selectedBox);
                var rebound = boundingBoxes.FirstOrDefault(b => b.FrameIndex == frameIndex && b.Label == selLabel && GetBoxId(b) == selId && !b.IsDeleted);
                if (rebound != null)
                {
                    selectedBox = rebound;
                    HighlightSelectedBoxInSidebar();
                }
                else
                {
                    selectedBox = null;
                    ClearSidebarHighlights();
                }
            }
            UpdateTimeLabels();

            // Waypoint entry 프레임에서만 bbox 리스트 업데이트 (리소스 최적화)
            if (ShouldUpdateBboxList(frameIndex))
            {
                UpdateBboxListDisplay();
            }

            pictureBoxVideo.Invalidate();

            // ? 속성 창 업데이트 (토글이 켜져 있을 경우)
            UpdateAttributeWindows();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[프레임 로드 오류] {ex.Message}\n{ex.StackTrace}");
                // 오류 발생 시 현재 프레임 인덱스는 유지
            }
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

            UpdateSubtitleTimestampDisplay(subtitleText);

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
                // ? 비디오 전환 시: 데이터 백업 → 초기화 → 로드 (실패 시 복원)
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
                    // ? 로드 실패 시 이전 데이터 복원
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

    }
}



