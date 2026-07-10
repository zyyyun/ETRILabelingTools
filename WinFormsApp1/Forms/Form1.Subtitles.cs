using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FFMpegCore;

namespace WinFormsApp1
{
    public partial class Form1
    {
        #region SRT Subtitle Extraction
        private async Task<bool> ExtractSrtFromVideo(string videoPath, CancellationToken cancellationToken = default)
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
                        .WithCustomArgument("-loglevel error")  // 경고 메시지 숨기기
                        .WithCustomArgument("-map 0:s:0")  // 첫 번째 자막 스트림 선택
                        .WithCustomArgument("-c:s srt")    // SRT 형식으로 출력
                    )
                    .ProcessAsynchronously();

                var result = await ffTask;
                cancellationToken.ThrowIfCancellationRequested();

                if (result && File.Exists(srtPath))
                {
                    currentSrtFile = srtPath;
                    await LoadSrtFile(srtPath, cancellationToken);
                    return true;
                }
                else
                {
                    // 자막 스트림이 없는 경우 (정상 상황)
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 자막이 없거나 추출 실패 시 조용히 실패
                return false;
            }
        }

        private async Task LoadSrtFile(string srtPath, CancellationToken cancellationToken = default)
        {
            try
            {
                subtitleEntries.Clear();
                string[] lines = await File.ReadAllLinesAsync(srtPath, cancellationToken);

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
            catch (OperationCanceledException)
            {
                throw;
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

        private void UpdateSubtitleTimestampDisplay(string subtitleText)
        {
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
        }

        #endregion

    }
}



