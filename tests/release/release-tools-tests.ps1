[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repositoryRoot = [IO.Path]::GetFullPath(
    [IO.Path]::Combine($PSScriptRoot, '..', '..'))
$temporaryBase = [IO.Path]::GetFullPath(
    [IO.Path]::Combine(
        [IO.Path]::GetTempPath(),
        'TateYoko.ReleaseTools.Tests'))
$testRoot = [IO.Path]::Combine(
    $temporaryBase,
    [Guid]::NewGuid().ToString('N'))
$utf8NoBom = [Text.UTF8Encoding]::new($false)

function Assert-Condition {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Write-TestFile {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Content
    )

    $parent = [IO.Path]::GetDirectoryName($Path)
    if ([string]::IsNullOrWhiteSpace($parent)) {
        throw "Test file has no parent directory: $Path"
    }

    [IO.Directory]::CreateDirectory($parent) | Out-Null
    [IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Invoke-ReleaseTool {
    param(
        [Parameter(Mandatory)]
        [string]$AssemblyPath,

        [Parameter()]
        [string[]]$Arguments = @()
    )

    $output = @(
        & dotnet $AssemblyPath @Arguments 2>&1 |
            ForEach-Object { $_.ToString() }
    )
    [PSCustomObject]@{
        ExitCode = $LASTEXITCODE
        Text = $output -join [Environment]::NewLine
    }
}

function Write-CoverageReport {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Package,

        [Parameter(Mandatory)]
        [int]$LinesCovered,

        [Parameter(Mandatory)]
        [string]$LineRate,

        [Parameter(Mandatory)]
        [int]$BranchesCovered,

        [Parameter(Mandatory)]
        [string]$BranchRate
    )

    Write-TestFile -Path $Path -Content @"
<?xml version="1.0" encoding="utf-8"?>
<coverage
  lines-covered="$LinesCovered"
  lines-valid="100"
  line-rate="$LineRate"
  branches-covered="$BranchesCovered"
  branches-valid="100"
  branch-rate="$BranchRate">
  <packages>
    <package name="$Package" />
  </packages>
</coverage>
"@
}

function Write-ValidCoverageSet {
    param(
        [Parameter(Mandatory)]
        [string]$Root
    )

    Write-CoverageReport `
        -Path ([IO.Path]::Combine($Root, 'engine', 'engine.cobertura.xml')) `
        -Package 'TateYoko.Engine' `
        -LinesCovered 90 `
        -LineRate '0.9' `
        -BranchesCovered 85 `
        -BranchRate '0.85'
    Write-CoverageReport `
        -Path ([IO.Path]::Combine($Root, 'app', 'app.cobertura.xml')) `
        -Package 'TateYoko' `
        -LinesCovered 85 `
        -LineRate '0.85' `
        -BranchesCovered 80 `
        -BranchRate '0.8'
}

function Write-NoticeFixture {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter()]
        [object[]]$RestoreLogs = @()
    )

    $packageRoot = [IO.Path]::Combine($Root, 'packages')
    $packagePath = [IO.Path]::Combine(
        $packageRoot,
        'example.package',
        '1.2.3')
    [IO.Directory]::CreateDirectory($packagePath) | Out-Null
    Write-TestFile `
        -Path ([IO.Path]::Combine($packagePath, 'example.package.nuspec')) `
        -Content @'
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>Example.Package</id>
    <version>1.2.3</version>
    <authors>Example Authors</authors>
    <description>Release gate fixture.</description>
    <license type="expression">MIT</license>
    <copyright>Copyright (c) Example Authors</copyright>
    <projectUrl>https://example.invalid/package</projectUrl>
  </metadata>
</package>
'@

    $packageFolders = [ordered]@{}
    $packageFolders[$packageRoot + [IO.Path]::DirectorySeparatorChar] =
        [ordered]@{}
    $targetLibrary = [ordered]@{
        runtime = [ordered]@{
            'lib/net10.0/Example.Package.dll' = [ordered]@{}
        }
    }
    $targets = [ordered]@{
        'net10.0' = [ordered]@{
            'Example.Package/1.2.3' = $targetLibrary
        }
    }
    $libraries = [ordered]@{
        'Example.Package/1.2.3' = [ordered]@{
            type = 'package'
            path = 'example.package/1.2.3'
        }
    }
    $assets = [ordered]@{
        version = 3
        targets = $targets
        libraries = $libraries
        packageFolders = $packageFolders
        logs = $RestoreLogs
    }
    $assetsPath = [IO.Path]::Combine($Root, 'project.assets.json')
    Write-TestFile `
        -Path $assetsPath `
        -Content ($assets | ConvertTo-Json -Depth 12)
    return $assetsPath
}

[IO.Directory]::CreateDirectory($testRoot) | Out-Null

try {
    $packAssembly = [IO.Path]::Combine(
        $repositoryRoot,
        'tools',
        'TateYoko.Pack',
        'bin',
        'Release',
        'net10.0',
        'TateYoko.Pack.dll')
    $noticesAssembly = [IO.Path]::Combine(
        $repositoryRoot,
        'tools',
        'TateYoko.Notices',
        'bin',
        'Release',
        'net10.0',
        'TateYoko.Notices.dll')
    $qualityAssembly = [IO.Path]::Combine(
        $repositoryRoot,
        'tools',
        'TateYoko.Quality',
        'bin',
        'Release',
        'net10.0',
        'TateYoko.Quality.dll')
    foreach ($assembly in @($packAssembly, $noticesAssembly, $qualityAssembly)) {
        Assert-Condition `
            -Condition ([IO.File]::Exists($assembly)) `
            -Message "Build release tools before running contract tests: $assembly"
    }

    $signable = Invoke-ReleaseTool `
        -AssemblyPath $packAssembly `
        -Arguments @('list-signable')
    Assert-Condition `
        -Condition ($signable.ExitCode -eq 0) `
        -Message "Pack list-signable failed: $($signable.Text)"
    $expectedSignable = @(
        'TateYoko-win-x64.exe',
        'TateYoko-win-arm64.exe',
        'TateYoko.msixbundle'
    ) -join [Environment]::NewLine
    Assert-Condition `
        -Condition ($signable.Text -eq $expectedSignable) `
        -Message "Pack signing surface changed: $($signable.Text)"

    $invalidVersion = Invoke-ReleaseTool `
        -AssemblyPath $packAssembly `
        -Arguments @('build', '--version', 'invalid')
    Assert-Condition `
        -Condition (
            $invalidVersion.ExitCode -eq 1 -and
            $invalidVersion.Text.Contains(
                'numeric SemVer',
                [StringComparison]::Ordinal)) `
        -Message "Pack accepted an invalid version: $($invalidVersion.Text)"

    $unknownOption = Invoke-ReleaseTool `
        -AssemblyPath $packAssembly `
        -Arguments @('list-signable', '--unknown')
    Assert-Condition `
        -Condition (
            $unknownOption.ExitCode -eq 1 -and
            $unknownOption.Text.Contains(
                'Unknown option',
                [StringComparison]::Ordinal)) `
        -Message "Pack accepted an unknown option: $($unknownOption.Text)"

    $validCoverageRoot = [IO.Path]::Combine($testRoot, 'coverage-valid')
    Write-ValidCoverageSet -Root $validCoverageRoot
    $validCoverage = Invoke-ReleaseTool `
        -AssemblyPath $qualityAssembly `
        -Arguments @('coverage', $validCoverageRoot)
    Assert-Condition `
        -Condition (
            $validCoverage.ExitCode -eq 0 -and
            $validCoverage.Text.Contains(
                'engine: lines 90',
                [StringComparison]::Ordinal) -and
            $validCoverage.Text.Contains(
                'app: lines 85',
                [StringComparison]::Ordinal)) `
        -Message "Quality rejected exact budgets: $($validCoverage.Text)"

    Write-CoverageReport `
        -Path ([IO.Path]::Combine(
            $validCoverageRoot,
            'engine',
            'engine.cobertura.xml')) `
        -Package 'TateYoko.Engine' `
        -LinesCovered 89 `
        -LineRate '0.89' `
        -BranchesCovered 85 `
        -BranchRate '0.85'
    $belowBudget = Invoke-ReleaseTool `
        -AssemblyPath $qualityAssembly `
        -Arguments @('coverage', $validCoverageRoot)
    Assert-Condition `
        -Condition (
            $belowBudget.ExitCode -eq 1 -and
            $belowBudget.Text.Contains(
                'below budget',
                [StringComparison]::Ordinal)) `
        -Message "Quality accepted coverage below budget: $($belowBudget.Text)"

    Write-CoverageReport `
        -Path ([IO.Path]::Combine(
            $validCoverageRoot,
            'engine',
            'engine.cobertura.xml')) `
        -Package 'TateYoko.Engine' `
        -LinesCovered 90 `
        -LineRate '0.91' `
        -BranchesCovered 85 `
        -BranchRate '0.85'
    $inconsistentCoverage = Invoke-ReleaseTool `
        -AssemblyPath $qualityAssembly `
        -Arguments @('coverage', $validCoverageRoot)
    Assert-Condition `
        -Condition (
            $inconsistentCoverage.ExitCode -eq 1 -and
            $inconsistentCoverage.Text.Contains(
                'inconsistent line-rate',
                [StringComparison]::Ordinal)) `
        -Message (
            'Quality trusted an inconsistent declared rate: ' +
            $inconsistentCoverage.Text)

    $dtdCoverageRoot = [IO.Path]::Combine($testRoot, 'coverage-dtd')
    Write-ValidCoverageSet -Root $dtdCoverageRoot
    Write-TestFile `
        -Path ([IO.Path]::Combine(
            $dtdCoverageRoot,
            'engine',
            'engine.cobertura.xml')) `
        -Content @'
<?xml version="1.0"?>
<!DOCTYPE coverage [<!ENTITY probe SYSTEM "file:///C:/Windows/win.ini">]>
<coverage
  lines-covered="90"
  lines-valid="100"
  line-rate="0.9"
  branches-covered="85"
  branches-valid="100"
  branch-rate="0.85">
  <packages><package name="TateYoko.Engine" /></packages>
</coverage>
'@
    $dtdCoverage = Invoke-ReleaseTool `
        -AssemblyPath $qualityAssembly `
        -Arguments @('coverage', $dtdCoverageRoot)
    Assert-Condition `
        -Condition ($dtdCoverage.ExitCode -eq 1) `
        -Message 'Quality accepted a Cobertura document with a DTD.'

    $auditProjects = @(
        'src/TateYoko.App/TateYoko.App.csproj'
        'src/TateYoko.Engine/TateYoko.Engine.csproj'
        'tests/TateYoko.App.Tests/TateYoko.App.Tests.csproj'
        'tests/TateYoko.Engine.Tests/TateYoko.Engine.Tests.csproj'
        'tools/TateYoko.Icons/TateYoko.Icons.csproj'
        'tools/TateYoko.Notices/TateYoko.Notices.csproj'
        'tools/TateYoko.Pack/TateYoko.Pack.csproj'
        'tools/TateYoko.Quality/TateYoko.Quality.csproj'
    )
    $auditReport = [ordered]@{
        version = 1
        parameters = '--vulnerable --include-transitive'
        sources = @('https://api.nuget.org/v3/index.json')
        projects = @(
            $auditProjects | ForEach-Object {
                [ordered]@{
                    path = [IO.Path]::Combine(
                        $repositoryRoot,
                        $_.Replace('/', [IO.Path]::DirectorySeparatorChar))
                }
            }
        )
    }
    $auditPath = [IO.Path]::Combine($testRoot, 'audit-valid.json')
    Write-TestFile `
        -Path $auditPath `
        -Content ($auditReport | ConvertTo-Json -Depth 12)
    $validAudit = Invoke-ReleaseTool `
        -AssemblyPath $qualityAssembly `
        -Arguments @('audit', $auditPath)
    Assert-Condition `
        -Condition (
            $validAudit.ExitCode -eq 0 -and
            $validAudit.Text.Contains(
                '8 projects, 0 known vulnerabilities',
                [StringComparison]::Ordinal)) `
        -Message "Quality rejected a clean NuGet audit: $($validAudit.Text)"

    $auditReport.projects[0].frameworks = @(
        [ordered]@{
            framework = 'net10.0'
            topLevelPackages = @(
                [ordered]@{
                    id = 'Compromised.Package'
                    resolvedVersion = '1.2.3'
                    vulnerabilities = @(
                        [ordered]@{
                            severity = 'Critical'
                            advisoryurl = 'https://example.invalid/advisory'
                        }
                    )
                }
            )
        }
    )
    $vulnerableAuditPath = [IO.Path]::Combine(
        $testRoot,
        'audit-vulnerable.json')
    Write-TestFile `
        -Path $vulnerableAuditPath `
        -Content ($auditReport | ConvertTo-Json -Depth 12)
    $vulnerableAudit = Invoke-ReleaseTool `
        -AssemblyPath $qualityAssembly `
        -Arguments @('audit', $vulnerableAuditPath)
    Assert-Condition `
        -Condition (
            $vulnerableAudit.ExitCode -eq 1 -and
            $vulnerableAudit.Text.Contains(
                'Compromised.Package 1.2.3 (Critical)',
                [StringComparison]::Ordinal)) `
        -Message (
            'Quality accepted a known NuGet vulnerability: ' +
            $vulnerableAudit.Text)

    $auditReport.projects = @($auditReport.projects | Select-Object -Skip 1)
    $incompleteAuditPath = [IO.Path]::Combine(
        $testRoot,
        'audit-incomplete.json')
    Write-TestFile `
        -Path $incompleteAuditPath `
        -Content ($auditReport | ConvertTo-Json -Depth 12)
    $incompleteAudit = Invoke-ReleaseTool `
        -AssemblyPath $qualityAssembly `
        -Arguments @('audit', $incompleteAuditPath)
    Assert-Condition `
        -Condition (
            $incompleteAudit.ExitCode -eq 1 -and
            $incompleteAudit.Text.Contains(
                'project set is incomplete',
                [StringComparison]::Ordinal)) `
        -Message (
            'Quality accepted an incomplete NuGet audit: ' +
            $incompleteAudit.Text)

    $noticeRoot = [IO.Path]::Combine($testRoot, 'notices-valid')
    $noticeAssets = Write-NoticeFixture -Root $noticeRoot
    $noticeOutput = [IO.Path]::Combine($noticeRoot, 'THIRD-PARTY-NOTICES.txt')
    $validNotices = Invoke-ReleaseTool `
        -AssemblyPath $noticesAssembly `
        -Arguments @($noticeAssets, $noticeOutput)
    Assert-Condition `
        -Condition ($validNotices.ExitCode -eq 0) `
        -Message "Notices rejected a valid runtime graph: $($validNotices.Text)"
    $noticeText = [IO.File]::ReadAllText($noticeOutput)
    Assert-Condition `
        -Condition (
            $noticeText.Contains(
                'Example.Package 1.2.3',
                [StringComparison]::Ordinal) -and
            $noticeText.Contains(
                'License: MIT',
                [StringComparison]::Ordinal) -and
            $noticeText.Contains(
                'MIT License',
                [StringComparison]::Ordinal)) `
        -Message 'Notices omitted package identity or distributable MIT text.'

    $failedNoticeRoot = [IO.Path]::Combine($testRoot, 'notices-failed-restore')
    $failedNoticeAssets = Write-NoticeFixture `
        -Root $failedNoticeRoot `
        -RestoreLogs @(
            [ordered]@{
                code = 'NU9999'
                level = 'Error'
                message = 'Synthetic restore failure.'
            }
        )
    $failedNoticeOutput = [IO.Path]::Combine(
        $failedNoticeRoot,
        'must-not-exist.txt')
    $failedNotices = Invoke-ReleaseTool `
        -AssemblyPath $noticesAssembly `
        -Arguments @($failedNoticeAssets, $failedNoticeOutput)
    Assert-Condition `
        -Condition (
            $failedNotices.ExitCode -eq 1 -and
            $failedNotices.Text.Contains(
                'restore diagnostics',
                [StringComparison]::Ordinal) -and
            -not [IO.File]::Exists($failedNoticeOutput)) `
        -Message (
            'Notices accepted or wrote output for a failed restore graph: ' +
            $failedNotices.Text)

    Write-Output 'Release tool contract tests passed.'
}
finally {
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    $requiredPrefix = $temporaryBase.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) +
        [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedTestRoot.StartsWith(
            $requiredPrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove an unexpected test path: $resolvedTestRoot"
    }

    if ([IO.Directory]::Exists($resolvedTestRoot)) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
