[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$requiredVersion = [Version]'1.25.0'
$repositoryRoot = [IO.Path]::GetFullPath(
    [IO.Path]::Combine($PSScriptRoot, '..'))
$module = Get-Module -ListAvailable -Name PSScriptAnalyzer |
    Where-Object { $_.Version -eq $requiredVersion } |
    Select-Object -First 1
if ($null -eq $module) {
    throw (
        "PSScriptAnalyzer $requiredVersion is required. " +
        'Run just setup-powershell.')
}

Import-Module -Name $module.Path -Force -ErrorAction Stop
$scriptPaths = @(
    Get-Item -LiteralPath (
        [IO.Path]::Combine($repositoryRoot, 'BuildAndRun.ps1'))
    Get-ChildItem `
        -LiteralPath ([IO.Path]::Combine($repositoryRoot, '.config')) `
        -Filter '*.ps1' `
        -File
    Get-ChildItem `
        -LiteralPath ([IO.Path]::Combine($repositoryRoot, 'tests')) `
        -Filter '*.ps1' `
        -File `
        -Recurse
) |
    Select-Object -ExpandProperty FullName -Unique |
    Sort-Object
$scriptPaths = @(
    $scriptPaths
)
$diagnostics = @(
    foreach ($scriptPath in $scriptPaths) {
        Invoke-ScriptAnalyzer `
            -Path $scriptPath `
            -Severity Error,Warning,Information
    }
)
if ($diagnostics.Count -ne 0) {
    foreach ($diagnostic in $diagnostics |
            Sort-Object ScriptName,Line,RuleName) {
        [Console]::Error.WriteLine(
            '{0}:{1}: {2} {3}: {4}',
            $diagnostic.ScriptName,
            $diagnostic.Line,
            $diagnostic.Severity,
            $diagnostic.RuleName,
            $diagnostic.Message)
    }

    exit 1
}

Write-Output (
    "PSScriptAnalyzer ${requiredVersion}: " +
    "$($scriptPaths.Count) scripts clean.")
