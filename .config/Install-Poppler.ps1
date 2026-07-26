[CmdletBinding()]
param(
    [Parameter()]
    [string]$Destination = [IO.Path]::Combine(
        $PSScriptRoot,
        '..',
        'build',
        'tools',
        'poppler')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$popplerVersion = '26.02.0-0'
$archiveName = "Release-$popplerVersion.zip"
$archiveUri = (
    'https://github.com/oschwartz10612/poppler-windows/' +
    "releases/download/v$popplerVersion/$archiveName")
$expectedArchiveHash =
    '993e4a94376ed712fafc7058d724ea0b943d118bbd2305cd9ed55174eb85cda5'
$expectedExecutableHash =
    '575a2f66073b256f69084c4f12f34427e6076b7854b2505cb09ecbdc9957fd7f'

$repositoryRoot = [IO.Path]::GetFullPath(
    [IO.Path]::Combine($PSScriptRoot, '..'))
$toolsRoot = [IO.Path]::GetFullPath(
    [IO.Path]::Combine($repositoryRoot, 'build', 'tools'))
$destinationPath = [IO.Path]::GetFullPath($Destination)
$toolsPrefix = $toolsRoot.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if (-not $destinationPath.StartsWith(
        $toolsPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Poppler destination must be beneath ${toolsRoot}: $destinationPath"
}

[IO.Directory]::CreateDirectory($toolsRoot) | Out-Null
$archivePath = [IO.Path]::Combine($toolsRoot, $archiveName)
$partialPath = "$archivePath.partial"
if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
    try {
        Invoke-WebRequest `
            -Uri $archiveUri `
            -OutFile $partialPath `
            -MaximumRetryCount 3 `
            -RetryIntervalSec 2
        Move-Item `
            -LiteralPath $partialPath `
            -Destination $archivePath `
            -ErrorAction Stop
    }
    finally {
        if (Test-Path -LiteralPath $partialPath) {
            Remove-Item -LiteralPath $partialPath -Force
        }
    }
}

$archiveHash = (
    Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
).Hash.ToLowerInvariant()
if ($archiveHash -ne $expectedArchiveHash) {
    throw (
        "Poppler archive hash mismatch for $archivePath. " +
        "Expected $expectedArchiveHash; found $archiveHash.")
}

if (Test-Path -LiteralPath $destinationPath) {
    $destinationItem = Get-Item -LiteralPath $destinationPath -Force
    if (-not $destinationItem.PSIsContainer) {
        throw "Poppler destination is not a directory: $destinationPath"
    }
    if ($destinationItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Refusing to replace reparse-point destination: $destinationPath"
    }

    Remove-Item -LiteralPath $destinationPath -Recurse -Force
}

Expand-Archive -LiteralPath $archivePath -DestinationPath $destinationPath
$executables = @(
    Get-ChildItem `
        -LiteralPath $destinationPath `
        -Filter pdftoppm.exe `
        -File `
        -Recurse
)
if ($executables.Count -ne 1) {
    throw (
        "Expected exactly one pdftoppm.exe in the pinned archive; " +
        "found $($executables.Count).")
}

$executablePath = $executables[0].FullName
$executableHash = (
    Get-FileHash -LiteralPath $executablePath -Algorithm SHA256
).Hash.ToLowerInvariant()
if ($executableHash -ne $expectedExecutableHash) {
    throw (
        "pdftoppm.exe hash mismatch. Expected $expectedExecutableHash; " +
        "found $executableHash.")
}

Write-Output $executablePath
