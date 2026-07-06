# 縦横 (TateYoko) — developer command runner (single source of truth).
# Run under the mise-pinned toolchain:  mise exec -- just <recipe>
# (or, with mise activated in your shell,  just <recipe>).

set shell := ["bash", "-euo", "pipefail", "-c"]
set windows-shell := ["bash", "-euo", "pipefail", "-c"]

# Release defaults mirror tools/TateYoko.Pack's Options.Parse.
version := "0.1.0"
rid := "win-x64"

# List all recipes.
default:
    @just --list

# Provision the pinned toolchain (.NET 10 + just) from mise.toml.
setup:
    mise install

# Build the whole solution (Release).
build:
    dotnet build TateYoko.slnx -c Release

# All tests (Core unit + Pdf integration + ViewModel state machine).
test:
    dotnet test TateYoko.slnx -c Release

# Run the app in development (unpackaged).
run:
    dotnet run --project src/TateYoko.App

# Format the code in place (.editorconfig rules).
fmt:
    dotnet format TateYoko.slnx

# Verify formatting without writing — the CI gate.
fmt-check:
    dotnet format TateYoko.slnx --verify-no-changes

# Regenerate icon assets from assets/AppIcon.png.
icons:
    dotnet run --project tools/TateYoko.Icons

# Full distribution bundle + zip/checksum (local release flow = old `mise run publish`).
publish version=version rid=rid:
    dotnet run --project tools/TateYoko.Pack -c Release -- all --version {{version}} --rid {{rid}}

# Assemble the bundle only, no zip — the step CI signs between.
bundle version=version rid=rid:
    dotnet run --project tools/TateYoko.Pack -c Release -- bundle --version {{version}} --rid {{rid}}

# Zip + SHA256SUMS the (already-assembled) bundle.
package version=version rid=rid:
    dotnet run --project tools/TateYoko.Pack -c Release -- package --version {{version}} --rid {{rid}}

# Stage first-party PEs for Authenticode signing.
sign-stage:
    dotnet run --project tools/TateYoko.Pack -c Release -- sign-stage

# Copy signed PEs back into the bundle.
sign-collect:
    dotnet run --project tools/TateYoko.Pack -c Release -- sign-collect

# Print the bundle-relative first-party PE paths.
list-signable:
    dotnet run --project tools/TateYoko.Pack -c Release -- list-signable

# CycloneDX SBOM of the shipped app (routed through just so CI stops bypassing mise).
sbom version=version:
    mkdir -p build/sbom
    dotnet tool install --global CycloneDX || true
    dotnet CycloneDX src/TateYoko.App/TateYoko.App.csproj --output build/sbom --json --filename tateyoko.cdx.json --set-version {{version}}

# What CI runs: format gate + tests.
ci: fmt-check test

# Remove build + publish outputs.
clean:
    dotnet clean TateYoko.slnx -c Release
    rm -rf publish/win-x64 publish/package publish/sign-stage publish/signed publish/.launcher-* build/sbom
