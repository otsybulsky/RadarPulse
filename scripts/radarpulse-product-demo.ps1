param(
    [Parameter(Position = 0)]
    [ValidateSet("help", "paths", "start", "readiness", "demo", "history", "reset-history", "verify")]
    [string]$Command = "help",

    [string]$Url = "http://127.0.0.1:5129",
    [string]$HistoryPath = "",
    [string]$RunId = "product-demo",

    [int]$Sources = 2,
    [int]$Batches = 2,
    [int]$EventsPerBatch = 2,

    [ValidateSet("none", "counter-checksum", "counter-checksum-heavy", "snapshot-counting")]
    [string]$Handlers = "counter-checksum",

    [switch]$SkipUiBuild,
    [switch]$AsJson
)

$ErrorActionPreference = "Stop"

function Get-RepositoryRoot {
    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
}

function Resolve-PackagePath {
    param(
        [string]$Value,
        [string]$BasePath
    )

    if ([System.IO.Path]::IsPathRooted($Value)) {
        return [System.IO.Path]::GetFullPath($Value)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Value))
}

function Get-DemoPaths {
    $repoRoot = Get-RepositoryRoot
    $operatorUiProject = Join-Path $repoRoot "src\Presentation\OperatorUi"
    $operatorUiDist = Join-Path $operatorUiProject "dist\OperatorUi\browser"
    $productHttpProject = Join-Path $repoRoot "src\Presentation\RadarPulse.Http\RadarPulse.Http.csproj"
    $demoRoot = Join-Path $repoRoot ".tmp\product-demo"

    if ([string]::IsNullOrWhiteSpace($HistoryPath)) {
        $resolvedHistoryPath = Join-Path $demoRoot "radarpulse-product-history.json"
    }
    else {
        $resolvedHistoryPath = Resolve-PackagePath $HistoryPath $repoRoot
    }

    [pscustomobject]@{
        RepositoryRoot = $repoRoot
        OperatorUiProject = [System.IO.Path]::GetFullPath($operatorUiProject)
        OperatorUiDist = [System.IO.Path]::GetFullPath($operatorUiDist)
        ProductHttpProject = [System.IO.Path]::GetFullPath($productHttpProject)
        DemoRoot = [System.IO.Path]::GetFullPath($demoRoot)
        HistoryPath = [System.IO.Path]::GetFullPath($resolvedHistoryPath)
        Url = $Url.TrimEnd("/")
    }
}

function Show-Help {
    $scriptName = Split-Path -Leaf $PSCommandPath
    Write-Host "RadarPulse local product demo/readiness package"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\$scriptName help"
    Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\$scriptName paths"
    Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\$scriptName start [-SkipUiBuild] [-Url http://127.0.0.1:5129]"
    Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\$scriptName readiness [-Url http://127.0.0.1:5129]"
    Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\$scriptName demo [-RunId product-demo]"
    Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\$scriptName history"
    Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\$scriptName reset-history"
    Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\$scriptName verify"
    Write-Host ""
    Write-Host "Scope:"
    Write-Host "  Local deterministic demo/archive-shaped workflows only."
    Write-Host "  This is not public production deployment, auth/TLS hardening, external adapter certification, or exactly-once delivery."
    Write-Host ""
    Write-Host "Docs:"
    Write-Host "  docs/product-demo-readiness.md"
}

function Write-Paths {
    param([pscustomobject]$Paths)

    if ($AsJson) {
        $Paths | ConvertTo-Json -Depth 6
        return
    }

    Write-Host "Repository root:        $($Paths.RepositoryRoot)"
    Write-Host "Operator UI project:    $($Paths.OperatorUiProject)"
    Write-Host "Operator UI dist:       $($Paths.OperatorUiDist)"
    Write-Host "Product HTTP project:   $($Paths.ProductHttpProject)"
    Write-Host "Demo workspace:         $($Paths.DemoRoot)"
    Write-Host "Product history path:   $($Paths.HistoryPath)"
    Write-Host "Local product URL:      $($Paths.Url)"
}

function Convert-HandlerSet {
    param([string]$Value)

    switch ($Value) {
        "none" { return 1 }
        "counter-checksum" { return 2 }
        "counter-checksum-heavy" { return 3 }
        "snapshot-counting" { return 4 }
        default { throw "Unsupported handler set '$Value'." }
    }
}

function Resolve-ProcessExecutable {
    param([string]$Executable)

    $cmdName = "$Executable.cmd"
    $cmd = Get-Command $cmdName -ErrorAction SilentlyContinue
    if ($cmd -ne $null -and -not [string]::IsNullOrWhiteSpace($cmd.Source)) {
        return $cmd.Source
    }

    $resolved = Get-Command $Executable -ErrorAction SilentlyContinue
    if ($resolved -ne $null -and -not [string]::IsNullOrWhiteSpace($resolved.Source)) {
        return $resolved.Source
    }

    return $Executable
}

function Invoke-CheckedProcess {
    param(
        [string]$Executable,
        [string[]]$CommandArguments,
        [string]$WorkingDirectory
    )

    $processFile = Resolve-ProcessExecutable $Executable
    Push-Location $WorkingDirectory
    try {
        Write-Host "Running: $Executable $($CommandArguments -join ' ')"
        & $processFile @CommandArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code $LASTEXITCODE`: $Executable $($CommandArguments -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

function Write-VerifyStep {
    param([string]$Name)

    Write-Host ""
    Write-Host "== $Name =="
}

function Invoke-ProductGet {
    param(
        [string]$BaseUrl,
        [string]$Path
    )

    $uri = "$($BaseUrl.TrimEnd('/'))$Path"
    return Invoke-RestMethod -Method Get -Uri $uri
}

function Invoke-ProductPost {
    param(
        [string]$BaseUrl,
        [string]$Path,
        [object]$Body
    )

    $uri = "$($BaseUrl.TrimEnd('/'))$Path"
    $json = $Body | ConvertTo-Json -Depth 8
    return Invoke-RestMethod -Method Post -Uri $uri -ContentType "application/json" -Body $json
}

function Write-ApiResponse {
    param(
        [object]$Response,
        [string]$Title
    )

    if ($AsJson) {
        $Response | ConvertTo-Json -Depth 16
        return
    }

    Write-Host $Title
    Write-Host "  status:  $($Response.statusCode)"
    Write-Host "  success: $($Response.isSuccess)"
    if (-not [string]::IsNullOrWhiteSpace($Response.message)) {
        Write-Host "  message: $($Response.message)"
    }
}

function Start-LocalProductHost {
    param([pscustomobject]$Paths)

    New-Item -ItemType Directory -Force -Path $Paths.DemoRoot | Out-Null

    if (-not $SkipUiBuild) {
        Invoke-CheckedProcess -Executable "npm" -CommandArguments @("run", "build") -WorkingDirectory $Paths.OperatorUiProject
    }

    $env:RadarPulse__ProductHttp__HistoryPath = $Paths.HistoryPath
    $env:RadarPulse__ProductHttp__UseInMemoryHistory = "false"
    $env:RadarPulse__ProductHttp__EnableOperatorUiStaticFiles = "true"
    $env:RadarPulse__ProductHttp__OperatorUiStaticAssetPath = $Paths.OperatorUiDist

    Write-Host "Starting RadarPulse.Http at $($Paths.Url)"
    Write-Host "History path: $($Paths.HistoryPath)"
    Write-Host "Operator UI static asset path: $($Paths.OperatorUiDist)"
    & dotnet run --project $Paths.ProductHttpProject --urls $Paths.Url
    exit $LASTEXITCODE
}

function Show-DemoReadiness {
    param([pscustomobject]$Paths)

    $response = Invoke-ProductGet $Paths.Url "/product/pipeline/host/demo-readiness"
    Write-ApiResponse $response "Product demo readiness"
    if (-not $AsJson) {
        Write-Host "  ready:   $($response.body.isReady)"
        if (-not [string]::IsNullOrWhiteSpace($response.body.firstBlockingReason)) {
            Write-Host "  blocker: $($response.body.firstBlockingReason)"
        }
        Write-Host "  history: $($response.body.history.status) - $($response.body.history.detail)"
        Write-Host "  ui:      $($response.body.operatorUi.status) - $($response.body.operatorUi.detail)"
    }

    if (-not $response.body.isReady) {
        exit 2
    }
}

function Invoke-DemoRun {
    param([pscustomobject]$Paths)

    $body = @{
        runId = $RunId
        sourceCount = $Sources
        batchCount = $Batches
        eventsPerBatch = $EventsPerBatch
        handlerSet = Convert-HandlerSet $Handlers
    }

    $response = Invoke-ProductPost $Paths.Url "/product/pipeline/runs/demo" $body
    Write-ApiResponse $response "Product demo run"
    if (-not $AsJson -and $response.body -ne $null) {
        Write-Host "  run id:    $($response.body.runId)"
        Write-Host "  state:     $($response.body.summary.runState)"
        Write-Host "  readiness: $($response.body.summary.readiness)"
        Write-Host "  handlers:  $($response.body.summary.handlerMode)"
    }

    if (-not $response.isSuccess) {
        exit 1
    }
}

function Show-History {
    param([pscustomobject]$Paths)

    $readiness = Invoke-ProductGet $Paths.Url "/product/pipeline/host/readiness"
    $runs = Invoke-ProductGet $Paths.Url "/product/pipeline/runs"

    if ($AsJson) {
        [pscustomobject]@{
            readiness = $readiness
            runs = $runs
        } | ConvertTo-Json -Depth 16
        return
    }

    Write-Host "Product history"
    Write-Host "  ready:       $($readiness.body.isReady)"
    Write-Host "  storage:     $($readiness.body.storageKind)"
    Write-Host "  identity:    $($readiness.body.storageIdentity)"
    Write-Host "  loaded runs: $($readiness.body.loadedRunCount)"
    if (-not [string]::IsNullOrWhiteSpace($readiness.body.firstBlockingReason)) {
        Write-Host "  blocker:     $($readiness.body.firstBlockingReason)"
    }
    Write-Host "  listed runs: $($runs.body.Count)"
    foreach ($run in $runs.body | Select-Object -First 5) {
        Write-Host "    $($run.runId) - $($run.runState) - $($run.readiness)"
    }
}

function Reset-History {
    param([pscustomobject]$Paths)

    $demoRoot = [System.IO.Path]::GetFullPath($Paths.DemoRoot)
    $historyPath = [System.IO.Path]::GetFullPath($Paths.HistoryPath)
    $prefix = $demoRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not $historyPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset history outside the demo workspace: $historyPath"
    }

    if (Test-Path -LiteralPath $historyPath) {
        Remove-Item -LiteralPath $historyPath -Force
        Write-Host "Removed product demo history: $historyPath"
    }
    else {
        Write-Host "Product demo history is already absent: $historyPath"
    }
}

function Invoke-PackagedVerify {
    param([pscustomobject]$Paths)

    $testProject = Join-Path $Paths.RepositoryRoot "tests\RadarPulse.Tests\RadarPulse.Tests.csproj"
    $solution = Join-Path $Paths.RepositoryRoot "RadarPulse.sln"
    $focusedFilter = "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

    Write-VerifyStep "Angular unit tests"
    Invoke-CheckedProcess -Executable "npm" -CommandArguments @("test", "--", "--watch=false") -WorkingDirectory $Paths.OperatorUiProject

    Write-VerifyStep "Angular production build"
    Invoke-CheckedProcess -Executable "npm" -CommandArguments @("run", "build") -WorkingDirectory $Paths.OperatorUiProject

    Write-VerifyStep "Operator UI browser smoke"
    Invoke-CheckedProcess -Executable "npm" -CommandArguments @("run", "smoke") -WorkingDirectory $Paths.OperatorUiProject

    Write-VerifyStep "Hosted same-origin browser smoke"
    Invoke-CheckedProcess -Executable "npm" -CommandArguments @("run", "smoke:hosted") -WorkingDirectory $Paths.OperatorUiProject

    Write-VerifyStep "Focused .NET product HTTP/API/readiness Release gate"
    Invoke-CheckedProcess -Executable "dotnet" -CommandArguments @(
        "test",
        $testProject,
        "-c",
        "Release",
        "--no-restore",
        "--filter",
        $focusedFilter) -WorkingDirectory $Paths.RepositoryRoot

    Write-VerifyStep ".NET Release build"
    Invoke-CheckedProcess -Executable "dotnet" -CommandArguments @(
        "build",
        $solution,
        "-c",
        "Release",
        "--no-restore") -WorkingDirectory $Paths.RepositoryRoot

    Write-Host ""
    Write-Host "Packaged verification passed."
}

try {
    $paths = Get-DemoPaths

    switch ($Command) {
        "help" {
            Show-Help
            exit 0
        }
        "paths" {
            Write-Paths $paths
            exit 0
        }
        "start" {
            Start-LocalProductHost $paths
        }
        "readiness" {
            Show-DemoReadiness $paths
            exit 0
        }
        "demo" {
            Invoke-DemoRun $paths
            exit 0
        }
        "history" {
            Show-History $paths
            exit 0
        }
        "reset-history" {
            Reset-History $paths
            exit 0
        }
        "verify" {
            Invoke-PackagedVerify $paths
            exit 0
        }
    }
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
