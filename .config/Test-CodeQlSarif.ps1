[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $Path
)

$ErrorActionPreference = 'Stop'
$sarifFiles = @(
    Get-ChildItem -LiteralPath $Path -Filter '*.sarif' -File -Recurse
)
if ($sarifFiles.Count -eq 0) {
    throw "No CodeQL SARIF files were produced beneath '$Path'."
}

$violations = [Collections.Generic.List[string]]::new()
foreach ($sarifFile in $sarifFiles) {
    $document = Get-Content -LiteralPath $sarifFile.FullName -Raw |
        ConvertFrom-Json -Depth 100
    foreach ($run in @($document.runs)) {
        $retained = [Collections.Generic.List[object]]::new()
        foreach ($result in @($run.results)) {
            $uris = @(
                $result.locations |
                    ForEach-Object {
                        $_.physicalLocation.artifactLocation.uri
                    } |
                    Where-Object {
                        -not [string]::IsNullOrWhiteSpace($_)
                    }
            )
            $isGenerated = $uris.Count -gt 0 -and @(
                $uris |
                    Where-Object {
                        $normalized = $_.Replace('\', '/')
                        $normalized -match '(^|/)(bin|obj|build|publish)/'
                    }
            ).Count -eq $uris.Count
            if ($isGenerated) {
                continue
            }

            $retained.Add($result)
            if ($result.level -in @('error', 'warning')) {
                $ruleId = if ([string]::IsNullOrWhiteSpace($result.ruleId)) {
                    '<unknown-rule>'
                } else {
                    $result.ruleId
                }
                $location = if ($uris.Count -eq 0) {
                    '<no-location>'
                } else {
                    $uris[0]
                }
                $violations.Add("$ruleId at $location")
            }
        }

        $run.results = @($retained)
    }

    $json = $document | ConvertTo-Json -Depth 100
    [IO.File]::WriteAllText(
        $sarifFile.FullName,
        $json,
        [Text.UTF8Encoding]::new($false)
    )
}

if ($violations.Count -gt 0) {
    throw (
        "CodeQL reported source warnings or errors:`n" +
        ($violations | Sort-Object -Unique | ForEach-Object { "- $_" } | Out-String)
    )
}
