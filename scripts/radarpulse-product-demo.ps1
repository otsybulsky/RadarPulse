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

function Join-DemoPath {
    param(
        [string]$Root,
        [string[]]$Segments
    )

    $path = $Root
    foreach ($segment in $Segments) {
        $path = Join-Path $path $segment
    }

    return [System.IO.Path]::GetFullPath($path)
}

function Convert-ToNativePath {
    param([string]$Value)

    if ([System.IO.Path]::DirectorySeparatorChar -eq "/") {
        return $Value.Replace("\", "/")
    }

    return $Value
}

function Get-PathStringComparison {
    if ([System.IO.Path]::DirectorySeparatorChar -eq "\") {
        return [System.StringComparison]::OrdinalIgnoreCase
    }

    return [System.StringComparison]::Ordinal
}

function Resolve-PackagePath {
    param(
        [string]$Value,
        [string]$BasePath
    )

    $nativeValue = Convert-ToNativePath $Value

    if ([System.IO.Path]::IsPathRooted($nativeValue)) {
        return [System.IO.Path]::GetFullPath($nativeValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $nativeValue))
}

function Get-DemoPaths {
    $repoRoot = Get-RepositoryRoot
    $operatorUiProject = Join-DemoPath -Root $repoRoot -Segments @("src", "Presentation", "OperatorUi")
    $operatorUiDist = Join-DemoPath -Root $operatorUiProject -Segments @("dist", "OperatorUi", "browser")
    $productHttpProject = Join-DemoPath -Root $repoRoot -Segments @("src", "Presentation", "RadarPulse.Http", "RadarPulse.Http.csproj")
    $demoRoot = Join-DemoPath -Root $repoRoot -Segments @(".tmp", "product-demo")

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
    $psCoreCommand = "pwsh -File scripts/$scriptName"
    $windowsPowerShellCommand = "powershell -ExecutionPolicy Bypass -File scripts\$scriptName"
    $unixShellCommand = "bash scripts/radarpulse-product-demo.sh"

    Write-Host "RadarPulse local product demo/readiness package"
    Write-Host "Default URL: http://127.0.0.1:5129"
    Write-Host ""
    Write-Host "Entrypoints:"
    Write-Host "  Windows PowerShell: $windowsPowerShellCommand help"
    Write-Host "  PowerShell 7:       $psCoreCommand help"
    Write-Host "  Linux/macOS/WSL2:   $unixShellCommand help"
    Write-Host ""
    Write-Host "Typical first run on Windows:"
    Write-Host "  1. $windowsPowerShellCommand paths"
    Write-Host "  2. $windowsPowerShellCommand reset-history"
    Write-Host "  3. $windowsPowerShellCommand start"
    Write-Host "  4. open http://127.0.0.1:5129"
    Write-Host "  5. $windowsPowerShellCommand readiness"
    Write-Host "  6. $windowsPowerShellCommand demo -RunId product-demo"
    Write-Host "  7. $windowsPowerShellCommand history"
    Write-Host ""
    Write-Host "Typical first run on Linux/macOS/WSL2:"
    Write-Host "  1. $unixShellCommand paths"
    Write-Host "  2. $unixShellCommand reset-history"
    Write-Host "  3. $unixShellCommand start"
    Write-Host "  4. open http://127.0.0.1:5129"
    Write-Host "  5. $unixShellCommand readiness"
    Write-Host "  6. $unixShellCommand demo --run-id product-demo"
    Write-Host "  7. $unixShellCommand history"
    Write-Host ""
    Write-Host "Commands:"
    Write-Host "  $windowsPowerShellCommand help"
    Write-Host "  $windowsPowerShellCommand paths"
    Write-Host "  $windowsPowerShellCommand start [-SkipUiBuild] [-Url http://127.0.0.1:5129]"
    Write-Host "  $windowsPowerShellCommand readiness [-Url http://127.0.0.1:5129]"
    Write-Host "  $windowsPowerShellCommand demo [-RunId product-demo]"
    Write-Host "  $windowsPowerShellCommand history"
    Write-Host "  $windowsPowerShellCommand reset-history"
    Write-Host "  $windowsPowerShellCommand verify"
    Write-Host ""
    Write-Host "Scope:"
    Write-Host "  Local deterministic demo/archive-shaped workflows only."
    Write-Host "  This is not public production deployment, auth/TLS hardening, external adapter certification, or exactly-once delivery."
    Write-Host "  Readiness blockers and warning-only scope posture stay visible."
    Write-Host "  Verify refreshes .NET restore metadata for the current OS before no-restore gates."
    Write-Host ""
    Write-Host "Docs:"
    Write-Host "  README.md"
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
    $comparison = Get-PathStringComparison

    if (-not $historyPath.StartsWith($prefix, $comparison)) {
        throw "Refusing to reset history outside the demo workspace: $historyPath"
    }

    if (Test-Path -LiteralPath $historyPath) {
        $item = Get-Item -LiteralPath $historyPath
        if ($item.PSIsContainer) {
            throw "Refusing to reset a directory as product demo history: $historyPath"
        }

        Remove-Item -LiteralPath $historyPath -Force
        Write-Host "Removed product demo history: $historyPath"
    }
    else {
        Write-Host "Product demo history is already absent: $historyPath"
    }
}

function Invoke-PackagedVerify {
    param([pscustomobject]$Paths)

    $testProject = Join-DemoPath -Root $Paths.RepositoryRoot -Segments @("tests", "RadarPulse.Tests", "RadarPulse.Tests.csproj")
    $solution = Join-Path $Paths.RepositoryRoot "RadarPulse.sln"
    $focusedFilter = "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
    $architectureFilter = "FullyQualifiedName~RadarPulseArchitectureTests"

    Write-VerifyStep "Angular unit tests"
    Invoke-CheckedProcess -Executable "npm" -CommandArguments @("test", "--", "--watch=false") -WorkingDirectory $Paths.OperatorUiProject

    Write-VerifyStep "Angular production build"
    Invoke-CheckedProcess -Executable "npm" -CommandArguments @("run", "build") -WorkingDirectory $Paths.OperatorUiProject

    Write-VerifyStep "Operator UI browser smoke"
    Invoke-CheckedProcess -Executable "npm" -CommandArguments @("run", "smoke") -WorkingDirectory $Paths.OperatorUiProject

    Write-VerifyStep "Hosted same-origin browser smoke"
    Invoke-CheckedProcess -Executable "npm" -CommandArguments @("run", "smoke:hosted") -WorkingDirectory $Paths.OperatorUiProject

    Write-VerifyStep ".NET dependency restore"
    Invoke-CheckedProcess -Executable "dotnet" -CommandArguments @(
        "restore",
        $solution,
        "--force") -WorkingDirectory $Paths.RepositoryRoot

    Write-VerifyStep ".NET architecture boundary gate"
    Invoke-CheckedProcess -Executable "dotnet" -CommandArguments @(
        "test",
        $testProject,
        "-c",
        "Release",
        "--no-restore",
        "--filter",
        $architectureFilter) -WorkingDirectory $Paths.RepositoryRoot

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
