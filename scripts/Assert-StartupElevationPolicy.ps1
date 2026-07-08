param(
    [switch]$RequireExactTag
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$manifestPath = Join-Path $repositoryRoot "NVConso/app.manifest"
$programPath = Join-Path $repositoryRoot "NVConso/Program.cs"
$startupManagerPath = Join-Path $repositoryRoot "NVConso/WindowsTaskSchedulerStartupManager.cs"
$sourceRoot = Join-Path $repositoryRoot "NVConso"
$viewsRoot = Join-Path $sourceRoot "Views"
$releaseWorkflowPath = Join-Path $repositoryRoot ".github/workflows/release.yml"
$allowedRunasFile = Join-Path $sourceRoot "WindowsPrivilegeService.cs"
$allowedMainWindow = "NVConso.Views.WattPilotWindow"

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

    Write-Host "Recherche de tâche de démarrage élevée dans NVConso/WindowsTaskSchedulerStartupManager.cs"
    $highestPrivilegeStartupMatches = @(Select-String -Path $startupManagerPath -Pattern 'runWithHighestPrivileges:\s*true')
    if ($highestPrivilegeStartupMatches.Count -gt 0) {
        $highestPrivilegeStartupMatches | ForEach-Object { Write-Host $_ }
        throw "La tâche de démarrage WattPilot ne doit pas demander les privilèges les plus élevés."
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

    Write-Host "Recherche des fenêtres WPF principales"
    $windowDeclarations = @(
        Get-ChildItem -Path $viewsRoot -Filter "*.xaml" -Recurse |
            Where-Object {
                $_.FullName -notmatch "\\bin\\" -and
                $_.FullName -notmatch "\\obj\\"
            } |
            Select-String -Pattern '^\s*<Window\s+x:Class="([^"]+)"'
    )

    $unexpectedWindows = @(
        $windowDeclarations |
            Where-Object { $_.Matches[0].Groups[1].Value -ne $allowedMainWindow }
    )

    if ($windowDeclarations.Count -ne 1 -or $unexpectedWindows.Count -gt 0) {
        $windowDeclarations | ForEach-Object { Write-Host $_ }
        throw "WattPilot doit conserver une seule fenêtre principale WPF : $allowedMainWindow."
    }

    $windowDeclarations | ForEach-Object { Write-Host $_ }

    Write-Host "Recherche de TabControl dans la fenêtre principale"
    $tabControlMatches = @(Select-String -Path (Join-Path $viewsRoot "WattPilotWindow.xaml") -Pattern '<\s*Tab(Control|Item)\b')
    if ($tabControlMatches.Count -gt 0) {
        $tabControlMatches | ForEach-Object { Write-Host $_ }
        throw "Le panneau Paramètres intégré ne doit pas réintroduire de TabControl."
    }

    Write-Host "Aucun résultat."

    Write-Host "Recherche d'assets publics principaux NVConso dans le workflow de release"
    $legacyAssetMatches = @(Select-String -Path $releaseWorkflowPath -Pattern '^\s*(SETUP_EXE_NAME|PORTABLE_ZIP_NAME|MAIN_EXE_NAME|VELOPACK_PACK_ID):\s*NVConso\b|NVConso-(Setup|win-x64)')
    if ($legacyAssetMatches.Count -gt 0) {
        $legacyAssetMatches | ForEach-Object { Write-Host $_ }
        throw "Les assets publics principaux ne doivent pas reprendre le nom NVConso."
    }

    Write-Host "Aucun résultat."
}
finally {
    Pop-Location
}
