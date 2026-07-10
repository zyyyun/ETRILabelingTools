using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinFormsApp1
{
    public partial class Form1
    {
        #region Video Playback
        private void btnPlay_Click(object sender, EventArgs e)
        {
            try
            {
                // ✅ YOLO 추적/탐지 중에는 재생 버튼 차단
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine("[재생 버튼 차단] YOLO 추적/탐지 중이므로 재생 불가");
                    MessageBox.Show(
                        "YOLO 추적 또는 탐지가 진행 중입니다.\n작업이 완료될 때까지 기다려주세요.",
                        "작업 중",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                if (videoCapture == null || !videoCapture.IsOpened())
                {
                    MessageBox.Show("비디오 파일이 로드되지 않았습니다.\n먼저 파일을 선택해주세요.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

            isPlaying = !isPlaying;

            if (isPlaying)
            {
                // ✅ 재생 시 YOLO 탐지 토글 자동으로 꺼기
                if (showYoloDetections)
                {
                    System.Diagnostics.Debug.WriteLine("[재생 버튼] 재생 시작 시 YOLO 탐지 토글 자동 해제");
                    showYoloDetections = false;
                    StopYoloDetection();
                    lock (yoloDetectionCacheLock)
                    {
                        yoloDetectionCache.Clear();
                    }
                    if (btnToggleYoloDetections != null)
                    {
                        btnToggleYoloDetections.Text = "YOLO 탐지";
                        btnToggleYoloDetections.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
                    }
                    pictureBoxVideo?.Invalidate();
                }
                
                // ✅ 재생 시 속성값 조회 토글 자동으로 꺼기
                if (isAttributeViewEnabled)
                {
                    System.Diagnostics.Debug.WriteLine("[재생 버튼] 재생 시작 시 속성값 조회 토글 자동 해제");
                    isAttributeViewEnabled = false;
                    
                    // 모든 속성 창 닫기
                    foreach (var window in attributeWindows.Values.ToList())
                    {
                        window.Close();
                    }
                    attributeWindows.Clear();
                    
                    // 버튼 UI 업데이트
                    if (btnToggleAttributeView != null)
                    {
                        btnToggleAttributeView.Text = "속성값 조회";
                        btnToggleAttributeView.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
                    }
                }
                
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[재생 버튼 오류] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"재생 중 오류 발생:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                // 상태 복원
                isPlaying = false;
                if (btnPlay != null)
                {
                    btnPlay.Text = "▶";
                }
                timerPlayback?.Stop();
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
            SeekBySeconds(-5);
        }

        private void btnForward_Click(object sender, EventArgs e)
        {
            SeekBySeconds(5);
        }

        private void SeekBySeconds(int seconds)
        {
            try
            {
                if (IsYoloOperationInProgress())
                {
                    System.Diagnostics.Debug.WriteLine($"[{Math.Abs(seconds)}s seek blocked] YOLO tracking/detection is in progress.");
                    MessageBox.Show(
                        "YOLO tracking or detection is in progress.\nPlease wait until the current operation finishes.",
                        "Busy",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                int framesToMove = (int)(fps * Math.Abs(seconds));
                int newFrame = seconds < 0
                    ? Math.Max(0, currentFrameIndex - framesToMove)
                    : Math.Min(totalFrames - 1, currentFrameIndex + framesToMove);
                LoadFrame(newFrame);
            }
            catch (Exception ex)
            {
                string direction = seconds < 0 ? "backward" : "forward";
                System.Diagnostics.Debug.WriteLine($"[{Math.Abs(seconds)}s seek {direction} error] {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"An error occurred while moving frames:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void IncreasePlaybackSpeed()
        {
            if (playbackSpeed < 1.0) playbackSpeed = 1.0;
            else if (playbackSpeed < 4.0) playbackSpeed = 4.0;
            else if (playbackSpeed < 8.0) playbackSpeed = 8.0;
            else if (playbackSpeed < 16.0) playbackSpeed = 16.0;

            if (isPlaying)
                lastFrameTime = DateTime.Now.Ticks / 10000;
            UpdateTimeLabels();
        }

        private void DecreasePlaybackSpeed()
        {
            if (playbackSpeed > 8.0) playbackSpeed = 8.0;
            else if (playbackSpeed > 4.0) playbackSpeed = 4.0;
            else if (playbackSpeed > 1.0) playbackSpeed = 1.0;
            else playbackSpeed = 0.5;

            if (isPlaying)
                lastFrameTime = DateTime.Now.Ticks / 10000;
            UpdateTimeLabels();
        }

        private void ResetPlaybackSpeed()
        {
            playbackSpeed = 1.0;
            if (isPlaying)
                lastFrameTime = DateTime.Now.Ticks / 10000;
            UpdateTimeLabels();
            MessageBox.Show("Playback speed was reset to 1.0x.", "Speed Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        #endregion

    }
}
