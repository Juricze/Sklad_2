# ============================================================
# Sklad_2 Standalone Updater
# ============================================================
# Tento script aktualizuje Sklad_2 bez nutnosti spouštět aplikaci.
# Použití když:
# - Aplikace nefunguje (chybí .NET runtime)
# - Chcete aktualizovat bez spuštění aplikace
# - Chcete aktualizovat více instalací najednou
#
# Použití:
#   1. Pravý klik na tento soubor → "Spustit pomocí PowerShell"
#   2. Zadej cestu k instalaci Sklad_2 (nebo stiskni Enter pro výchozí)
#   3. Script stáhne a nainstaluje nejnovější verzi z GitHub
#
# ============================================================

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"  # Rychlejší download

# GitHub API URL
$GITHUB_API_URL = "https://api.github.com/repos/Juricze/Sklad_2/releases/latest"

# Funkce pro logování
function Write-ColorLog {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$timestamp] " -NoNewline -ForegroundColor DarkGray
    Write-Host $Message -ForegroundColor $Color
}

# ASCII Art Banner
Write-Host ""
Write-Host "  █████████  ████      █████ ██████     ██████ " -ForegroundColor Cyan
Write-Host "  ████ ████     ████████    ██" -ForegroundColor Cyan
Write-Host "  ████████████ ██     █████████  ██     █████" -ForegroundColor Cyan
Write-Host "  ██████ ██     ██████  ██    ██ " -ForegroundColor Cyan
Write-Host "  █████████  ███████████  ████████    ███████" -ForegroundColor Cyan
Write-Host "           " -ForegroundColor Cyan
Write-Host ""
Write-Host "  Standalone Updater v1.0" -ForegroundColor Yellow
Write-Host "  ==============================================" -ForegroundColor DarkGray
Write-Host ""

try {
    # ===========================================
    # KROK 1: Zjištění instalační cesty
    # ===========================================
    Write-ColorLog "KROK 1: Určení instalační cesty" "Yellow"

    # Výchozí cesta
    $defaultPath = Join-Path $env:USERPROFILE "Desktop\Sklad_2"

    Write-Host ""
    Write-Host "  Zadej cestu k instalaci Sklad_2:" -ForegroundColor White
    Write-Host "  (nebo stiskni Enter pro výchozí: $defaultPath)" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  Cesta: " -NoNewline -ForegroundColor Cyan

    $installPath = Read-Host
    if ([string]::IsNullOrWhiteSpace($installPath)) {
        $installPath = $defaultPath
        Write-ColorLog "Použita výchozí cesta: $installPath" "DarkGray"
    }

    # Kontrola existence složky
    if (-not (Test-Path $installPath)) {
        Write-Host ""
        Write-ColorLog "VAROVÁNÍ: Složka neexistuje!" "Yellow"
        Write-Host "  Chceš ji vytvořit? (A/N): " -NoNewline -ForegroundColor Cyan
        $create = Read-Host

        if ($create -eq "A" -or $create -eq "a") {
            New-Item -ItemType Directory -Path $installPath -Force | Out-Null
            Write-ColorLog "OK Složka vytvořena" "Green"
        } else {
            Write-ColorLog "Aktualizace zrušena" "Red"
            exit 1
        }
    }

    Write-ColorLog "OK Instalační cesta: $installPath" "Green"
    Write-Host ""

    # ===========================================
    # KROK 2: Kontrola GitHub API
    # ===========================================
    Write-ColorLog "KROK 2: Připojování k GitHub..." "Yellow"

    $headers = @{
        "User-Agent" = "Sklad_2-StandaloneUpdater"
    }

    $response = Invoke-RestMethod -Uri $GITHUB_API_URL -Headers $headers -Method Get

    $latestVersion = $response.tag_name
    $releaseNotes = $response.body
    $publishedAt = $response.published_at

    Write-ColorLog "OK Nejnovější verze: $latestVersion" "Green"
    Write-ColorLog "  Datum vydání: $publishedAt" "DarkGray"
    Write-Host ""

    # ===========================================
    # KROK 3: Hledání ZIP souboru
    # ===========================================
    Write-ColorLog "KROK 3: Hledání ZIP balíčku..." "Yellow"

    $zipAsset = $response.assets | Where-Object { $_.name -like "*.zip" } | Select-Object -First 1

    if (-not $zipAsset) {
        Write-ColorLog "X CHYBA: Nebyl nalezen ZIP soubor v release!" "Red"
        exit 1
    }

    $downloadUrl = $zipAsset.browser_download_url
    $zipFileName = $zipAsset.name
    $zipSizeMB = [math]::Round($zipAsset.size / 1MB, 2)

    Write-ColorLog "OK Nalezen: $zipFileName ($zipSizeMB MB)" "Green"
    Write-ColorLog "  URL: $downloadUrl" "DarkGray"
    Write-Host ""

    # ===========================================
    # KROK 4: Potvrzení aktualizace
    # ===========================================
    Write-Host "  ┌─────────────────────────────────────────────┐" -ForegroundColor DarkGray
    Write-Host "  │ " -NoNewline -ForegroundColor DarkGray
    Write-Host "Aktualizovat na verzi $latestVersion ?" -NoNewline -ForegroundColor Yellow
    Write-Host "       │" -ForegroundColor DarkGray
    Write-Host "  │ " -NoNewline -ForegroundColor DarkGray
    Write-Host "Velikost: $zipSizeMB MB" -NoNewline -ForegroundColor White
    Write-Host (" " * (29 - $zipSizeMB.ToString().Length)) -NoNewline
    Write-Host "│" -ForegroundColor DarkGray
    Write-Host "  │ " -NoNewline -ForegroundColor DarkGray
    Write-Host "Cíl: $installPath" -NoNewline -ForegroundColor White
    $padding = 40 - $installPath.Length
    if ($padding -gt 0) {
        Write-Host (" " * $padding) -NoNewline
    }
    Write-Host " │" -ForegroundColor DarkGray
    Write-Host "  └─────────────────────────────────────────────┘" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  Pokračovat? (A/N): " -NoNewline -ForegroundColor Cyan

    $confirm = Read-Host
    if ($confirm -ne "A" -and $confirm -ne "a") {
        Write-ColorLog "Aktualizace zrušena" "Yellow"
        exit 0
    }

    Write-Host ""

    # ===========================================
    # KROK 5: Stahování ZIP
    # ===========================================
    Write-ColorLog "KROK 5: Stahování aktualizace..." "Yellow"

    $tempFolder = Join-Path $env:TEMP "Sklad_2_StandaloneUpdate_$([Guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $tempFolder -Force | Out-Null

    $zipPath = Join-Path $tempFolder $zipFileName

    # Download s progress barem
    Write-Host "  Stahuji: " -NoNewline -ForegroundColor White
    Write-Host "$zipFileName" -ForegroundColor Cyan

    $webClient = New-Object System.Net.WebClient
    $webClient.Headers.Add("User-Agent", "Sklad_2-StandaloneUpdater")

    Register-ObjectEvent -InputObject $webClient -EventName DownloadProgressChanged -SourceIdentifier WebClient.DownloadProgressChanged -Action {
        $percent = $EventArgs.ProgressPercentage
        Write-Progress -Activity "Stahování aktualizace" -Status "$percent% dokončeno" -PercentComplete $percent
    } | Out-Null

    $webClient.DownloadFileAsync($downloadUrl, $zipPath)

    while ($webClient.IsBusy) {
        Start-Sleep -Milliseconds 100
    }

    Unregister-Event -SourceIdentifier WebClient.DownloadProgressChanged
    $webClient.Dispose()
    Write-Progress -Activity "Stahování aktualizace" -Completed

    Write-ColorLog "OK Staženo: $zipPath" "Green"
    Write-Host ""

    # ===========================================
    # KROK 6: Rozbalení ZIP
    # ===========================================
    Write-ColorLog "KROK 6: Rozbalování ZIP..." "Yellow"

    $extractPath = Join-Path $tempFolder "extracted"

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $extractPath)

    $fileCount = (Get-ChildItem -Path $extractPath -Recurse -File).Count
    Write-ColorLog "OK Rozbaleno: $fileCount souborů" "Green"
    Write-Host ""

    # ===========================================
    # KROK 7: Kontrola běžící aplikace
    # ===========================================
    Write-ColorLog "KROK 7: Kontrola běžící aplikace..." "Yellow"

    $runningProcess = Get-Process -Name "Sklad_2" -ErrorAction SilentlyContinue

    if ($runningProcess) {
        Write-ColorLog "! VAROVÁNÍ: Sklad_2 je spuštěn!" "Yellow"
        Write-Host "  Chceš aplikaci zavřít? (A/N): " -NoNewline -ForegroundColor Cyan
        $closeApp = Read-Host

        if ($closeApp -eq "A" -or $closeApp -eq "a") {
            Stop-Process -Name "Sklad_2" -Force
            Start-Sleep -Seconds 2
            Write-ColorLog "OK Aplikace zavřena" "Green"
        } else {
            Write-ColorLog "VAROVÁNÍ: Aktualizace může selhat pokud je aplikace spuštěna!" "Yellow"
            Write-Host "  Pokračovat i přesto? (A/N): " -NoNewline -ForegroundColor Cyan
            $continueAnyway = Read-Host

            if ($continueAnyway -ne "A" -and $continueAnyway -ne "a") {
                Write-ColorLog "Aktualizace zrušena" "Yellow"
                Remove-Item -Path $tempFolder -Recurse -Force -ErrorAction SilentlyContinue
                exit 0
            }
        }
    } else {
        Write-ColorLog "OK Aplikace není spuštěna" "Green"
    }

    Write-Host ""

    # ===========================================
    # KROK 8: Záloha (volitelná)
    # ===========================================
    Write-ColorLog "KROK 8: Vytvoření zálohy..." "Yellow"

    if (Test-Path (Join-Path $installPath "Sklad_2.exe")) {
        Write-Host "  Chceš vytvořit zálohu staré verze? (A/N): " -NoNewline -ForegroundColor Cyan
        $createBackup = Read-Host

        if ($createBackup -eq "A" -or $createBackup -eq "a") {
            $backupFolder = Join-Path $tempFolder "backup"
            New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null

            Copy-Item -Path "$installPath\*" -Destination $backupFolder -Recurse -Force -ErrorAction SilentlyContinue

            Write-ColorLog "OK Záloha vytvořena: $backupFolder" "Green"
            Write-ColorLog "  (Záloha bude smazána po restartování PC)" "DarkGray"
        } else {
            Write-ColorLog "○ Záloha přeskočena" "DarkGray"
        }
    } else {
        Write-ColorLog "○ Nová instalace - záloha nepotřebná" "DarkGray"
    }

    Write-Host ""

    # ===========================================
    # KROK 9: Kopírování souborů
    # ===========================================
    Write-ColorLog "KROK 9: Kopírování nových souborů..." "Yellow"

    $sourceFiles = Get-ChildItem -Path $extractPath -Recurse -File
    $copiedCount = 0
    $skippedCount = 0

    $sourceRoot = $extractPath
    if (-not $sourceRoot.EndsWith('\')) { $sourceRoot += '\' }

    foreach ($file in $sourceFiles) {
        try {
            $relativePath = $file.FullName.Substring($sourceRoot.Length)
            $targetPath = Join-Path $installPath $relativePath

            # Přeskočit user data
            if ($relativePath -like "*AppData*" -or
                $relativePath -like "*settings.json*" -or
                $relativePath -like "*sklad.db*" -or
                $relativePath -like "*ProductImages*") {
                $skippedCount++
                continue
            }

            $targetDir = Split-Path $targetPath -Parent
            if (-not (Test-Path $targetDir)) {
                New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
            }

            Copy-Item -Path $file.FullName -Destination $targetPath -Force
            $copiedCount++

            # Progress každých 50 souborů
            if ($copiedCount % 50 -eq 0) {
                Write-Host "  Zkopírováno: $copiedCount/$($sourceFiles.Count) souborů..." -ForegroundColor DarkGray
            }
        }
        catch {
            Write-ColorLog "  ! Chyba při kopírování: $relativePath" "Yellow"
        }
    }

    Write-ColorLog "OK Zkopírováno: $copiedCount souborů" "Green"
    Write-ColorLog "  Přeskočeno: $skippedCount souborů (user data)" "DarkGray"
    Write-Host ""

    # ===========================================
    # KROK 10: Dokončení
    # ===========================================
    Write-ColorLog "KROK 10: Dokončení..." "Yellow"

    # Cleanup temp (kromě zálohy)
    Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $extractPath -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host "  " -ForegroundColor Green
    Write-Host "   " -NoNewline -ForegroundColor Green
    Write-Host "OK AKTUALIZACE ÚSPĚŠNÁ!                      " -NoNewline -ForegroundColor White
    Write-Host "" -ForegroundColor Green
    Write-Host "  " -ForegroundColor Green
    Write-Host "   " -NoNewline -ForegroundColor Green
    Write-Host "Verze: $latestVersion" -NoNewline -ForegroundColor Cyan
    Write-Host (" " * (36 - $latestVersion.Length)) -NoNewline
    Write-Host "" -ForegroundColor Green
    Write-Host "   " -NoNewline -ForegroundColor Green
    Write-Host "Umístění: $installPath" -NoNewline -ForegroundColor White
    $padding = 31 - $installPath.Length
    if ($padding -gt 0) { Write-Host (" " * $padding) -NoNewline }
    Write-Host " " -ForegroundColor Green
    Write-Host "  " -ForegroundColor Green
    Write-Host ""

    # Nabídka spuštění
    Write-Host "  Chceš spustit Sklad_2? (A/N): " -NoNewline -ForegroundColor Cyan
    $launch = Read-Host

    if ($launch -eq "A" -or $launch -eq "a") {
        $exePath = Join-Path $installPath "Sklad_2.exe"
        if (Test-Path $exePath) {
            Start-Process -FilePath $exePath
            Write-ColorLog "OK Aplikace spuštěna" "Green"
        } else {
            Write-ColorLog "! Sklad_2.exe nenalezen!" "Yellow"
        }
    }

    Write-Host ""
    Write-ColorLog "Aktualizace dokončena. Stiskni ENTER pro ukončení..." "DarkGray"
    Read-Host | Out-Null

    exit 0
}
catch {
    Write-Host ""
    Write-ColorLog "X CHYBA: $($_.Exception.Message)" "Red"
    Write-ColorLog "Řádek: $($_.InvocationInfo.ScriptLineNumber)" "DarkGray"
    Write-Host ""
    Write-ColorLog "Pro pomoc kontaktuj vývojáře" "Yellow"
    Write-Host ""
    Write-Host "Stiskni ENTER pro ukončení..." -ForegroundColor DarkGray
    Read-Host | Out-Null
    exit 1
}
