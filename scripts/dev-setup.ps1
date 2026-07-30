# Aegis Development Environment Setup Script (Milestone 1)

param (
    [switch]$RegisterService,
    [switch]$UnregisterService
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Aegis Development Environment Bootstrap " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Unregister Service if requested
if ($UnregisterService) {
    Write-Host "`n[Unregistering Aegis Service via sc.exe]..." -ForegroundColor Yellow
    try {
        sc.exe stop AegisService 2>$null | Out-Null
        sc.exe delete AegisService 2>$null | Out-Null
        Write-Host "  + AegisService removed." -ForegroundColor Green
    } catch {
        Write-Host "  . AegisService was not installed." -ForegroundColor Gray
    }
    return
}

# 2. Create Data Directories under %ProgramData%\Aegis
$ProgramDataPath = [System.IO.Path]::Combine($env:ProgramData, "Aegis")
$Directories = @("logs", "keys", "policies", "backups")

Write-Host "`n[1/4] Ensuring Data Directories at $ProgramDataPath..." -ForegroundColor Yellow

if (-not (Test-Path -Path $ProgramDataPath)) {
    New-Item -ItemType Directory -Path $ProgramDataPath -Force | Out-Null
    Write-Host "  + Created root: $ProgramDataPath" -ForegroundColor Green
}

foreach ($dir in $Directories) {
    $targetPath = [System.IO.Path]::Combine($ProgramDataPath, $dir)
    if (-not (Test-Path -Path $targetPath)) {
        New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
        Write-Host "  + Created subdirectory: $dir" -ForegroundColor Green
    } else {
        Write-Host "  . Directory exists: $dir" -ForegroundColor Gray
    }
}

# 3. Deploy default appsettings.json to ProgramData
$SourceConfig = Join-Path $PSScriptRoot "..\src\Aegis.Service\appsettings.json"
$TargetConfig = Join-Path $ProgramDataPath "appsettings.json"

Write-Host "`n[2/4] Checking Configuration file..." -ForegroundColor Yellow
if (Test-Path -Path $SourceConfig) {
    if (-not (Test-Path -Path $TargetConfig)) {
        Copy-Item -Path $SourceConfig -Destination $TargetConfig -Force
        Write-Host "  + Deployed default configuration to $TargetConfig" -ForegroundColor Green
    } else {
        Write-Host "  . Config file exists at $TargetConfig" -ForegroundColor Gray
    }
}

# 4. Create initial Dev HMAC Key stub
$DevKeyFile = Join-Path $ProgramDataPath "keys\hmac.key"
Write-Host "`n[3/4] Checking Dev Cryptographic Keys..." -ForegroundColor Yellow
if (-not (Test-Path -Path $DevKeyFile)) {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    [System.IO.File]::WriteAllBytes($DevKeyFile, $bytes)
    Write-Host "  + Generated dev HMAC key at $DevKeyFile" -ForegroundColor Green
} else {
    Write-Host "  . Dev HMAC key exists at $DevKeyFile" -ForegroundColor Gray
}

# 5. Optional Windows Service Registration via sc.exe (Milestone 1)
Write-Host "`n[4/4] Windows Service Registration Check..." -ForegroundColor Yellow
if ($RegisterService) {
    $ServiceExe = Join-Path $PSScriptRoot "..\src\Aegis.Service\bin\Debug\net8.0-windows10.0.19041.0\Aegis.Service.exe"
    if (Test-Path -Path $ServiceExe) {
        Write-Host "  + Registering AegisService with sc.exe..." -ForegroundColor Green
        sc.exe create AegisService binPath= "`"$ServiceExe`"" start= auto DisplayName= "Aegis Protection Service"
        Write-Host "  + AegisService registered successfully." -ForegroundColor Green
    } else {
        Write-Host "  ! Service executable not found at $ServiceExe. Build solution first." -ForegroundColor Red
    }
} else {
    Write-Host "  . Service registration skipped (use -RegisterService flag to register with sc.exe)." -ForegroundColor Gray
}

Write-Host "`n[Extension Dev Note]: To load MV3 browser extension in Chrome/Edge:" -ForegroundColor Cyan
Write-Host "  1. Navigate to chrome://extensions or edge://extensions" -ForegroundColor Gray
Write-Host "  2. Enable 'Developer mode'" -ForegroundColor Gray
Write-Host "  3. Click 'Load unpacked' and select directory: $(Resolve-Path (Join-Path $PSScriptRoot '..\extension'))" -ForegroundColor Gray

Write-Host "`n[SUCCESS] Aegis Milestone 1 Development Setup Complete!" -ForegroundColor Green
