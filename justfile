# TateYoko's local and CI command surface.
# Windows 11 is the only supported development and release host.

set shell := ["pwsh", "-NoLogo", "-NoProfile", "-NonInteractive", "-Command"]
set windows-shell := ["pwsh", "-NoLogo", "-NoProfile", "-NonInteractive", "-Command"]

version := "0.1.0"

default:
    @just --list

# Install the exactly pinned PowerShell analyzer used by local and CI gates.
setup-powershell:
    if (-not (Get-Module -ListAvailable -Name PSScriptAnalyzer | Where-Object Version -EQ '1.25.0')) { Set-PSRepository -Name PSGallery -InstallationPolicy Trusted; Install-Module -Name PSScriptAnalyzer -RequiredVersion 1.25.0 -Repository PSGallery -Scope CurrentUser -Force -ErrorAction Stop }

# Download and verify the independent PDF renderer used by rendered golden tests.
setup-poppler:
    ./.config/Install-Poppler.ps1

# Install the exactly pinned host toolchain.
setup: setup-powershell setup-poppler
    mise install

# Restore manifest-pinned .NET tools and refresh NuGet lock files.
restore:
    dotnet tool restore
    dotnet restore TateYoko.slnx
    dotnet restore src/TateYoko.App/TateYoko.App.csproj --force-evaluate -p:DistributionMode=Msix -p:PublishReadyToRun=true

# Reproduce only the committed dependency graph; CI and releases use this.
restore-locked:
    dotnet tool restore
    dotnet restore TateYoko.slnx --locked-mode
    dotnet restore src/TateYoko.App/TateYoko.App.csproj --locked-mode -p:DistributionMode=Msix -p:PublishReadyToRun=true

# Compile every product, test, and release-gate project with strict analyzers.
build:
    dotnet build TateYoko.slnx -c Release --no-restore

# Run both Microsoft Testing Platform v2 test executables.
test: setup-poppler
    dotnet test TateYoko.slnx -c Release --no-restore

# Exercise the release gate executables as black boxes with valid and hostile fixtures.
release-tools-test:
    ./tests/release/release-tools-tests.ps1

# Enforce deterministic line/branch budgets for Engine and application logic.
coverage: setup-poppler
    if (Test-Path -LiteralPath build/coverage) { Remove-Item -LiteralPath build/coverage -Recurse -Force }
    dotnet test tests/TateYoko.Engine.Tests/TateYoko.Engine.Tests.csproj -c Release --no-restore --results-directory build/coverage/engine --coverlet
    dotnet test tests/TateYoko.App.Tests/TateYoko.App.Tests.csproj -c Release --no-restore --results-directory build/coverage/app --coverlet
    dotnet run --project tools/TateYoko.Quality -c Release --no-restore -- coverage build/coverage

# Prove that Engine tests kill at least 90% of all supported mutations.
mutation: setup-poppler
    $exitCode = 0; Push-Location src/TateYoko.Engine; try { dotnet stryker --skip-version-check; $exitCode = $LASTEXITCODE } finally { Pop-Location }; if ($exitCode -ne 0) { exit $exitCode }

# Fail if the restored graph contains a low-or-higher known vulnerability.
audit:
    New-Item -ItemType Directory -Force build/audit | Out-Null
    $report = 'build/audit/nuget-vulnerabilities.json'; $output = & dotnet package list --project TateYoko.slnx --vulnerable --include-transitive --no-restore --format json --output-version 1; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; [IO.File]::WriteAllLines($report, $output, [Text.UTF8Encoding]::new($false))
    dotnet run --project tools/TateYoko.Quality -c Release --no-restore -- audit build/audit/nuget-vulnerabilities.json

# Generate the exact third-party legal text from the restored shipping graph.
notices:
    dotnet run --project tools/TateYoko.Notices -c Release --no-restore -- src/TateYoko.App/obj/project.assets.json build/legal/THIRD-PARTY-NOTICES.txt

# Generate runtime and source-file SBOMs only from a REUSE-compliant tree.
sbom version=version:
    reuse lint
    New-Item -ItemType Directory -Force build/sbom | Out-Null
    dotnet CycloneDX src/TateYoko.App/TateYoko.App.csproj -o build/sbom -F Json -fn tateyoko.cdx.json -sv {{version}} -sn TateYoko -st Application -ed
    reuse spdx --creator-person "Yasunobu Sakashita" -o build/sbom/tateyoko-sources.spdx

# Build, register, and launch the packaged development app through the pinned WinApp CLI.
run:
    ./BuildAndRun.ps1 src/TateYoko.App/TateYoko.App.csproj /p:Configuration=Debug /p:Platform=x64

# Exercise the running app through Windows UI Automation and capture RC evidence.
ui-test app_pid:
    ./tests/ui/ui-tests.ps1 -AppPid {{app_pid}}

# Regenerate all icon and MSIX logo assets from the checked-in source icon.
icons:
    dotnet run --project tools/TateYoko.Icons -c Release --no-restore

# Prove generated icon assets are current and byte-for-byte deterministic.
icons-check:
    ./.config/Test-Icons.ps1

# Format every checked-in C# source deterministically without evaluating WinUI generated files.
fmt:
    dotnet tool run csharpier format .

# Verify C# formatting without modifying the worktree.
fmt-check:
    dotnet tool run csharpier check .

# Validate GitHub Actions syntax, expressions, job dependencies, and shell snippets.
actions-check:
    actionlint

# Check spelling in source, tests, configuration, documentation, and translations.
typos-check:
    typos

# Reject every PowerShell analyzer diagnostic, including information-level rules.
powershell-check:
    ./.config/Test-PowerShell.ps1

# Require complete, machine-readable copyright and license information for every file.
reuse-lint:
    reuse lint

# Build and validate the unsigned x64/ARM64 MSIX bundle.
dist-build version=version:
    dotnet run --project tools/TateYoko.Pack -c Release --no-restore -- build --version {{version}}

# Copy exactly the release files that must receive Authenticode signatures.
sign-stage version=version:
    dotnet run --project tools/TateYoko.Pack -c Release --no-restore -- stage-signing --version {{version}}

# Collect externally signed artifacts and immediately validate them.
sign-collect version=version:
    dotnet run --project tools/TateYoko.Pack -c Release --no-restore -- collect-signing --version {{version}}

# Revalidate signer identity, timestamp, bundle architecture, and AppInstaller.
sign-verify version=version:
    dotnet run --project tools/TateYoko.Pack -c Release --no-restore -- verify --version {{version}}

# Assemble release files only after signatures, SBOM, notices, and license exist.
package version=version:
    dotnet run --project tools/TateYoko.Pack -c Release --no-restore -- package --version {{version}}

# Print the authoritative external-signing input list.
list-signable:
    dotnet run --project tools/TateYoko.Pack -c Release --no-restore -- list-signable

# Full merge gate. The dependency graph and every executable are version-pinned.
ci: restore-locked reuse-lint typos-check actions-check powershell-check fmt-check build icons-check release-tools-test test coverage audit notices mutation

# Remove only known generated directories beneath the repository.
clean:
    dotnet clean TateYoko.slnx -c Release
    foreach ($path in @('publish', 'build')) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force } }
