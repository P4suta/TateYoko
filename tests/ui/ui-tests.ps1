<#
.SYNOPSIS
Runs TateYoko's release-candidate UI Automation flow against an already-running app.

.EXAMPLE
.\tests\ui\ui-tests.ps1 -AppPid 12345
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$AppPid
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not (Get-Process -Id $AppPid -ErrorAction SilentlyContinue)) {
    throw "No running process has PID $AppPid."
}

$appProject = Join-Path $repositoryRoot 'src\TateYoko.App\TateYoko.App.csproj'
$winAppQuery = @(
    & dotnet msbuild $appProject --nologo '-getProperty:WinAppCliPath'
)
$winAppQueryExitCode = $LASTEXITCODE
$winAppPath = ($winAppQuery -join [Environment]::NewLine).Trim()
if ($winAppQueryExitCode -ne 0 `
    -or [string]::IsNullOrWhiteSpace($winAppPath) `
    -or -not (Test-Path -LiteralPath $winAppPath -PathType Leaf)) {
    throw (
        'The pinned WinApp CLI could not be resolved from ' +
        'Microsoft.Windows.SDK.BuildTools.WinApp. Run just restore first.'
    )
}

$winAppPath = (Resolve-Path -LiteralPath $winAppPath).Path
Set-Alias -Name winapp -Value $winAppPath -Scope Script -Option ReadOnly

$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$resultsDirectory = Join-Path $repositoryRoot "build\ui-tests\$runId"
$screenshotsDirectory = Join-Path $resultsDirectory 'screenshots'
New-Item -ItemType Directory -Force -Path $screenshotsDirectory | Out-Null

$inputPath = Join-Path $resultsDirectory 'input.pdf'
$outputPath = Join-Path $resultsDirectory 'chosen-output.pdf'
$resultsPath = Join-Path $resultsDirectory 'test-results.json'
$script:pass = 0
$script:fail = 0
$script:results = @()
$script:pickerHwnd = $null

function Write-PdfFixture {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][ValidateRange(1, 20000)][int]$PageCount
    )

    $contentObjectId = $PageCount + 3
    $pageReferences = 0..($PageCount - 1) |
        ForEach-Object { "$($_ + 3) 0 R" }
    $objects = [System.Collections.Generic.List[string]]::new($PageCount + 3)
    $objects.Add("1 0 obj`n<< /Type /Catalog /Pages 2 0 R >>`nendobj`n")
    $objects.Add(
        "2 0 obj`n<< /Type /Pages /Kids [$($pageReferences -join ' ')] " +
        "/Count $PageCount >>`nendobj`n"
    )
    for ($index = 0; $index -lt $PageCount; $index++) {
        $pageObjectId = $index + 3
        $width = if ($index % 2 -eq 0) { 200 } else { 300 }
        $height = if ($index % 2 -eq 0) { 300 } else { 200 }
        $objects.Add(
            "$pageObjectId 0 obj`n<< /Type /Page /Parent 2 0 R " +
            "/MediaBox [0 0 $width $height] /Contents $contentObjectId 0 R >>`nendobj`n"
        )
    }
    $objects.Add(
        "$contentObjectId 0 obj`n<< /Length 0 >>`nstream`n`nendstream`nendobj`n"
    )

    $encoding = [System.Text.Encoding]::ASCII
    $builder = [System.Text.StringBuilder]::new()
    $null = $builder.Append("%PDF-1.4`n")
    $offsets = [System.Collections.Generic.List[int]]::new()
    foreach ($object in $objects) {
        $offsets.Add($builder.Length)
        $null = $builder.Append($object)
    }

    $xrefOffset = $builder.Length
    $objectCount = $objects.Count + 1
    $null = $builder.Append("xref`n0 $objectCount`n")
    $null = $builder.Append("0000000000 65535 f `n")
    foreach ($offset in $offsets) {
        $null = $builder.Append(('{0:D10} 00000 n ' -f $offset)).Append("`n")
    }

    $null = $builder.Append("trailer`n<< /Size $objectCount /Root 1 0 R >>`n")
    $null = $builder.Append("startxref`n$xrefOffset`n%%EOF`n")
    [System.IO.File]::WriteAllBytes(
        $Path,
        $encoding.GetBytes($builder.ToString())
    )
}

function Test-UI {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Script
    )

    try {
        $global:LASTEXITCODE = 0
        $output = & $Script 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw ($output -join [Environment]::NewLine)
        }

        $script:pass++
        $script:results += @{ name = $Name; status = 'PASS' }
    }
    catch {
        $script:fail++
        $script:results += @{ name = $Name; status = 'FAIL'; detail = "$_" }
    }
}

function Wait-PickerWindow {
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        $windows = winapp ui list-windows -a $AppPid --json 2>$null |
            ConvertFrom-Json
        $candidate = $windows |
            Where-Object {
                $_.ownerHwnd -eq $script:mainHwnd -and
                $_.className -eq '#32770'
            } |
            Select-Object -First 1
        if ($candidate) {
            return $candidate.hwnd
        }

        Start-Sleep -Milliseconds 100
    }

    throw 'The Windows file picker did not appear within five seconds.'
}

function Get-PickerSelector {
    param(
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$Type
    )

    $search = winapp ui search $AutomationId -w $script:pickerHwnd --json 2>$null |
        ConvertFrom-Json
    $selectorMatches = @(
        $search.matches |
            Where-Object {
                $_.automationId -eq $AutomationId -and
                $_.type -eq $Type
            }
    )
    if ($selectorMatches.Count -ne 1) {
        throw (
            "Expected one $Type with AutomationId $AutomationId in picker " +
            "$($script:pickerHwnd); found $($selectorMatches.Count)."
        )
    }

    return $selectorMatches[0].selector
}

function Get-PickerFileNameSelector {
    foreach ($candidate in @(
        @{ AutomationId = '1001'; Type = 'Edit' }
        @{ AutomationId = '1148'; Type = 'Edit' }
    )) {
        $search = winapp ui search `
            $candidate.AutomationId `
            -w $script:pickerHwnd `
            --json `
            2>$null |
            ConvertFrom-Json
        $selectorMatches = @(
            $search.matches |
                Where-Object {
                    $_.automationId -eq $candidate.AutomationId -and
                    $_.type -eq $candidate.Type
                }
        )
        if ($selectorMatches.Count -eq 1) {
            return $selectorMatches[0].selector
        }

        if ($selectorMatches.Count -gt 1) {
            throw (
                "Expected at most one $($candidate.Type) with AutomationId " +
                "$($candidate.AutomationId) in picker $($script:pickerHwnd); " +
                "found $($selectorMatches.Count)."
            )
        }
    }

    throw "The file name editor was not found in picker $($script:pickerHwnd)."
}

function Wait-PickerClosed {
    param([Parameter(Mandatory)][long]$Hwnd)

    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        $windows = winapp ui list-windows -a $AppPid --json 2>$null |
            ConvertFrom-Json
        if (-not ($windows | Where-Object { $_.hwnd -eq $Hwnd })) {
            return
        }

        Start-Sleep -Milliseconds 100
    }

    throw "The Windows file picker $Hwnd did not close within five seconds."
}

function Invoke-PickerButton {
    param([Parameter(Mandatory)][string]$AutomationId)

    $selector = Get-PickerSelector -AutomationId $AutomationId -Type 'Button'
    $hwnd = $script:pickerHwnd
    winapp ui invoke $selector -w $hwnd
    Wait-PickerClosed -Hwnd $hwnd
}

function Complete-PdfSelection {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ExpectedFileName
    )

    $fileNameSelector = Get-PickerFileNameSelector
    winapp ui set-value $fileNameSelector $Path -w $script:pickerHwnd
    Invoke-PickerButton -AutomationId '1'
    winapp ui wait-for `
        'InputFileText' `
        -w $script:mainHwnd `
        --value $ExpectedFileName `
        -t 5000
}

function Select-PdfThroughPicker {
    param(
        [Parameter(Mandatory)][string]$ButtonId,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ExpectedFileName
    )

    winapp ui invoke $ButtonId -w $script:mainHwnd
    $script:pickerHwnd = Wait-PickerWindow
    Complete-PdfSelection -Path $Path -ExpectedFileName $ExpectedFileName
}

function Select-ConversionOutput {
    param(
        [Parameter(Mandatory)][string]$ButtonId,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ExpectedSuggestion
    )

    winapp ui invoke $ButtonId -w $script:mainHwnd
    $script:pickerHwnd = Wait-PickerWindow
    $fileNameSelector = Get-PickerFileNameSelector
    winapp ui wait-for `
        $fileNameSelector `
        -w $script:pickerHwnd `
        --value $ExpectedSuggestion `
        -t 3000
    winapp ui set-value $fileNameSelector $Path -w $script:pickerHwnd
    Invoke-PickerButton -AutomationId '1'
}

function Save-Screenshot {
    param([Parameter(Mandatory)][string]$Name)

    winapp ui screenshot `
        -w $script:mainHwnd `
        -o (Join-Path $screenshotsDirectory $Name) `
        2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not capture $Name."
    }
}

function Test-Accessibility {
    param([Parameter(Mandatory)][string]$State)

    try {
        $inspection = winapp ui inspect `
            -w $script:mainHwnd `
            --interactive `
            --json `
            2>$null |
            ConvertFrom-Json
        $interactive = @(
            $inspection.windows |
                Where-Object { $_.hwnd -eq $script:mainHwnd } |
                ForEach-Object { $_.elements } |
                Where-Object {
                    $type = $_.PSObject.Properties['type']?.Value
                    $name = $_.PSObject.Properties['name']?.Value
                    $className = $_.PSObject.Properties['className']?.Value
                    $type -match 'Button|Edit|PasswordBox|ProgressBar|RadioButton' -and
                    $name -notmatch 'Minimize|Maximize|Close|System' -and
                    $className -notmatch 'PickerHost|#32770|CabinetWClass'
                }
        )
        $missing = @(
            $interactive |
                Where-Object {
                    -not $_.PSObject.Properties['automationId'] -or
                    [string]::IsNullOrWhiteSpace($_.automationId)
                }
        )
        if ($missing.Count -gt 0) {
            $names = $missing |
                ForEach-Object { "$($_.type) '$($_.name)'" }
            throw "Missing AutomationId: $($names -join ', ')"
        }

        $unnamed = @(
            $interactive |
                Where-Object {
                    -not $_.PSObject.Properties['name'] -or
                    [string]::IsNullOrWhiteSpace($_.name)
                }
        )
        if ($unnamed.Count -gt 0) {
            throw "Interactive elements have no accessible name: $($unnamed.type -join ', ')"
        }

        $duplicateIds = @(
            $interactive |
                Where-Object {
                    $_.PSObject.Properties['automationId'] -and
                    -not [string]::IsNullOrWhiteSpace($_.automationId)
                } |
                Group-Object -Property automationId |
                Where-Object { $_.Count -gt 1 }
        )
        if ($duplicateIds.Count -gt 0) {
            throw "Duplicate AutomationId values: $($duplicateIds.Name -join ', ')"
        }

        $script:pass++
        $script:results += @{ name = "$State accessibility IDs"; status = 'PASS' }
    }
    catch {
        $script:fail++
        $script:results += @{
            name = "$State accessibility IDs"
            status = 'FAIL'
            detail = "$_"
        }
    }
}

$cancelInputPath = Join-Path $resultsDirectory 'cancel.pdf'
$cancelOutputPath = Join-Path $resultsDirectory 'cancel_spread.pdf'
$corruptInputPath = Join-Path $resultsDirectory 'corrupt.pdf'
$protectedInputPath = Join-Path $resultsDirectory 'protected.pdf'
$protectedOutputPath = Join-Path $resultsDirectory 'protected_spread.pdf'
Write-PdfFixture -Path $inputPath -PageCount 2
Write-PdfFixture -Path $cancelInputPath -PageCount 5000
[System.IO.File]::WriteAllText(
    $corruptInputPath,
    'This deliberately is not a PDF.',
    [System.Text.Encoding]::ASCII
)
$protectedFixture = Join-Path $PSScriptRoot 'fixtures\protected-secret.pdf.base64'
[System.IO.File]::WriteAllBytes(
    $protectedInputPath,
    [Convert]::FromBase64String(
        (Get-Content -LiteralPath $protectedFixture -Raw).Trim()
    )
)
$windows = winapp ui list-windows -a $AppPid --json 2>$null |
    ConvertFrom-Json
$mainWindow = $windows |
    Where-Object { $_.title -ne 'PopupHost' } |
    Select-Object -First 1
if (-not $mainWindow) {
    throw "No TateYoko window was found for PID $AppPid."
}
$script:mainHwnd = $mainWindow.hwnd

Test-UI 'Idle choose button exists' {
    winapp ui wait-for 'ChoosePdfButton' -w $script:mainHwnd -t 3000
}
Test-UI 'Ctrl+O opens the picker' {
    winapp ui send-keys 'ctrl+o' -w $script:mainHwnd --via send-input
    $script:pickerHwnd = Wait-PickerWindow
}
Test-UI 'Picker can be cancelled without changing state' {
    Invoke-PickerButton -AutomationId '2'
    winapp ui wait-for 'ChoosePdfButton' -w $script:mainHwnd -t 3000
}
Save-Screenshot -Name '01-idle.png'
Test-Accessibility -State 'Idle'

Test-UI 'Choose PDF opens the picker' {
    Select-PdfThroughPicker `
        -ButtonId 'ChoosePdfButton' `
        -Path $inputPath `
        -ExpectedFileName 'input.pdf'
}
Test-UI 'Ready controls are wired' {
    winapp ui wait-for 'OpeningModeRadioButtons' -w $script:mainHwnd -t 3000
    winapp ui wait-for 'ConvertButton' -w $script:mainHwnd -t 3000
    winapp ui wait-for `
        'ConvertButton' `
        -w $script:mainHwnd `
        -p HasKeyboardFocus `
        --value 'True' `
        -t 3000
}
Test-UI 'Cover mode can be selected' {
    winapp ui invoke 'OpeningCover' -w $script:mainHwnd
    $selection = winapp ui get-property `
        'OpeningCover' `
        -w $script:mainHwnd `
        -p IsSelected `
        --json |
        ConvertFrom-Json
    if ($selection.properties.IsSelected -ne 'True') {
        throw 'Cover mode was not selected.'
    }
}
Test-UI 'Save picker can be cancelled safely' {
    winapp ui invoke 'ConvertButton' -w $script:mainHwnd
    $script:pickerHwnd = Wait-PickerWindow
    $fileNameSelector = Get-PickerFileNameSelector
    winapp ui wait-for `
        $fileNameSelector `
        -w $script:pickerHwnd `
        --value 'input_spread.pdf' `
        -t 3000
    Invoke-PickerButton -AutomationId '2'
    winapp ui wait-for 'ConvertButton' -w $script:mainHwnd -t 3000
}
Save-Screenshot -Name '02-ready-cover.png'
Test-Accessibility -State 'Ready'

Test-UI 'Explicit picker destination converts and reaches Done' {
    Select-ConversionOutput `
        -ButtonId 'ConvertButton' `
        -Path $outputPath `
        -ExpectedSuggestion 'input_spread.pdf'
    winapp ui wait-for 'OpenOutputButton' -w $script:mainHwnd -t 15000
    winapp ui wait-for `
        'OpenOutputButton' `
        -w $script:mainHwnd `
        -p HasKeyboardFocus `
        --value 'True' `
        -t 3000
}
Test-UI 'Output was committed at the explicitly selected path' {
    if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
        throw "Expected output was not created: $outputPath"
    }
}
Test-UI 'Done actions are wired' {
    winapp ui wait-for `
        'OpenOutputButton' `
        -w $script:mainHwnd `
        -p IsEnabled `
        --value 'True' `
        -t 3000
    winapp ui wait-for `
        'ShowFolderButton' `
        -w $script:mainHwnd `
        -p IsEnabled `
        --value 'True' `
        -t 3000
    winapp ui wait-for `
        'AnotherPdfButton' `
        -w $script:mainHwnd `
        -p IsEnabled `
        --value 'True' `
        -t 3000
}
Save-Screenshot -Name '03-done.png'
Test-Accessibility -State 'Done'

Test-UI 'Convert another opens the picker' {
    winapp ui invoke 'AnotherPdfButton' -w $script:mainHwnd
    $script:pickerHwnd = Wait-PickerWindow
}
Save-Screenshot -Name '04-another-picker.png'

Test-UI 'Large PDF can be selected from the another-PDF flow' {
    Complete-PdfSelection `
        -Path $cancelInputPath `
        -ExpectedFileName 'cancel.pdf'
}
Test-UI 'Active conversion can be cancelled without output' {
    Select-ConversionOutput `
        -ButtonId 'ConvertButton' `
        -Path $cancelOutputPath `
        -ExpectedSuggestion 'cancel_spread.pdf'
    winapp ui wait-for 'CancelButton' -w $script:mainHwnd -t 3000
    winapp ui invoke 'CancelButton' -w $script:mainHwnd
    winapp ui wait-for 'ConvertButton' -w $script:mainHwnd -t 15000
    winapp ui wait-for `
        'ConvertButton' `
        -w $script:mainHwnd `
        -p HasKeyboardFocus `
        --value 'True' `
        -t 3000
    if (Test-Path -LiteralPath $cancelOutputPath) {
        throw "Cancelled conversion committed an output: $cancelOutputPath"
    }
}
Save-Screenshot -Name '05-cancelled-ready.png'

Test-UI 'Corrupt PDF reaches an actionable Error state' {
    Select-PdfThroughPicker `
        -ButtonId 'PickAnotherButton' `
        -Path $corruptInputPath `
        -ExpectedFileName 'corrupt.pdf'
    Select-ConversionOutput `
        -ButtonId 'ConvertButton' `
        -Path (Join-Path $resultsDirectory 'corrupt_spread.pdf') `
        -ExpectedSuggestion 'corrupt_spread.pdf'
    winapp ui wait-for 'ErrorInfo' -w $script:mainHwnd -t 10000
    winapp ui wait-for 'RetryButton' -w $script:mainHwnd --gone -t 3000
    winapp ui wait-for `
        'ErrorChooseAnotherButton' `
        -w $script:mainHwnd `
        -p IsEnabled `
        --value 'True' `
        -t 3000
    winapp ui wait-for `
        'ErrorChooseAnotherButton' `
        -w $script:mainHwnd `
        -p HasKeyboardFocus `
        --value 'True' `
        -t 3000
}
Save-Screenshot -Name '06-corrupt-error.png'
Test-Accessibility -State 'Error'

Test-UI 'Protected PDF reaches Password state' {
    Select-PdfThroughPicker `
        -ButtonId 'ErrorChooseAnotherButton' `
        -Path $protectedInputPath `
        -ExpectedFileName 'protected.pdf'
    Select-ConversionOutput `
        -ButtonId 'ConvertButton' `
        -Path $protectedOutputPath `
        -ExpectedSuggestion 'protected_spread.pdf'
    winapp ui wait-for 'PasswordInput' -w $script:mainHwnd -t 10000
    winapp ui wait-for `
        'PasswordInput' `
        -w $script:mainHwnd `
        -p HasKeyboardFocus `
        --value 'True' `
        -t 3000
}
Test-UI 'Wrong password stays retryable' {
    winapp ui send-keys `
        'wrong' `
        --target 'PasswordInput' `
        -w $script:mainHwnd `
        --via send-input
    winapp ui invoke 'RetryPasswordButton' -w $script:mainHwnd
    winapp ui wait-for 'PasswordInput' -w $script:mainHwnd -t 10000
}
Save-Screenshot -Name '07-wrong-password.png'
Test-Accessibility -State 'Password'

Test-UI 'Correct password completes and preserves an output' {
    winapp ui send-keys `
        'secret' `
        --target 'PasswordInput' `
        -w $script:mainHwnd `
        --via send-input
    winapp ui invoke 'RetryPasswordButton' -w $script:mainHwnd
    winapp ui wait-for 'OpenOutputButton' -w $script:mainHwnd -t 15000
    if (-not (Test-Path -LiteralPath $protectedOutputPath -PathType Leaf)) {
        throw "Protected output was not created: $protectedOutputPath"
    }
}
Save-Screenshot -Name '08-protected-done.png'

$summary = @{
    appPid = $AppPid
    failed = $script:fail
    passed = $script:pass
    results = $script:results
    runDirectory = $resultsDirectory
}
$summary |
    ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath $resultsPath -Encoding utf8

Write-Output "`nPassed: $($script:pass) | Failed: $($script:fail)"
Write-Output "Evidence: $resultsDirectory"
$script:results |
    Where-Object { $_.status -eq 'FAIL' } |
    ForEach-Object {
        Write-Output "  FAIL: $($_.name) - $($_.detail)"
    }

if ($script:fail -gt 0) {
    exit 1
}

exit 0
