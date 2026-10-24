$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$app=Join-Path $root 'app'
$tools=Join-Path $app 'tools'
New-Item -ItemType Directory -Force $tools | Out-Null
$version=(Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'VERSION must contain major.minor.patch'}
$versionInfo=Join-Path $app 'VersionInfo.g.cs'
@"
[assembly: System.Reflection.AssemblyVersion("$version.0")]
[assembly: System.Reflection.AssemblyFileVersion("$version.0")]
"@ | Set-Content -LiteralPath $versionInfo -Encoding UTF8
[xml]$manifest=Get-Content -LiteralPath (Join-Path $root 'src/app.manifest') -Raw
$manifest.assembly.assemblyIdentity.SetAttribute('version', "$version.0")
$manifestPath=Join-Path $app 'app.manifest'
$manifest.Save($manifestPath)
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /platform:x86 /optimize+ /utf8output /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Xml.dll "/win32manifest:$manifestPath" "/win32icon:$root\assets\subforge.ico" "/out:$app\SupCraft.exe" "$versionInfo" "$root\src\Program.cs" "$root\src\ConversionSupport.cs" "$root\src\WindowsUi.cs" "$root\src\WindowChrome.cs" "$root\src\UiScaling.cs" "$root\src\Appearance.cs" "$root\src\CleanNumeric.cs" "$root\src\AssFonts.cs" "$root\src\FontManager.cs"
if($LASTEXITCODE -ne 0){throw 'Compilation failed'}
Copy-Item "$root\third_party\avs2bdnxml\avs2bdnxml.exe" "$tools\avs2bdnxml.exe" -Force
$filterSource="$root\third_party\VSFilterMod\S-VSFilterMod.dll"
if(Test-Path $filterSource){Copy-Item $filterSource "$tools\S-VSFilterMod.dll" -Force}
else{throw 'third_party/VSFilterMod/S-VSFilterMod.dll is missing'}
Copy-Item "$root\third_party\avs2bdnxml\COPYING" "$tools\COPYING-avs2bdnxml-GPLv3.txt" -Force
$modern=Join-Path $tools 'modern'
$super=Join-Path $tools 'super'
New-Item -ItemType Directory -Force $modern,$super | Out-Null
Copy-Item "$root\third_party\ass2bdnxml-v0.7h\ass2bdnxml_v07h\*" $modern -Recurse -Force
# The upstream v0.7h Windows archive ships libunibreak-6.dll while its
# executable imports libunibreak-7.dll.  Both names refer to the compatible
# DLL bundled in that archive, so keep an alias in every generated package.
Copy-Item (Join-Path $modern 'libunibreak-6.dll') (Join-Path $modern 'libunibreak-7.dll') -Force
Copy-Item "$root\third_party\SUPer-v0.2.8\CLI_win_x64_SUPer\*" $super -Recurse -Force
$notices=Join-Path $tools 'licenses'
$noticePath=[IO.Path]::GetFullPath($notices)
if([IO.Path]::GetDirectoryName($noticePath) -ne [IO.Path]::GetFullPath($tools) -or [IO.Path]::GetFileName($noticePath) -ne 'licenses'){throw 'Invalid generated notice directory'}
# Recreate generated notices so removed duplicate aliases do not survive rebuilds.
if(Test-Path -LiteralPath $noticePath){Remove-Item -LiteralPath $noticePath -Recurse -Force}
Copy-Item -LiteralPath (Join-Path $root 'legal/licenses') -Destination $tools -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'legal/source-index.json') -Destination $notices -Force
Copy-Item -LiteralPath (Join-Path $root 'legal/license-index.json') -Destination $notices -Force
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $notices 'SupCraft-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $root 'legal/SUPCRAFT-0BSD.txt') -Destination (Join-Path $notices 'SupCraft-runtime-0BSD.txt') -Force
$noticeText=[IO.File]::ReadAllText((Join-Path $root 'THIRD-PARTY-NOTICES.md'))
$noticeText=$noticeText.Replace('(legal/SUPCRAFT-0BSD.txt)','(SupCraft-runtime-0BSD.txt)').Replace('(legal/licenses/','(').Replace('(legal/','(https://github.com/Enfana5780/SupCraft/tree/main/legal/').Replace('(third_party/','(https://github.com/Enfana5780/SupCraft/tree/main/third_party/').Replace('(LICENSE)','(SupCraft-LICENSE.txt)')
[IO.File]::WriteAllText((Join-Path $notices 'THIRD-PARTY-NOTICES.md'),$noticeText,(New-Object Text.UTF8Encoding($false)))
'Third-party source and build materials: https://github.com/Enfana5780/SupCraft/tree/main/legal' | Set-Content -LiteralPath (Join-Path $notices 'SOURCE-LOCATION.txt') -Encoding UTF8
Write-Output "Built $app\SupCraft.exe"
