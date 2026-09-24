param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot "src\Softsphere.PrintBridge\Softsphere.PrintBridge.csproj"
$output = Join-Path $projectRoot "publish\$Runtime"

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $output

$standalone = Join-Path $output "Softsphere.PrintBridge-Standalone-win-x64.exe"
Copy-Item (Join-Path $output "Softsphere.PrintBridge.exe") $standalone -Force

Write-Host "Published to $output" -ForegroundColor Green
Write-Host "Deployment executable: $standalone" -ForegroundColor Green
