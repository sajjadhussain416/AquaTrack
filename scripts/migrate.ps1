# AquaTrack - add a database migration and apply it
# Usage (from E:\WaterProduct):  powershell -ExecutionPolicy Bypass -File .\scripts\migrate.ps1 -Name Day6Payments
param([Parameter(Mandatory = $true)][string]$Name)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path -Parent $PSScriptRoot)
$infra = "src/AquaTrack.Infrastructure"
$web = "src/AquaTrack.Web"

Write-Host "`n==> Building" -ForegroundColor Cyan
dotnet build AquaTrack.sln -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Build failed. Copy the errors above and send them to Claude." }

Write-Host "`n==> Adding migration $Name" -ForegroundColor Cyan
dotnet ef migrations add $Name -p $infra -s $web -o Persistence/Migrations --no-build
if ($LASTEXITCODE -ne 0) { throw "Migration failed. Send the error to Claude." }

Write-Host "`n==> Updating database" -ForegroundColor Cyan
dotnet ef database update -p $infra -s $web
if ($LASTEXITCODE -ne 0) { throw "Database update failed. Send the error to Claude." }

Write-Host "`nDONE. Database is up to date. Press F5 in Visual Studio." -ForegroundColor Green
