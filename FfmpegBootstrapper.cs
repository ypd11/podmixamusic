using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsShuffle.Classes
{
    internal static class FfmpegBootstrapper
    {
        private const string DownloadUrl = "https://archive.org/download/ffmpeg_202605/ffmpeg.zip";
        private static readonly string[] RequiredFiles = { "ffmpeg.exe", "ffprobe.exe" };
        private static readonly SemaphoreSlim InstallLock = new SemaphoreSlim(1, 1);
        private static int _backgroundStarted;

        public static string InstallFolder => AppDomain.CurrentDomain.BaseDirectory;
        public static string FfmpegPath => Path.Combine(InstallFolder, "ffmpeg.exe");
        public static string FfprobePath => Path.Combine(InstallFolder, "ffprobe.exe");

        public static bool IsAvailable()
        {
            return RequiredFiles.All(file => File.Exists(Path.Combine(InstallFolder, file)));
        }

        public static void EnsureAvailableInBackground()
        {
            if (Interlocked.Exchange(ref _backgroundStarted, 1) == 1) return;

            Task.Run(async () =>
            {
                try
                {
                    await EnsureAvailableAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("FFmpeg background install failed: " + ex.Message);
                }
            });
        }

        public static async Task EnsureAvailableAsync()
        {
            if (IsAvailable()) return;

            await InstallLock.WaitAsync();
            try
            {
                if (IsAvailable()) return;

                string tempZipPath = Path.Combine(Path.GetTempPath(), "podmix_ffmpeg_" + Guid.NewGuid().ToString("N") + ".zip");

                try
                {
                    await DownloadZipAsync(tempZipPath);
                    ExtractRequiredFiles(tempZipPath);
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FFmpeg install failed: " + ex.Message);
            }
            finally
            {
                InstallLock.Release();
            }
        }

        private static async Task DownloadZipAsync(string tempZipPath)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            using (var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                using (var input = await response.Content.ReadAsStreamAsync())
                using (var output = File.Create(tempZipPath))
                {
                    await input.CopyToAsync(output);
                }
            }
        }

        private static void ExtractRequiredFiles(string zipPath)
        {
            Directory.CreateDirectory(InstallFolder);

            using (var stream = File.OpenRead(zipPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    string fileName = Path.GetFileName(entry.FullName);
                    if (!RequiredFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase)) continue;

                    string destinationPath = Path.Combine(InstallFolder, fileName);
                    using (var input = entry.Open())
                    using (var output = File.Create(destinationPath))
                    {
                        input.CopyTo(output);
                    }
                }
            }
        }
    }
}
