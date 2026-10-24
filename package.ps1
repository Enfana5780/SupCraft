param([string]$Suffix='')
$ErrorActionPreference='Stop'
if($Suffix -and $Suffix -notmatch '^[a-zA-Z0-9-]+$'){throw 'Invalid package suffix'}
& (Join-Path $PSScriptRoot 'build.ps1')
$version=(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
$release="SupCraft-v$version"
if($Suffix){$release+='-'+$Suffix}
$parent=Split-Path $PSScriptRoot -Parent
$stage=Join-Path $PSScriptRoot ('package-stage-'+[guid]::NewGuid().ToString('N'))
try {
    $runtime=Join-Path $stage $release
    $app=$runtime
    New-Item -ItemType Directory -Path $app -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'app/tools') -Destination $app -Recurse
    Get-ChildItem -LiteralPath (Join-Path $app 'tools') -Filter 'subforge-config.lock' -Recurse | Remove-Item -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'app/SupCraft.exe') -Destination $app
    $payload=@(Get-ChildItem -LiteralPath $app | ForEach-Object {$_.FullName})
    Compress-Archive -LiteralPath $payload -DestinationPath (Join-Path $parent "$release.zip") -Force
    $source=Join-Path $stage 'SupCraft-source'
    New-Item -ItemType Directory -Path $source | Out-Null
    foreach($folder in 'src','third_party','assets','tests','macos','.github','legal','docs'){
        $base=Join-Path $PSScriptRoot $folder
        Get-ChildItem -LiteralPath $base -File -Recurse -Force | Where-Object {$_.FullName -notmatch '[\\/](\.git|__pycache__|bin|obj)[\\/]' -and $_.FullName -notmatch '[\\/]macos[\\/](vendor|dist)[\\/]'} | ForEach-Object {
            $target=Join-Path $source $_.FullName.Substring($PSScriptRoot.Length+1)
            New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $target
        }
    }
    Get-ChildItem -LiteralPath $PSScriptRoot -File -Force | Where-Object {$_.Extension -in '.ps1','.md' -or $_.Name -in '.gitignore','.gitattributes','LICENSE','VERSION'} | Copy-Item -Destination $source
    Compress-Archive -LiteralPath $source -DestinationPath (Join-Path $parent "$release-source.zip") -Force
    $names="$release.zip","$release-source.zip"
    $manifest=foreach($name in $names){$hash=Get-FileHash -LiteralPath (Join-Path $parent $name) -Algorithm SHA256; $hash.Hash+'  '+$name}
    $manifest | Set-Content -LiteralPath (Join-Path $parent "$release-SHA256SUMS.txt") -Encoding ASCII
} finally {
    $full=[IO.Path]::GetFullPath($stage)
    if($full.StartsWith($PSScriptRoot+[IO.Path]::DirectorySeparatorChar) -and (Test-Path -LiteralPath $full)){Remove-Item -LiteralPath $full -Recurse -Force}
}
Write-Output "Saved SupCraft v$version runtime, complete source snapshot and checksums."
