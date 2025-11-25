using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface IUpdateService
    {
        Task<UpdateInfo> CheckForUpdatesAsync();
        Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, IProgress<int> progress = null);
        string CurrentVersion { get; }
    }

    public class UpdateInfo
    {
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public DateTime PublishedAt { get; set; }
        public bool IsNewerVersion { get; set; }
    }

    public class UpdateService : IUpdateService
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/Juricze/Sklad_2/releases/latest";
        private readonly HttpClient _httpClient;

        public string CurrentVersion { get; }

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Sklad_2-UpdateChecker");

            // Get current version from assembly
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            CurrentVersion = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                Debug.WriteLine($"UpdateService: Checking for updates. Current version: {CurrentVersion}");

                var response = await _httpClient.GetStringAsync(GITHUB_API_URL);
                var json = JsonDocument.Parse(response);
                var root = json.RootElement;

                var latestVersion = root.GetProperty("tag_name").GetString();
                var releaseNotes = root.GetProperty("body").GetString() ?? "";
                var publishedAt = root.GetProperty("published_at").GetDateTime();

                // Find the exe download URL
                string downloadUrl = null;
                var assets = root.GetProperty("assets");
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    if (name != null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }

                var isNewer = IsNewerVersion(latestVersion, CurrentVersion);

                Debug.WriteLine($"UpdateService: Latest version: {latestVersion}, Is newer: {isNewer}");

                return new UpdateInfo
                {
                    Version = latestVersion,
                    DownloadUrl = downloadUrl,
                    ReleaseNotes = releaseNotes,
                    PublishedAt = publishedAt,
                    IsNewerVersion = isNewer
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService: Error checking for updates: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, IProgress<int> progress = null)
        {
            if (updateInfo?.DownloadUrl == null)
                return false;

            try
            {
                Debug.WriteLine($"UpdateService: Downloading update from {updateInfo.DownloadUrl}");

                // Download to temp folder
                var tempPath = Path.Combine(Path.GetTempPath(), "Sklad_2_Update");
                Directory.CreateDirectory(tempPath);
                var downloadPath = Path.Combine(tempPath, "Sklad_2_new.exe");

                using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                var downloadedBytes = 0L;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0 && progress != null)
                    {
                        var percentComplete = (int)((downloadedBytes * 100) / totalBytes);
                        progress.Report(percentComplete);
                    }
                }

                Debug.WriteLine($"UpdateService: Download complete. Starting installer...");

                // Create a batch script to replace the exe after the app closes
                var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (currentExePath == null)
                    return false;

                var batchPath = Path.Combine(tempPath, "update.bat");
                var batchContent = $@"@echo off
echo Cekam na ukonceni aplikace...
timeout /t 2 /nobreak > nul
echo Instaluji aktualizaci...
copy /Y ""{downloadPath}"" ""{currentExePath}""
echo Spoustim novou verzi...
start """" ""{currentExePath}""
del ""{downloadPath}""
del ""%~f0""
";
                await File.WriteAllTextAsync(batchPath, batchContent, System.Text.Encoding.GetEncoding(852));

                // Start the batch script
                Process.Start(new ProcessStartInfo
                {
                    FileName = batchPath,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService: Error downloading update: {ex.Message}");
                return false;
            }
        }

        private bool IsNewerVersion(string latest, string current)
        {
            try
            {
                // Remove 'v' prefix if present
                latest = latest?.TrimStart('v') ?? "0.0.0";
                current = current?.TrimStart('v') ?? "0.0.0";

                var latestParts = latest.Split('.');
                var currentParts = current.Split('.');

                for (int i = 0; i < Math.Max(latestParts.Length, currentParts.Length); i++)
                {
                    var latestNum = i < latestParts.Length && int.TryParse(latestParts[i], out var ln) ? ln : 0;
                    var currentNum = i < currentParts.Length && int.TryParse(currentParts[i], out var cn) ? cn : 0;

                    if (latestNum > currentNum)
                        return true;
                    if (latestNum < currentNum)
                        return false;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
