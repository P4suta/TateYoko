<#
.SYNOPSIS
Builds and optionally runs the TateYoko WinUI 3 application.

.EXAMPLE
.\BuildAndRun.ps1 src\TateYoko.App\TateYoko.App.csproj
.\BuildAndRun.ps1 src\TateYoko.App\TateYoko.App.csproj -SkipRun
.\BuildAndRun.ps1 src\TateYoko.App\TateYoko.App.csproj /p:Platform=ARM64
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Project = 'src\TateYoko.App\TateYoko.App.csproj',
    [switch]$SkipRun,
    [switch]$Detach,
    [switch]$Symbols,
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$ExtraArgs
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$developerMode = $false
try {
    $registryPath =
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
    if (Test-Path -LiteralPath $registryPath) {
        $value = Get-ItemProperty `
            -LiteralPath $registryPath `
            -Name AllowDevelopmentWithoutDevLicense `
            -ErrorAction SilentlyContinue
        $developerMode = $value.AllowDevelopmentWithoutDevLicense -eq 1
    }
}
catch {
    $developerMode = $false
}

if (-not (Test-Path -LiteralPath $Project -PathType Leaf)) {
    Write-Error "Project does not exist: $Project"
    exit 1
}
$Project = (Resolve-Path -LiteralPath $Project).Path

$platform = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'ARM64' } else { 'x64' }
$configuration = 'Debug'
foreach ($argument in $ExtraArgs) {
    if ($argument -match '^[/-]p:Platform=(x64|ARM64)$') {
        $platform = $Matches[1]
    }
    elseif ($argument -match '^[/-]p:Configuration=(Debug|Release)$') {
        $configuration = $Matches[1]
    }
}

$buildArguments = @(
    'build'
    $Project
    '--nologo'
    '--verbosity'
    'minimal'
)
$hasPlatformArgument = $ExtraArgs |
    Where-Object { $_ -match '^[/-]p:Platform=' }
$hasConfigurationArgument = $ExtraArgs |
    Where-Object { $_ -match '^[/-]p:Configuration=' }
if (-not $hasPlatformArgument) {
    $buildArguments += "-p:Platform=$platform"
}
if (-not $hasConfigurationArgument) {
    $buildArguments += "-p:Configuration=$configuration"
}
foreach ($argument in $ExtraArgs) {
    if ($argument -match '^[/-]p:(.+)$') {
        $normalized = "-p:$($Matches[1])"
        if ($normalized -notin $buildArguments) {
            $buildArguments += $normalized
        }
    }
    else {
        $buildArguments += $argument
    }
}

Write-Output "--> Building $configuration/$platform"
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

if ($SkipRun) {
    Write-Output '--> Build succeeded; launch skipped.'
    exit 0
}

if (-not $developerMode) {
    Write-Error (
        'Developer Mode is required for packaged WinUI deployment. ' +
        'Enable Settings > System > For developers > Developer Mode.'
    )
    exit 1
}

$propertyArguments = @(
    $Project
    '--nologo'
)
foreach ($argument in $buildArguments | Select-Object -Skip 2) {
    if ($argument -like '-p:*') {
        $propertyArguments += $argument
    }
}

$winappPath = (
    & dotnet msbuild @propertyArguments '-getProperty:WinAppCliPath'
).Trim()
if ($LASTEXITCODE -ne 0 `
    -or [string]::IsNullOrWhiteSpace($winappPath) `
    -or -not (Test-Path -LiteralPath $winappPath -PathType Leaf)) {
    Write-Error (
        'The pinned WinApp CLI could not be resolved from ' +
        'Microsoft.Windows.SDK.BuildTools.WinApp.'
    )
    exit 1
}

$outputDirectory = (
    & dotnet msbuild @propertyArguments '-getProperty:TargetDir'
).Trim()
if ($LASTEXITCODE -ne 0 `
    -or [string]::IsNullOrWhiteSpace($outputDirectory) `
    -or -not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    Write-Error 'The built WinUI output directory could not be resolved.'
    exit 1
}

$runArguments = @('run', $outputDirectory)
if ($Detach) {
    $runArguments += @('--detach', '--json')
}
else {
    $runArguments += '--debug-output'
    if ($Symbols) {
        $runArguments += '--symbols'
    }
}

Write-Output "--> Launching $outputDirectory"
& $winappPath @runArguments
exit $LASTEXITCODE
