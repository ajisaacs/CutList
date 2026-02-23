<#
Deploy CutList.Web as a Windows Service

Examples:
  # Run from repository root:
  powershell -ExecutionPolicy Bypass -File scripts/Deploy-CutListWeb.ps1 -ServiceName CutListWeb -InstallDir C:\Services\CutListWeb -Urls "http://*:5270" -OpenFirewall

  # Run from scripts directory:
  powershell -ExecutionPolicy Bypass -File Deploy-CutListWeb.ps1 -ServiceName CutListWeb -InstallDir C:\Services\CutListWeb -Urls "http://*:5270" -OpenFirewall

Requires: dotnet SDK/runtime installed and administrative privileges.
#>

Param(
    [string]$ServiceName = "CutListWeb",
    [string]$PublishConfiguration = "Release",
    [string]$InstallDir = "C:\Services\CutListWeb",
    [string]$Urls = "http://*:5270",
    [switch]$OpenFirewall,
    [int]$ServiceStopTimeoutSeconds = 30,
    [int]$ServiceStartTimeoutSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Detect repository root (parent of scripts directory or current directory if already at root)
$ScriptDir = Split-Path -Parent $PSCommandPath
$RepoRoot = if ((Split-Path -Leaf $ScriptDir) -eq 'scripts') {
    Split-Path -Parent $ScriptDir
} else {
    $ScriptDir
}

Write-Host "Repository root: $RepoRoot"
$ProjectPath = Join-Path $RepoRoot 'CutList.Web\CutList.Web.csproj'

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "Project not found at: $ProjectPath"
}

function Ensure-Dir($path) {
    if (-not (Test-Path -LiteralPath $path)) {
        New-Item -ItemType Directory -Path $path | Out-Null
    }
}

function Stop-And-DeleteService($name) {
    $svc = Get-Service -Name $name -ErrorAction SilentlyContinue
    if ($null -ne $svc) {
        if ($svc.Status -ne 'Stopped') {
            Write-Host "Stopping service '$name'..."
            Stop-Service -Name $name -Force -ErrorAction SilentlyContinue
            try { $svc.WaitForStatus('Stopped',[TimeSpan]::FromSeconds($ServiceStopTimeoutSeconds)) | Out-Null } catch {}
            # If still running, kill by PID
            $q = & sc.exe queryex $name 2>$null
            $pidLine = $q | Where-Object { $_ -match 'PID' }
            if ($pidLine -and ($pidLine -match '(\d+)$')) {
                $procId = [int]$Matches[1]
                if ($procId -gt 0) {
                    try { Write-Host "Killing service process PID=$procId ..."; Stop-Process -Id $procId -Force } catch {}
                }
            }
        }
        Write-Host "Deleting service '$name'..."
        sc.exe delete $name | Out-Null
        Start-Sleep -Seconds 1
    }
}

function Publish-App() {
    Write-Host "Publishing CutList.Web to $InstallDir ..."
    Ensure-Dir $InstallDir

    # Run dotnet publish directly - output will be visible
    & dotnet publish $ProjectPath -c $PublishConfiguration -o $InstallDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}

function Stop-ExeLocks($path) {
    $procs = Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and ($_.Path -ieq $path)
    }
    foreach ($p in $procs) {
        try { Write-Host "Killing process $($p.Id) $($p.ProcessName) ..."; Stop-Process -Id $p.Id -Force } catch {}
    }
    # Wait until unlocked
    for ($i=0; $i -lt 50; $i++) {
        $still = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -and ($_.Path -ieq $path) }
        if (-not $still) { break }
        Start-Sleep -Milliseconds 200
    }
}

function Create-Service($name, $bin, $urls) {
    $binPath = '"' + $bin + '" --urls ' + $urls
    Write-Host "Creating service '$name' with binPath: $binPath"
    # Note: space after '=' is required for sc.exe syntax
    sc.exe create $name binPath= "$binPath" start= auto DisplayName= "$name" | Out-Null
    # Set recovery to restart on failure
    sc.exe failure $name reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
    sc.exe description $name 'CutList bin packing web application' | Out-Null
}

function Start-ServiceSafe($name) {
    Write-Host "Starting service '$name'..."
    Start-Service -Name $name
    (Get-Service -Name $name).WaitForStatus('Running',[TimeSpan]::FromSeconds($ServiceStartTimeoutSeconds)) | Out-Null
    sc.exe query $name | Write-Host
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK/Runtime not found in PATH. Please install .NET 8+ or add it to PATH."
}

Stop-And-DeleteService -name $ServiceName
Stop-ExeLocks -path (Join-Path $InstallDir 'CutList.Web.exe')
try { Remove-Item -LiteralPath (Join-Path $InstallDir 'CutList.Web.exe') -Force -ErrorAction SilentlyContinue } catch {}
Publish-App

$exe = Join-Path $InstallDir 'CutList.Web.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Expected published executable not found: $exe"
}

Create-Service -name $ServiceName -bin $exe -urls $Urls

if ($OpenFirewall) {
    $port = ($Urls -split ':')[-1]
    if ($port -match '^(\d+)$') {
        $ruleName = "$ServiceName HTTP $port"
        $existingRule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
        if ($null -eq $existingRule) {
            Write-Host "Creating firewall rule for TCP port $port ..."
            New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $port -Action Allow | Out-Null
        } else {
            Write-Host "Firewall rule '$ruleName' already exists, skipping creation."
        }
    }
}

Start-ServiceSafe -name $ServiceName

Write-Host "Deployment complete. Service '$ServiceName' is running."
