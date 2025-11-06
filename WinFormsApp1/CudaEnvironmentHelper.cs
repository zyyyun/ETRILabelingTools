using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WinFormsApp1
{
    internal static class CudaEnvironmentHelper
    {
        private static readonly string[] RequiredCudaDlls =
        {
            "cublasLt64_12.dll",
            "cublas64_12.dll",
            "cudart64_12.dll",
            "cudnn64_9.dll",
            "cufft64_11.dll"
        };

        private static string[] _probedDirectories = Array.Empty<string>();

        public static void EnsureCudaPathOnProcess()
        {
            var candidateDirectories = GatherCandidateDirectories();

            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var pathEntries = currentPath.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            var normalizedEntries = new HashSet<string>(pathEntries.Select(NormalizePath), StringComparer.OrdinalIgnoreCase);

            var directoriesToAppend = new List<string>();
            foreach (var dir in candidateDirectories)
            {
                if (Directory.Exists(dir) && normalizedEntries.Add(NormalizePath(dir)))
                {
                    directoriesToAppend.Add(dir);
                }
            }

            if (directoriesToAppend.Count > 0)
            {
                // 새 경로는 앞으로 추가하여 우선순위를 높인다.
                directoriesToAppend.AddRange(pathEntries);
                var newPath = string.Join(';', directoriesToAppend);
                Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Process);
            }

            _probedDirectories = candidateDirectories
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static IReadOnlyList<string> GetMissingCudaDependencies()
        {
            var searchDirectories = _probedDirectories.Length > 0
                ? _probedDirectories
                : GatherCandidateDirectories().Where(Directory.Exists).ToArray();

            var missing = new List<string>();
            foreach (var dll in RequiredCudaDlls)
            {
                var exists = searchDirectories.Any(dir => File.Exists(Path.Combine(dir, dll)))
                             || File.Exists(Path.Combine(AppContext.BaseDirectory, dll));

                if (!exists)
                {
                    missing.Add(dll);
                }
            }

            return missing;
        }

        private static IEnumerable<string> GatherCandidateDirectories()
        {
            var directories = new List<string>
            {
                AppContext.BaseDirectory
            };

            var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
            if (!string.IsNullOrWhiteSpace(cudaPath))
            {
                directories.Add(Path.Combine(cudaPath, "bin"));
                directories.Add(Path.Combine(cudaPath, "libnvvp"));
            }

            var cudaRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "NVIDIA GPU Computing Toolkit",
                "CUDA");
            if (Directory.Exists(cudaRoot))
            {
                foreach (var versionDir in Directory.GetDirectories(cudaRoot, "v12.*", SearchOption.TopDirectoryOnly))
                {
                    directories.Add(Path.Combine(versionDir, "bin"));
                    directories.Add(Path.Combine(versionDir, "libnvvp"));
                }
            }

            return directories;
        }

        private static string NormalizePath(string path)
        {
            return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}


