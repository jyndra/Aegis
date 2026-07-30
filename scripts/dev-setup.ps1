# Aegis Development Environment Setup Script

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Aegis Development Environment Bootstrap " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Create Data Directories under %ProgramData%\Aegis
$ProgramDataPath = [System.IO.Path]::Combine($env:ProgramData, "Aegis")
$Directories = @("logs", "keys", "policies", "backups")

Write-Host "`n[1/3] Ensuring Data Directories at $ProgramDataPath..." -ForegroundColor Yellow

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

# 2. Deploy default appsettings.json to ProgramData if not present
$SourceConfig = Join-Path $PSScriptRoot "..\src\Aegis.Service\appsettings.json"
$TargetConfig = Join-Path $ProgramDataPath "appsettings.json"

Write-Host "`n[2/3] Checking Configuration file..." -ForegroundColor Yellow
if (Test-Path -Path $SourceConfig) {
    if (-not (Test-Path -Path $TargetConfig)) {
        Copy-Item -Path $SourceConfig -Destination $TargetConfig -Force
        Write-Host "  + Deployed default configuration to $TargetConfig" -ForegroundColor Green
    } else {
        Write-Host "  . Config file exists at $TargetConfig" -ForegroundColor Gray
    }
}

# 3. Create initial Dev HMAC Key stub if missing
$DevKeyFile = Join-Path $ProgramDataPath "keys\hmac.key"
Write-Host "`n[3/3] Checking Dev Cryptographic Keys..." -ForegroundColor Yellow
if (-not (Test-Path -Path $DevKeyFile)) {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    [System.IO.File]::WriteAllBytes($DevKeyFile, $bytes)
    Write-Host "  + Generated dev HMAC key at $DevKeyFile" -ForegroundColor Green
} else {
    Write-Host "  . Dev HMAC key exists at $DevKeyFile" -ForegroundColor Gray
}

Write-Host "`n[SUCCESS] Aegis Development Bootstrap Complete!" -ForegroundColor Green
