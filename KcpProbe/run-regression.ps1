param(
    [string]$Ip = "",
    [int]$Port = 0,
    [int]$Conv = 1001,
    [int]$Count = 200,
    [int]$Timeout = 5000,
    [string]$Configuration = "Debug",
    [string]$ServerExePath = "",
    [string]$ServerCommand = "",
    [switch]$SkipServer,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

function Resolve-ServerExe {
    param([string]$CppRoot)
    $candidates = @(
        (Join-Path $CppRoot "build\server.exe"),
        (Join-Path $CppRoot "build\Debug\server.exe"),
        (Join-Path $CppRoot "build\Release\server.exe"),
        (Join-Path $CppRoot "build_msvc\Debug\server.exe"),
        (Join-Path $CppRoot "build_msvc\Release\server.exe"),
        (Join-Path $CppRoot "build_test\server.exe")
    )
    foreach ($item in $candidates) {
        if (Test-Path $item) {
            return $item
        }
    }
    return $null
}

function Resolve-ConfigValue {
    param(
        [string]$ConfigPath,
        [string]$Section,
        [string]$Key
    )
    if (-not (Test-Path $ConfigPath)) {
        return $null
    }
    $currentSection = ""
    foreach ($raw in Get-Content -Path $ConfigPath) {
        $line = $raw.Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
            continue
        }
        if ($line.EndsWith(":")) {
            $currentSection = $line.Substring(0, $line.Length - 1)
            continue
        }
        if ($currentSection -ieq $Section -and $line.StartsWith("${Key}:")) {
            $value = $line.Substring($line.IndexOf(":") + 1).Trim().Trim('"')
            return $value
        }
    }
    return $null
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$appRoot = $scriptDir
$app1Root = Split-Path -Parent $appRoot
$demoRoot = Split-Path -Parent (Split-Path -Parent $app1Root)
$cppRoot = Join-Path $demoRoot "cpp-server"
$cppConfig = Join-Path $cppRoot "config.yaml"
$smokeProject = Join-Path $appRoot "Kcp.SmokeTests\Kcp.SmokeTests.csproj"

if ([string]::IsNullOrWhiteSpace($Ip)) {
    $configIp = Resolve-ConfigValue -ConfigPath $cppConfig -Section "server" -Key "ip"
    if (-not [string]::IsNullOrWhiteSpace($configIp)) {
        $Ip = if ($configIp -eq "0.0.0.0") { "127.0.0.1" } else { $configIp }
    } else {
        $Ip = "127.0.0.1"
    }
}

if ($Port -le 0) {
    $configPort = Resolve-ConfigValue -ConfigPath $cppConfig -Section "server" -Key "port"
    $parsedPort = 0
    if (-not [string]::IsNullOrWhiteSpace($configPort) -and [int]::TryParse($configPort, [ref]$parsedPort)) {
        $Port = $parsedPort
    } else {
        $Port = 8888
    }
}

if (-not (Test-Path $smokeProject)) {
    throw "Smoke test project not found: $smokeProject"
}

$serverProcess = $null
$smokeExitCode = 1

try {
    if (-not $SkipServer) {
        if (-not [string]::IsNullOrWhiteSpace($ServerCommand)) {
            Write-Host "Start server command: $ServerCommand"
            if (-not $DryRun) {
                $serverProcess = Start-Process -FilePath "powershell" -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $ServerCommand) -WorkingDirectory $cppRoot -PassThru
            }
        }
        else {
            if ([string]::IsNullOrWhiteSpace($ServerExePath)) {
                $ServerExePath = Resolve-ServerExe -CppRoot $cppRoot
            }
            if ([string]::IsNullOrWhiteSpace($ServerExePath) -or -not (Test-Path $ServerExePath)) {
                throw "server.exe not found. Build server first, or use -ServerExePath, -ServerCommand, or -SkipServer."
            }
            Write-Host "Start server executable: $ServerExePath"
            if (-not $DryRun) {
                $serverProcess = Start-Process -FilePath $ServerExePath -WorkingDirectory $cppRoot -PassThru
            }
        }

        if (-not $DryRun) {
            Start-Sleep -Seconds 1
        }
    }

    $smokeArgs = @(
        "run",
        "--project", $smokeProject,
        "--configuration", $Configuration,
        "--",
        "--ip", $Ip,
        "--port", $Port,
        "--conv", $Conv,
        "--count", $Count,
        "--timeout", $Timeout
    )

    Write-Host "Run smoke tests: dotnet $($smokeArgs -join ' ')"
    if ($DryRun) {
        $smokeExitCode = 0
    }
    else {
        & dotnet @smokeArgs
        $smokeExitCode = $LASTEXITCODE
    }
}
finally {
    if ($serverProcess -and -not $serverProcess.HasExited) {
        Write-Host "Stop server process PID=$($serverProcess.Id)"
        Stop-Process -Id $serverProcess.Id -Force
    }
}

if ($smokeExitCode -eq 0) {
    Write-Host "Regression passed"
}
else {
    Write-Host "Regression failed, exit code: $smokeExitCode"
}

exit $smokeExitCode
