param(
    [switch]$RequireExactTag
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$manifestPath = Join-Path $repositoryRoot "NVConso/app.manifest"
$programPath = Join-Path $repositoryRoot "NVConso/Program.cs"
$sourceRoot = Join-Path $repositoryRoot "NVConso"
$allowedRunasFile = Join-Path $sourceRoot "WindowsPrivilegeService.cs"

Push-Location $repositoryRoot
try {
    Write-Host "git rev-parse HEAD"
    git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) {
        throw "Impossible de lire la révision Git courante."
    }

    Write-Host "git describe --tags --exact-match"
    $exactTag = git describe --tags --exact-match 2>$null
    $describeExitCode = $LASTEXITCODE
    if ($describeExitCode -eq 0) {
        Write-Host $exactTag
    } else {
        if ($RequireExactTag) {
            throw "La révision courante ne correspond pas exactement à un tag."
        }

        Write-Host "Aucun tag exact pour cette révision non publiée."
        $global:LASTEXITCODE = 0
    }

    Write-Host "Contenu de NVConso/app.manifest"
    Get-Content -Path $manifestPath

    Write-Host 'Recherche requireAdministrator dans NVConso/app.manifest'
    $requireAdministratorMatches = @(Select-String -Path $manifestPath -Pattern "requireAdministrator")
    if ($requireAdministratorMatches.Count -gt 0) {
        $requireAdministratorMatches | ForEach-Object { Write-Host $_ }
        throw "Le manifeste ne doit pas demander requireAdministrator."
    }

    Write-Host "Aucun résultat."

    Write-Host 'Recherche Verb = "runas" dans NVConso/Program.cs'
    $programRunasMatches = @(Select-String -Path $programPath -Pattern 'Verb\s*=\s*"runas"')
    if ($programRunasMatches.Count -gt 0) {
        $programRunasMatches | ForEach-Object { Write-Host $_ }
        throw 'Program.cs ne doit pas relancer automatiquement l''application avec Verb = "runas".'
    }

    Write-Host "Aucun résultat."

    Write-Host 'Recherche globale des occurrences autorisées de Verb = "runas"'
    $runasMatches = @(
        Get-ChildItem -Path $sourceRoot -Filter "*.cs" -Recurse |
            Where-Object {
                $_.FullName -notmatch "\\bin\\" -and
                $_.FullName -notmatch "\\obj\\"
            } |
            Select-String -Pattern 'Verb\s*=\s*"runas"'
    )

    $unauthorizedMatches = @(
        $runasMatches |
            Where-Object {
                -not [string]::Equals($_.Path, $allowedRunasFile, [System.StringComparison]::OrdinalIgnoreCase)
            }
    )

    if ($unauthorizedMatches.Count -gt 0) {
        $unauthorizedMatches | ForEach-Object { Write-Host $_ }
        throw 'Verb = "runas" est autorisé uniquement dans WindowsPrivilegeService.'
    }

    if ($runasMatches.Count -eq 0) {
        Write-Host 'Aucune occurrence de Verb = "runas" trouvée.'
    } else {
        $runasMatches | ForEach-Object { Write-Host $_ }
    }
}
finally {
    Pop-Location
}
