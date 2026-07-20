using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace WinFormsApp1
{
    internal static class YoloTempFileHelper
    {
        private static readonly TimeSpan StaleFileAge = TimeSpan.FromHours(1);

        private static string RootDirectory => Path.Combine(Path.GetTempPath(), "ETRILabelingTool");

        public static string CreateFramePath(string prefix, int? frameIndex = null)
        {
            Directory.CreateDirectory(RootDirectory);
            string framePart = frameIndex.HasValue ? $"_{frameIndex.Value}" : string.Empty;
            return Path.Combine(RootDirectory, $"{prefix}{framePart}_{Guid.NewGuid():N}.jpg");
        }

        public static void CleanupStaleFiles()
        {
            try
            {
                if (!Directory.Exists(RootDirectory))
                {
                    return;
                }

                DateTime cutoff = DateTime.UtcNow - StaleFileAge;
                foreach (string path in Directory.EnumerateFiles(RootDirectory, "yolo_*.jpg"))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(path) < cutoff)
                        {
                            TryDelete(path);
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static bool TryDelete(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        return true;
                    }

                    File.Delete(path);
                    return !File.Exists(path);
                }
                catch (IOException)
                {
                    Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(50);
                }
            }

            return false;
        }
    }
}
