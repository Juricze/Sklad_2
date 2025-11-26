using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

            Debug.WriteLine($"[UpdateService] Initialized. Current version: {CurrentVersion}");
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                Debug.WriteLine($"[UpdateService] Starting update check...");
                Debug.WriteLine($"[UpdateService] Current version: {CurrentVersion}");
                Debug.WriteLine($"[UpdateService] Calling GitHub API: {GITHUB_API_URL}");

                var response = await _httpClient.GetStringAsync(GITHUB_API_URL);
                Debug.WriteLine($"[UpdateService] GitHub API response received ({response.Length} chars)");

                var json = JsonDocument.Parse(response);
                var root = json.RootElement;

                var latestVersion = root.GetProperty("tag_name").GetString();
                var releaseNotes = root.GetProperty("body").GetString() ?? "";
                var publishedAt = root.GetProperty("published_at").GetDateTime();

                Debug.WriteLine($"[UpdateService] Latest GitHub version: {latestVersion}");
                Debug.WriteLine($"[UpdateService] Published at: {publishedAt:yyyy-MM-dd HH:mm:ss}");

                // Find the ZIP download URL (multi-file deployment)
                string downloadUrl = null;
                var assets = root.GetProperty("assets");
                Debug.WriteLine($"[UpdateService] Scanning {assets.GetArrayLength()} release assets...");

                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    Debug.WriteLine($"[UpdateService]   - Asset: {name}");

                    if (name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        var sizeBytes = asset.GetProperty("size").GetInt64();
                        Debug.WriteLine($"[UpdateService] ✓ Found ZIP asset: {name} ({sizeBytes / 1024 / 1024} MB)");
                        Debug.WriteLine($"[UpdateService] Download URL: {downloadUrl}");
                        break;
                    }
                }

                if (downloadUrl == null)
                {
                    Debug.WriteLine($"[UpdateService] ⚠ WARNING: No ZIP file found in release assets!");
                }

                var isNewer = IsNewerVersion(latestVersion, CurrentVersion);
                Debug.WriteLine($"[UpdateService] Version comparison: {CurrentVersion} -> {latestVersion} = {(isNewer ? "NEWER" : "SAME/OLDER")}");

                return new UpdateInfo
                {
                    Version = latestVersion,
                    DownloadUrl = downloadUrl,
                    ReleaseNotes = releaseNotes,
                    PublishedAt = publishedAt,
                    IsNewerVersion = isNewer
                };
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[UpdateService] ❌ HTTP ERROR: {ex.Message}");
                Debug.WriteLine($"[UpdateService] Stack trace: {ex.StackTrace}");
                return null;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[UpdateService] ❌ JSON PARSE ERROR: {ex.Message}");
                Debug.WriteLine($"[UpdateService] Stack trace: {ex.StackTrace}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] ❌ UNEXPECTED ERROR: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[UpdateService] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, IProgress<int> progress = null)
        {
            if (updateInfo?.DownloadUrl == null)
            {
                Debug.WriteLine($"[UpdateService] ❌ ERROR: No download URL provided!");
                return false;
            }

            string tempUpdateFolder = null;
            string zipPath = null;
            string extractPath = null;

            try
            {
                Debug.WriteLine($"[UpdateService] ========== STARTING UPDATE PROCESS ==========");
                Debug.WriteLine($"[UpdateService] Version: {updateInfo.Version}");
                Debug.WriteLine($"[UpdateService] Download URL: {updateInfo.DownloadUrl}");

                // Step 0: Cleanup old update folders
                Debug.WriteLine($"[UpdateService] Step 0: Cleaning up old update folders...");
                try
                {
                    var tempPath = Path.GetTempPath();
                    var oldFolders = Directory.GetDirectories(tempPath, "Sklad_2_Update_*");
                    var deletedCount = 0;
                    var deletedSize = 0L;

                    foreach (var folder in oldFolders)
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(folder);
                            // Delete folders older than 1 hour (safe window)
                            if (DateTime.Now - dirInfo.CreationTime > TimeSpan.FromHours(1))
                            {
                                // Calculate folder size before deletion
                                var folderSize = dirInfo.GetFiles("*.*", SearchOption.AllDirectories).Sum(f => f.Length);
                                deletedSize += folderSize;

                                Directory.Delete(folder, true);
                                deletedCount++;
                                Debug.WriteLine($"[UpdateService] ✓ Deleted old folder: {dirInfo.Name} ({folderSize / 1024 / 1024} MB)");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[UpdateService] ⚠ Failed to delete folder {Path.GetFileName(folder)}: {ex.Message}");
                        }
                    }

                    if (deletedCount > 0)
                    {
                        Debug.WriteLine($"[UpdateService] ✓ Cleanup complete: Deleted {deletedCount} folder(s), freed {deletedSize / 1024 / 1024} MB");
                    }
                    else
                    {
                        Debug.WriteLine($"[UpdateService] ✓ No old folders to clean");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[UpdateService] ⚠ Cleanup warning (non-critical): {ex.Message}");
                }

                // Step 1: Create temp folders
                Debug.WriteLine($"[UpdateService] Step 1: Creating temporary folders...");
                tempUpdateFolder = Path.Combine(Path.GetTempPath(), $"Sklad_2_Update_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempUpdateFolder);
                Debug.WriteLine($"[UpdateService] ✓ Temp folder created: {tempUpdateFolder}");

                zipPath = Path.Combine(tempUpdateFolder, "Sklad_2_Update.zip");
                extractPath = Path.Combine(tempUpdateFolder, "extracted");

                // Step 2: Download ZIP file
                Debug.WriteLine($"[UpdateService] Step 2: Downloading ZIP file...");
                Debug.WriteLine($"[UpdateService] Target file: {zipPath}");

                using (var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1;
                    var downloadedBytes = 0L;

                    Debug.WriteLine($"[UpdateService] Download size: {(totalBytes > 0 ? $"{totalBytes / 1024 / 1024} MB" : "Unknown")}");

                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

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

                            if (downloadedBytes % (1024 * 1024 * 10) < 8192) // Log every ~10MB
                            {
                                Debug.WriteLine($"[UpdateService] Downloaded: {downloadedBytes / 1024 / 1024} MB / {totalBytes / 1024 / 1024} MB ({percentComplete}%)");
                            }
                        }
                    }

                    Debug.WriteLine($"[UpdateService] ✓ Download complete: {downloadedBytes / 1024 / 1024} MB");
                }

                // Step 3: Verify ZIP file
                Debug.WriteLine($"[UpdateService] Step 3: Verifying ZIP file...");
                var zipFileInfo = new FileInfo(zipPath);
                if (!zipFileInfo.Exists)
                {
                    Debug.WriteLine($"[UpdateService] ❌ ERROR: ZIP file does not exist after download!");
                    return false;
                }
                Debug.WriteLine($"[UpdateService] ✓ ZIP file verified: {zipFileInfo.Length / 1024 / 1024} MB");

                // Step 4: Extract ZIP file
                Debug.WriteLine($"[UpdateService] Step 4: Extracting ZIP file...");
                Directory.CreateDirectory(extractPath);

                try
                {
                    ZipFile.ExtractToDirectory(zipPath, extractPath);
                    Debug.WriteLine($"[UpdateService] ✓ ZIP extracted successfully");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[UpdateService] ❌ EXTRACTION ERROR: {ex.Message}");
                    throw new Exception($"Failed to extract ZIP file: {ex.Message}", ex);
                }

                // Step 5: Verify extracted files
                Debug.WriteLine($"[UpdateService] Step 5: Verifying extracted files...");
                var extractedFiles = Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories);
                Debug.WriteLine($"[UpdateService] ✓ Extracted {extractedFiles.Length} files");

                bool foundExe = false;
                foreach (var file in extractedFiles)
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName == "Sklad_2.exe")
                    {
                        foundExe = true;
                        Debug.WriteLine($"[UpdateService] ✓ Found main executable: {fileName}");
                        break;
                    }
                }

                if (!foundExe)
                {
                    Debug.WriteLine($"[UpdateService] ❌ ERROR: Sklad_2.exe not found in extracted files!");
                    return false;
                }

                // Step 6: Get current exe location
                Debug.WriteLine($"[UpdateService] Step 6: Determining installation folder...");
                var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (currentExePath == null)
                {
                    Debug.WriteLine($"[UpdateService] ❌ ERROR: Cannot determine current exe path!");
                    return false;
                }

                var installFolder = Path.GetDirectoryName(currentExePath);
                Debug.WriteLine($"[UpdateService] ✓ Current exe: {currentExePath}");
                Debug.WriteLine($"[UpdateService] ✓ Installation folder: {installFolder}");

                // Step 7: Create PowerShell update script
                Debug.WriteLine($"[UpdateService] Step 7: Creating PowerShell update script...");
                var scriptPath = Path.Combine(tempUpdateFolder, "update.ps1");

                // PowerShell script with detailed logging and error handling
                var scriptContent = $@"
# Sklad_2 Update Script v1.0.4
# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

$ErrorActionPreference = ""Stop""
$LogFile = ""{Path.Combine(tempUpdateFolder, "update.log")}""

function Write-Log {{
    param([string]$Message)
    $timestamp = Get-Date -Format ""yyyy-MM-dd HH:mm:ss""
    $logMessage = ""[$timestamp] $Message""
    Write-Host $logMessage
    Add-Content -Path $LogFile -Value $logMessage
}}

try {{
    Write-Log ""========== SKLAD_2 UPDATE SCRIPT =========""
    Write-Log ""Version: {updateInfo.Version}""
    Write-Log ""Installation folder: {installFolder}""
    Write-Log ""Source folder: {extractPath}""
    Write-Log ""Current exe: {currentExePath}""
    Write-Log ""Log file: $LogFile""
    Write-Log """"

    # Step 1: Wait for application to close
    Write-Log ""Step 1: Waiting for application to close...""
    Start-Sleep -Seconds 2

    # Wait for Sklad_2 process to exit (max 10 seconds)
    $waitCount = 0
    while ((Get-Process -Name ""Sklad_2"" -ErrorAction SilentlyContinue) -and $waitCount -lt 10) {{
        Write-Log ""  Waiting for Sklad_2.exe to exit... ($waitCount/10)""
        Start-Sleep -Seconds 1
        $waitCount++
    }}

    if (Get-Process -Name ""Sklad_2"" -ErrorAction SilentlyContinue) {{
        Write-Log ""  WARNING: Sklad_2.exe still running, trying to kill...""
        Stop-Process -Name ""Sklad_2"" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
    }}
    Write-Log ""OK: Application closed""

    # Step 2: Create backup
    Write-Log ""Step 2: Creating backup...""
    $backupFolder = ""{Path.Combine(tempUpdateFolder, "backup")}""
    New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
    Copy-Item -Path ""{installFolder}\*"" -Destination $backupFolder -Recurse -Force -ErrorAction SilentlyContinue
    Write-Log ""OK: Backup created at: $backupFolder""

    # Step 3: Copy new files (excluding user data)
    Write-Log ""Step 3: Copying new files...""
    $sourceRoot = ""{extractPath}""
    if (-not $sourceRoot.EndsWith('\')) {{ $sourceRoot += '\' }}
    Write-Log ""  Source root: $sourceRoot""

    $sourceFiles = Get-ChildItem -Path ""{extractPath}"" -Recurse -File
    Write-Log ""  Found $($sourceFiles.Count) files to process""
    $copiedCount = 0
    $skippedCount = 0

    foreach ($file in $sourceFiles) {{
        try {{
            # Calculate relative path safely
            $relativePath = $file.FullName.Substring($sourceRoot.Length)
            $targetPath = Join-Path ""{installFolder}"" $relativePath

            # Skip user data folders
            if ($relativePath -like ""*AppData*"" -or $relativePath -like ""*settings.json*"" -or $relativePath -like ""*sklad.db*"") {{
                Write-Log ""  SKIP: $relativePath (user data)""
                $skippedCount++
                continue
            }}

            $targetDir = Split-Path $targetPath -Parent
            if (-not (Test-Path $targetDir)) {{
                New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
            }}

            Copy-Item -Path $file.FullName -Destination $targetPath -Force
            $copiedCount++

            # Log every 50 files
            if ($copiedCount % 50 -eq 0) {{
                Write-Log ""  Progress: $copiedCount files copied...""
            }}
        }}
        catch {{
            Write-Log ""  ERROR copying file: $($file.FullName) - $($_.Exception.Message)""
        }}
    }}

    Write-Log ""OK: Copied $copiedCount files, skipped $skippedCount files""

    # Step 4: Restart application
    Write-Log ""Step 4: Restarting application...""
    if (Test-Path ""{currentExePath}"") {{
        Start-Process -FilePath ""{currentExePath}""
        Write-Log ""OK: Application started""
    }}
    else {{
        Write-Log ""ERROR: Executable not found at {currentExePath}""
    }}

    # Step 5: Keep temp folder for debugging (do NOT delete update.log)
    Write-Log ""Step 5: Update complete, keeping log for debugging""
    Write-Log ""  Log file location: $LogFile""
    Write-Log ""  Temp folder: {tempUpdateFolder}""
    Write-Log ""  (Manual cleanup required)""

    Write-Log ""========== UPDATE SUCCESSFUL =========""
    exit 0
}}
catch {{
    Write-Log ""ERROR: $($_.Exception.Message)""
    Write-Log ""Stack Trace: $($_.ScriptStackTrace)""
    Write-Log ""Error Line: $($_.InvocationInfo.ScriptLineNumber)""

    # Restore backup on error
    Write-Log ""Attempting to restore backup...""
    try {{
        Copy-Item -Path ""{Path.Combine(tempUpdateFolder, "backup")}\*"" -Destination ""{installFolder}"" -Recurse -Force
        Write-Log ""OK: Backup restored successfully""
    }}
    catch {{
        Write-Log ""CRITICAL ERROR: Failed to restore backup: $($_.Exception.Message)""
    }}

    Write-Log ""========== UPDATE FAILED =========""
    Write-Log ""Log file saved at: $LogFile""
    exit 1
}}
";

                // Use UTF-8 with BOM for proper encoding (fixes Czech characters like Peťa → PeĹĄa)
                await File.WriteAllTextAsync(scriptPath, scriptContent, new System.Text.UTF8Encoding(true));
                Debug.WriteLine($"[UpdateService] ✓ PowerShell script created: {scriptPath}");
                Debug.WriteLine($"[UpdateService] Script size: {new FileInfo(scriptPath).Length} bytes");

                // Step 8: Start PowerShell update script
                Debug.WriteLine($"[UpdateService] Step 8: Launching PowerShell update script...");

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                var process = Process.Start(psi);
                if (process == null)
                {
                    Debug.WriteLine($"[UpdateService] ❌ ERROR: Failed to start PowerShell process!");
                    return false;
                }

                Debug.WriteLine($"[UpdateService] ✓ PowerShell script launched (PID: {process.Id})");
                Debug.WriteLine($"[UpdateService] ✓ Log file will be at: {Path.Combine(tempUpdateFolder, "update.log")}");
                Debug.WriteLine($"[UpdateService] ========== UPDATE PROCESS COMPLETE ==========");
                Debug.WriteLine($"[UpdateService] Application will now close and restart with new version.");

                return true;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[UpdateService] ❌ DOWNLOAD ERROR: {ex.Message}");
                Debug.WriteLine($"[UpdateService] StatusCode: {ex.StatusCode}");
                Debug.WriteLine($"[UpdateService] Stack trace: {ex.StackTrace}");
                CleanupTempFolder(tempUpdateFolder);
                return false;
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"[UpdateService] ❌ FILE I/O ERROR: {ex.Message}");
                Debug.WriteLine($"[UpdateService] Stack trace: {ex.StackTrace}");
                CleanupTempFolder(tempUpdateFolder);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"[UpdateService] ❌ ACCESS DENIED: {ex.Message}");
                Debug.WriteLine($"[UpdateService] This may require administrator privileges!");
                Debug.WriteLine($"[UpdateService] Stack trace: {ex.StackTrace}");
                CleanupTempFolder(tempUpdateFolder);
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] ❌ UNEXPECTED ERROR: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[UpdateService] Stack trace: {ex.StackTrace}");
                CleanupTempFolder(tempUpdateFolder);
                return false;
            }
        }

        private void CleanupTempFolder(string folderPath)
        {
            if (folderPath == null)
                return;

            try
            {
                if (Directory.Exists(folderPath))
                {
                    Debug.WriteLine($"[UpdateService] Cleaning up temp folder: {folderPath}");
                    Directory.Delete(folderPath, true);
                    Debug.WriteLine($"[UpdateService] ✓ Cleanup successful");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] ⚠ Cleanup warning: {ex.Message}");
            }
        }

        private bool IsNewerVersion(string latest, string current)
        {
            try
            {
                Debug.WriteLine($"[UpdateService] Comparing versions: current='{current}' vs latest='{latest}'");

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
                    {
                        Debug.WriteLine($"[UpdateService] Version comparison result: NEWER (part {i}: {latestNum} > {currentNum})");
                        return true;
                    }
                    if (latestNum < currentNum)
                    {
                        Debug.WriteLine($"[UpdateService] Version comparison result: OLDER (part {i}: {latestNum} < {currentNum})");
                        return false;
                    }
                }

                Debug.WriteLine($"[UpdateService] Version comparison result: SAME");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] ❌ Version comparison error: {ex.Message}");
                return false;
            }
        }
    }
}
