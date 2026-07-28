[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath(
    [IO.Path]::Combine($PSScriptRoot, '..'))
$assetDirectory = [IO.Path]::Combine(
    $repositoryRoot,
    'src',
    'TateYoko.App',
    'Assets')
$outputNames = @(
    'AppIcon.ico',
    'SplashScreen.scale-200.png',
    'Square150x150Logo.scale-200.png',
    'Square44x44Logo.scale-200.png',
    'Square44x44Logo.targetsize-24_altform-unplated.png',
    'Square44x44Logo.targetsize-48_altform-lightunplated.png',
    'StoreLogo.png',
    'Wide310x150Logo.scale-200.png'
)
$outputPaths = @(
    $outputNames |
        ForEach-Object { [IO.Path]::Combine($assetDirectory, $_) }
)

$checkedInHashes = @{}
foreach ($path in $outputPaths) {
    $checkedInHashes[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

& dotnet run `
    --project ([IO.Path]::Combine(
        $repositoryRoot,
        'tools',
        'TateYoko.Icons')) `
    -c Release `
    --no-restore
if ($LASTEXITCODE -ne 0) {
    throw 'Icon generation failed.'
}

$generatedHashes = @{}
foreach ($path in $outputPaths) {
    $generatedHashes[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($generatedHashes[$path] -ne $checkedInHashes[$path]) {
        throw "Generated icon is stale: $path"
    }
}

& dotnet run `
    --project ([IO.Path]::Combine(
        $repositoryRoot,
        'tools',
        'TateYoko.Icons')) `
    -c Release `
    --no-restore
if ($LASTEXITCODE -ne 0) {
    throw 'Second icon generation failed.'
}

foreach ($path in $outputPaths) {
    $secondHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($secondHash -ne $generatedHashes[$path]) {
        throw "Icon generation is nondeterministic: $path"
    }
}

Write-Output "Icon assets are current and deterministic: $($outputPaths.Count)/$($outputPaths.Count)."
