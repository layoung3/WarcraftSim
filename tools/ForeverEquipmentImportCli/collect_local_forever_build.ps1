# CP173 read-only Forever client provenance inventory.
# Run in PowerShell on your own Windows computer, once for the older beta and
# once for the newer preinstall. It DOES NOT edit, launch or decompress the game.
# It collects build/manifest metadata, not login details, characters or assets.
param(
    [Parameter(Mandatory=$true)][string]$InstallDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $InstallDirectory).Path
if (-not (Test-Path -LiteralPath $OutputDirectory)) {
    New-Item -Path $OutputDirectory -ItemType Directory | Out-Null
}
$buildFile = Join-Path $root '.build.info'
$manifest = @()
foreach ($rel in @('.build.info','.product.db','Data\.build.info',
                    'Data\config','Data\indices','Data\data',
                    '_classic_','_classic_beta_','_classic_ptr_',
                    'WowClassic.exe','WowClassicBeta.exe','Wow.exe')) {
    $path = Join-Path $root $rel
    if (Test-Path -LiteralPath $path) {
        $item = Get-Item -LiteralPath $path
        $manifest += [ordered]@{
            relativePath = $rel
            kind = $(if($item.PSIsContainer){'directory'}else{'file'})
            bytes = $(if($item.PSIsContainer){$null}else{$item.Length})
        }
    }
}
$build = $null
if (Test-Path -LiteralPath $buildFile -PathType Leaf) {
    $build = Get-Content -LiteralPath $buildFile -Raw
}
$exe = Get-ChildItem -LiteralPath $root -Filter '*.exe' -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^(Wow|World of Warcraft)' } |
    Select-Object -First 12 -Property Name,Length,
        @{Name='FileVersion';Expression={$_.VersionInfo.FileVersion}},
        @{Name='ProductVersion';Expression={$_.VersionInfo.ProductVersion}}
$summary = [ordered]@{
    collectionFormat = 'warcraftsim-cp173-local-client-read-only-v1'
    collectedAtUtc = [DateTime]::UtcNow.ToString('o')
    files = $manifest
    executableVersions = @($exe)
    buildInfoPresent = ($null -ne $build)
    buildInfo = $build
    notes = @(
        'No CASC content extracted, no credentials read.',
        'Directory paths and account usernames are deliberately excluded.'
    )
}
$out = Join-Path $OutputDirectory 'forever-client-build-inventory.json'
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $out -Encoding UTF8
Write-Host "Read-only inventory written: $out"
Write-Host 'Review the file before sharing. Do not send logs, account data, or the game installation.'
