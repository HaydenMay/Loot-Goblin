[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Test-UnityProjectLocked {
    param(
        [Parameter(Mandatory = $true)]
        [string] $LockPath
    )

    if (-not (Test-Path -LiteralPath $LockPath)) {
        return $false
    }

    $lockStream = $null
    try {
        $lockStream = [System.IO.File]::Open(
            $LockPath,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
    }
    catch [System.IO.IOException] {
        return $true
    }
    finally {
        if ($null -ne $lockStream) {
            $lockStream.Dispose()
        }
    }

    Remove-Item -LiteralPath $LockPath -Force
    Write-Host "Removed stale Unity project lock: $LockPath"
    return $false
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$logDirectory = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory 'WebGLBuild.log'

$unityPath = if ([string]::IsNullOrWhiteSpace($env:UNITY_PATH)) {
    'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
}
else {
    $env:UNITY_PATH
}

if (-not (Test-Path -LiteralPath $unityPath -PathType Leaf)) {
    throw "Unity executable was not found at '$unityPath'. Set UNITY_PATH to the Unity.exe path to override it."
}

$lockPath = Join-Path $projectRoot 'Temp\UnityLockfile'
if (Test-UnityProjectLocked -LockPath $lockPath) {
    Write-Host "Unity build log: $logPath"
    [Console]::Error.WriteLine('The Loot Goblin Unity project is already open. Close Unity before running this batch build.')
    exit 1
}

Write-Host "Building WebGL to '$projectRoot\Builds\WebGL'..."
Write-Host "Unity build log: $logPath"

$unityArguments = @(
    '-batchmode',
    '-nographics',
    '-quit',
    '-projectPath', ('"{0}"' -f $projectRoot),
    '-buildTarget', 'WebGL',
    '-executeMethod', 'LootGoblinWebGLBuild.Build',
    '-logFile', ('"{0}"' -f $logPath)
)

$unityProcess = Start-Process -FilePath $unityPath -ArgumentList $unityArguments -Wait -PassThru
$exitCode = $unityProcess.ExitCode

if ($exitCode -ne 0) {
    [Console]::Error.WriteLine("Unity exited with code $exitCode. See '$logPath'.")
}

exit $exitCode
