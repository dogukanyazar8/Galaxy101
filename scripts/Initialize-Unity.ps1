#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$editorRoot = 'C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor'
$templateArchive = Join-Path $editorRoot 'Data\Resources\PackageManager\ProjectTemplates\com.unity.template.urp-blank-17.2.6.tgz'
$productDirectories = @('Assets', 'Packages', 'ProjectSettings')
foreach ($directoryName in $productDirectories) {
    if (Test-Path -LiteralPath (Join-Path $projectRoot $directoryName)) {
        throw "Initialization refused: $directoryName already exists. Existing project files are preserved."
    }
}
if (-not (Test-Path -LiteralPath $templateArchive -PathType Leaf)) {
    throw "Installed Unity template not found: $templateArchive"
}

$stagingRoot = Join-Path $projectRoot ('artifacts\initialization\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
$archiveEntries = @(& tar -tf $templateArchive)
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the installed Unity template archive.' }
$selectedEntries = @($archiveEntries | Where-Object {
    $_ -match '^package/ProjectData~/(Assets|Packages|ProjectSettings)/'
})
if ($selectedEntries.Count -eq 0) { throw 'The Unity template contains no approved project directories.' }
$stagingPrefix = [IO.Path]::GetFullPath($stagingRoot).TrimEnd('\') + '\'
foreach ($entry in $selectedEntries) {
    $entryTarget = [IO.Path]::GetFullPath((Join-Path $stagingRoot $entry))
    if (-not $entryTarget.StartsWith($stagingPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe archive entry rejected: $entry"
    }
}
& tar -xf $templateArchive -C $stagingRoot @selectedEntries
if ($LASTEXITCODE -ne 0) { throw 'Unity template extraction failed; project directories were not copied.' }

$templateProject = Join-Path $stagingRoot 'package\ProjectData~'
$manifestPath = Join-Path $templateProject 'Packages\manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable
$editorManifestPath = Join-Path $editorRoot 'Data\Resources\PackageManager\Editor\manifest.json'
$editorMetadata = Get-Content -LiteralPath $editorManifestPath -Raw | ConvertFrom-Json
$testVersion = [string]$editorMetadata.packages.'com.unity.test-framework'.version
if ($testVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'The installed Editor metadata did not specify a stable Test Framework version.' }
$manifest.dependencies['com.unity.test-framework'] = $testVersion
$manifest['testables'] = @('com.okey101.core')
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM

foreach ($directoryName in $productDirectories) {
    Copy-Item -LiteralPath (Join-Path $templateProject $directoryName) -Destination (Join-Path $projectRoot $directoryName) -Recurse
}
$versionPath = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
@('m_EditorVersion: 6000.6.6f1', 'm_EditorVersionWithRevision: 6000.6.6f1 (d20c9ac777da)') |
    Set-Content -LiteralPath $versionPath -Encoding utf8NoBOM
Write-Output "Unity project initialized at $projectRoot"
Write-Output "Test Framework resolved from installed Editor metadata: $testVersion"
Write-Output "Template extraction retained at $stagingRoot"
