# AquaTrack - one-click database setup
# Run from E:\WaterProduct:   powershell -ExecutionPolicy Bypass -File .\scripts\setup-db.ps1
# Author: Attique

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$infra = "src/AquaTrack.Infrastructure"
$web   = "src/AquaTrack.Web"

function Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }

# 1. dotnet-ef tool
Step "Checking dotnet-ef tool"
$ef = dotnet tool list --global | Select-String "dotnet-ef"
if (-not $ef) {
    dotnet tool install --global dotnet-ef --version 8.0.11
    $env:PATH += ";$env:USERPROFILE\.dotnet\tools"
} else {
    Write-Host "dotnet-ef already installed"
}

# 2. Super Admin secrets (stored outside the repo, in %APPDATA%)
Step "Checking Super Admin login"
$secretsDir  = Join-Path $env:APPDATA "Microsoft\UserSecrets\aquatrack-web-2026"
$secretsFile = Join-Path $secretsDir "secrets.json"
if (-not (Test-Path $secretsFile) -or -not (Select-String -Path $secretsFile -Pattern "SuperAdmin:Phone" -Quiet)) {
    Write-Host ""
    Write-Host "Type your Super Admin mobile number and press Enter:" -ForegroundColor Yellow
    $phone = Read-Host "Mobile number"
    Write-Host "Type a password (at least 8 characters) and press Enter:" -ForegroundColor Yellow
    $pass  = Read-Host "Password"
    New-Item -ItemType Directory -Force -Path $secretsDir | Out-Null
    $json = @{ "SuperAdmin:Phone" = $phone; "SuperAdmin:Password" = $pass; "SuperAdmin:FullName" = "Attique" } | ConvertTo-Json
    Set-Content -Path $secretsFile -Value $json -Encoding UTF8
    Write-Host "Super Admin saved (outside Git)" -ForegroundColor Green
} else {
    Write-Host "Super Admin already set"
}

# 3. Build
Step "Building solution"
dotnet build AquaTrack.sln -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Build failed. Copy the errors above and send them to Claude." }

# 4. Migration (only if none exists yet)
Step "Creating migration"
$migDir = Join-Path $infra "Persistence/Migrations"
if (-not (Test-Path $migDir) -or -not (Get-ChildItem $migDir -Filter "*InitialCreate.cs" -ErrorAction SilentlyContinue)) {
    dotnet ef migrations add InitialCreate -p $infra -s $web -o Persistence/Migrations
    if ($LASTEXITCODE -ne 0) { throw "Migration failed. Send the error to Claude." }
} else {
    Write-Host "InitialCreate already exists"
}

# 5. Create / update the database
Step "Creating database tables"
dotnet ef database update -p $infra -s $web
if ($LASTEXITCODE -ne 0) { throw "Database update failed. Send the error to Claude." }

Write-Host "`nDONE. Database AquaTrackDb is ready." -ForegroundColor Green
Write-Host "Now press F5 in Visual Studio (or run: dotnet run --project $web)." -ForegroundColor Green
Write-Host "On first run the app adds roles, the Super Admin and the plans." -ForegroundColor Green
