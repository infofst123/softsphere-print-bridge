param(
    [int]$Port = 17833
)

$ErrorActionPreference = "Stop"
$ruleName = "Softsphere Print Bridge ($Port)"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator
)) {
    throw "Run this script from an elevated PowerShell window."
}

Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule `
    -DisplayName $ruleName `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort $Port `
    -Profile Private

Write-Host "Windows Firewall now allows TCP $Port on private networks." -ForegroundColor Green
