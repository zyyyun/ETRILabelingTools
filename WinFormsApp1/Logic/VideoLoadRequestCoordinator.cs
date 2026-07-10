using System;
using System.Threading;

namespace WinFormsApp1
{
    public readonly record struct VideoLoadRequest(int RequestId, CancellationToken CancellationToken);

    public sealed class VideoLoadRequestCoordinator : IDisposable
    {
        private readonly object syncRoot = new();
        private CancellationTokenSource currentCancellationTokenSource = new();
        private int currentRequestId;

        public VideoLoadRequest BeginNewRequest()
        {
            lock (syncRoot)
            {
                currentCancellationTokenSource.Cancel();
                currentCancellationTokenSource.Dispose();
                currentCancellationTokenSource = new CancellationTokenSource();
                currentRequestId++;

                return new VideoLoadRequest(currentRequestId, currentCancellationTokenSource.Token);
            }
        }

        public bool IsCurrent(int requestId)
        {
            lock (syncRoot)
            {
                return requestId == currentRequestId;
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                currentCancellationTokenSource.Cancel();
                currentCancellationTokenSource.Dispose();
                currentCancellationTokenSource = new CancellationTokenSource();
            }
        }
    }
}
