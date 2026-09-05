[CmdletBinding()]
param(
    [string] $RepositoriesRoot = (Split-Path $PSScriptRoot -Parent | Split-Path -Parent),
    [switch] $VerifyOnly
)
$ErrorActionPreference = 'Stop'
$owner = Split-Path $PSScriptRoot -Parent
$source = Join-Path $owner 'extensions/video-game'
$names = @('ArtDirector.VideoGame','Artist.VideoGame','AudioDesigner.VideoGame','BuildReleaseEngineer.VideoGame',
    'CreativeDirector.VideoGame','Engineer.VideoGame','GameDesigner','LevelDesigner.VideoGame','NarrativeDesigner.VideoGame',
    'PlaytestResearcher.VideoGame','Producer.VideoGame','QA.VideoGame','TechnicalArtist.VideoGame','TechnicalDirector.VideoGame','UiUxAccessibilityDesigner.VideoGame')
$files = @('VideoGameContracts.cs','VideoGameAgentKit.cs','README.md')
$hashes = [ordered]@{}
foreach ($file in $files) { $hashes[$file] = (Get-FileHash (Join-Path $source $file) -Algorithm SHA256).Hash.ToLowerInvariant() }
$provenance = [ordered]@{
    extensionId = 'com.crosswiredstudios.video-game'
    version = '1.0.0'
    sourceRepository = 'https://github.com/CrosswiredStudios/CSweet.Agent.CreativeDirector.VideoGame'
    sourcePath = 'extensions/video-game'
    files = $hashes
} | ConvertTo-Json -Depth 5
foreach ($name in $names) {
    $repository = Join-Path $RepositoriesRoot "CSweet.Agent.$name"
    if (!(Test-Path (Join-Path $repository 'csweet-plugin.json'))) { throw "Missing consumer repository: $repository" }
    $target = Join-Path $repository 'extensions/video-game'
    if (!$VerifyOnly) {
        New-Item -ItemType Directory -Force $target | Out-Null
        if ([IO.Path]::GetFullPath($target) -ne [IO.Path]::GetFullPath($source)) {
            foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $target $file) }
        }
        [IO.File]::WriteAllText((Join-Path $target 'extension.json'), $provenance)
    }
    foreach ($file in $files) {
        if ((Get-FileHash (Join-Path $target $file) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hashes[$file]) {
            throw "Extension snapshot differs: $name/$file"
        }
    }
    if ((Get-Content (Join-Path $target 'extension.json') -Raw).Trim() -ne $provenance.Trim()) { throw "Extension provenance differs: $name" }
}
Write-Host "Verified $($names.Count) extension snapshots."
