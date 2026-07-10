using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace WinFormsApp1
{
    public partial class Form1
    {
        private readonly VideoLoadRequestCoordinator videoLoadRequestCoordinator = new();

        private VideoLoadRequest BeginVideoLoadRequest()
        {
            StopPlaybackForVideoLoad();
            ResetSubtitleStateBeforeVideoLoad();

            var request = videoLoadRequestCoordinator.BeginNewRequest();
            SetVideoLoadUiState(true);
            return request;
        }

        private void CompleteVideoLoadRequest(int requestId)
        {
            if (!videoLoadRequestCoordinator.IsCurrent(requestId))
            {
                return;
            }

            SetVideoLoadUiState(false);
        }

        private bool IsVideoLoadRequestCurrent(int requestId)
        {
            return videoLoadRequestCoordinator.IsCurrent(requestId);
        }

        private void ThrowIfVideoLoadSuperseded(VideoLoadRequest request)
        {
            request.CancellationToken.ThrowIfCancellationRequested();

            if (!IsVideoLoadRequestCurrent(request.RequestId))
            {
                throw new OperationCanceledException(request.CancellationToken);
            }
        }

        private void StopPlaybackForVideoLoad()
        {
            if (!isPlaying)
            {
                return;
            }

            isPlaying = false;

            if (btnPlay != null)
            {
                btnPlay.Text = "¢º";
            }

            timerPlayback?.Stop();
        }

        private void ResetSubtitleStateBeforeVideoLoad()
        {
            currentSrtFile = string.Empty;
            subtitleEntries.Clear();
        }

        private void SetVideoLoadUiState(bool loading)
        {
            UseWaitCursor = loading;

            if (btnPlay != null)
            {
                btnPlay.Enabled = !loading;
            }

            if (btnSelectFolder != null)
            {
                btnSelectFolder.Enabled = !loading;
            }

            if (btnSelectFolderPath != null)
            {
                btnSelectFolderPath.Enabled = !loading;
            }

            if (btnVideoList != null)
            {
                btnVideoList.Enabled = !loading;
            }

            Cursor.Current = loading ? Cursors.WaitCursor : Cursors.Default;
            Debug.WriteLine($"[Video Load] UI state changed: loading={loading}");
        }
    }
}


