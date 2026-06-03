param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^[a-zA-Z0-9_.-]+$')]
    [string]$PopupId,

    [Parameter(Mandatory = $true, Position = 1)]
    [datetime]$Deadline,

    [Parameter(Mandatory = $false, Position = 2)]
    [string]$ClearFlag = "false"
)

$ErrorActionPreference = "Stop"

$ClearFlagEnabled = $false

if ($ClearFlag -match '^(1|true|yes|y|tak)$') {
    $ClearFlagEnabled = $true
}

# Test PopupId - do not check/write flag for these IDs
# This must match your C# logic in IsTestPopupId()
$IsTestPopupId = $false

if ($PopupId -match '^TEST') {
    $IsTestPopupId = $true
}

Write-Host "PopupId: $PopupId"
Write-Host "Is test PopupId: $IsTestPopupId"

# Deadline popupu z argumentu
if ((Get-Date) -gt $Deadline) {
    Write-Host "Czas na popup juz minal"
    Write-Host "Deadline: $Deadline"
    exit 0
}

# Nazwa taska
$taskName = "PopupUser_$PopupId"

# Lokalizacja na udziale
$sourceDir = "\\sciezka\popup"
$sourceExe = Join-Path $sourceDir "popup 2.exe"
$sourceRtf = Join-Path $sourceDir "$PopupId.rtf"

# Lokalna lokalizacja
$localDir = "C:\ProgramData\TacticalPopup"
$localExe = Join-Path $localDir "popup 2.exe"
$localRtf = Join-Path $localDir "$PopupId.rtf"

# Znajdz zalogowanego usera po explorer.exe
$explorers = Get-CimInstance Win32_Process -Filter "Name='explorer.exe'" |
    Where-Object { $_.SessionId -gt 0 }

if (-not $explorers) {
    Write-Host "ERROR: No explorer.exe found - no interactive user session"
    exit 1
}

# Wez pierwszy explorer.exe z interaktywnej sesji
$explorer = $explorers | Sort-Object SessionId | Select-Object -First 1

$owner = Invoke-CimMethod -InputObject $explorer -MethodName GetOwner

if (-not $owner.User) {
    Write-Host "ERROR: Could not detect explorer.exe owner"
    exit 1
}

if ($owner.Domain) {
    $userId = "$($owner.Domain)\$($owner.User)"
}
else {
    $userId = $owner.User
}

Write-Host "Detected interactive user: $userId"
Write-Host "Session ID: $($explorer.SessionId)"

# Ustal sciezke flagi dla wykrytego interactive usera
try {
    $ownerSidResult = Invoke-CimMethod -InputObject $explorer -MethodName GetOwnerSid
    $userSid = $ownerSidResult.Sid

    if (-not $userSid) {
        throw "Could not detect user SID from explorer.exe"
    }

    $userProfile = Get-CimInstance Win32_UserProfile -Filter "SID='$userSid'" -ErrorAction Stop

    if (-not $userProfile.LocalPath) {
        throw "Could not detect user profile path for SID: $userSid"
    }

    $userLocalAppData = Join-Path $userProfile.LocalPath "AppData\Local"
    $userFlagPath = Join-Path $userLocalAppData "TacticalPopup\flags\$PopupId.ok"

    Write-Host "User profile: $($userProfile.LocalPath)"
    Write-Host "Flag path: $userFlagPath"
}
catch {
    Write-Host "ERROR: Failed to determine user flag path"
    Write-Host $_.Exception.Message
    exit 1
}

# Opcjonalne usunięcie flagi dla testów / ponownego wymuszenia popupu
if ($ClearFlagEnabled) {
    if ($IsTestPopupId) {
        Write-Host "ClearFlag enabled, but PopupId is test-only. No flag should exist for this PopupId."
    }
    else {
        Write-Host "ClearFlag enabled - trying to remove popup flag for detected user"

        try {
            Write-Host "Flag path: $userFlagPath"

            if (Test-Path $userFlagPath) {
                Remove-Item -Path $userFlagPath -Force
                Write-Host "OK: Flag removed"
            }
            else {
                Write-Host "OK: Flag does not exist"
            }
        }
        catch {
            Write-Host "WARNING: Failed to remove flag"
            Write-Host $_.Exception.Message
        }
    }
}

# Jesli flaga istnieje, nie wykonuj reszty skryptu
# Dla PopupId testowego pomijamy sprawdzanie flagi
if (-not $IsTestPopupId) {
    if (Test-Path $userFlagPath) {
        Write-Host "OK: Popup juz zostal odczytany przez uzytkownika. Pomijam."
        Write-Host "Flag exists: $userFlagPath"
        exit 0
    }
}
else {
    Write-Host "Test PopupId detected - skipping flag check."
}

# Przygotuj lokalny katalog
if (-not (Test-Path $localDir)) {
    New-Item -Path $localDir -ItemType Directory -Force | Out-Null
}

# Sprawdź źródła
if (-not (Test-Path $sourceExe)) {
    Write-Host "ERROR: Source EXE not found: $sourceExe"
    exit 1
}

if (-not (Test-Path $sourceRtf)) {
    Write-Host "ERROR: Source RTF not found: $sourceRtf"
    exit 1
}

# Skopiuj EXE i RTF lokalnie
Copy-Item -Path $sourceExe -Destination $localExe -Force
Copy-Item -Path $sourceRtf -Destination $localRtf -Force

if (-not (Test-Path $localExe)) {
    Write-Host "ERROR: Local EXE not found after copy: $localExe"
    exit 1
}

if (-not (Test-Path $localRtf)) {
    Write-Host "ERROR: Local RTF not found after copy: $localRtf"
    exit 1
}

# Start za 1 minute
$startTime = (Get-Date).AddMinutes(1)

# Koniec waznosci taska za 1 dzien
$endTime = (Get-Date).AddDays(1)

# Usuń poprzedni task jeśli istnieje
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

# Argumenty do EXE dla nowej wersji WPF:
# popup 2.exe -PopupId "ID" -RtfPath "C:\ProgramData\TacticalPopup\ID.rtf"
$exeArguments = "-PopupId `"$PopupId`" -RtfPath `"$localRtf`""

Write-Host "EXE arguments: $exeArguments"

# Akcja taska
$action = New-ScheduledTaskAction `
    -Execute $localExe `
    -Argument $exeArguments

# Trigger
$trigger = New-ScheduledTaskTrigger `
    -Once `
    -At $startTime

$trigger.EndBoundary = $endTime.ToString("yyyy-MM-ddTHH:mm:ss")

# Principal jako aktualny interactive user
$principal = New-ScheduledTaskPrincipal `
    -UserId $userId `
    -LogonType Interactive `
    -RunLevel Limited

# Settings
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable

# Rejestracja taska
Register-ScheduledTask `
    -TaskName $taskName `
    -Action $action `
    -Trigger $trigger `
    -Principal $principal `
    -Settings $settings `
    -Force | Out-Null

Write-Host "OK: Task created for $userId"
Write-Host "Start time: $startTime"
Write-Host "End boundary: $endTime"
Write-Host "EXE: $localExe"
Write-Host "RTF: $localRtf"
Write-Host "PopupId: $PopupId"

# Uruchom od razu
Start-ScheduledTask -TaskName $taskName

Write-Host "OK: Popup task started"

# Poczekaj chwilę, żeby Task Scheduler zdążył wystartować proces
Start-Sleep -Seconds 5

# Usuń task po uruchomieniu
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

Write-Host "OK: Task deleted"
