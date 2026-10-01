$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    & (Join-Path $projectRoot 'build.ps1')
    & python (Join-Path $PSScriptRoot 'prepare-fixture.py')
    if($LASTEXITCODE -ne 0){throw 'Fixture preparation failed'}
    $qa=Join-Path $projectRoot 'qa'
    New-Item -ItemType Directory -Path $qa -Force | Out-Null
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
    foreach($test in 'SupportTests','ParserTests','ConversionTests') {
        & $compiler /nologo /target:exe /platform:x86 /utf8output /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Xml.dll "/main:$test" "/out:$qa\$test.exe" "$projectRoot\app\VersionInfo.g.cs" "$projectRoot\src\Program.cs" "$projectRoot\src\ConversionSupport.cs" "$PSScriptRoot\$test.cs"
        if($LASTEXITCODE -ne 0){throw "Compile failed: $test"}
    }
    & "$qa/SupportTests.exe" "$qa/support"
    if($LASTEXITCODE -ne 0){throw 'Support checks failed'}
    & "$qa/ParserTests.exe" "$qa/parser"
    if($LASTEXITCODE -ne 0){throw 'Parser checks failed'}
    & "$qa/ConversionTests.exe" $projectRoot
    if($LASTEXITCODE -ne 0){throw 'Conversion checks failed'}
    $x86=Join-Path $env:WINDIR 'SysWOW64/WindowsPowerShell/v1.0/powershell.exe'
    foreach($size in 9,14) {
        & $x86 -NoProfile -ExecutionPolicy Bypass -File "$projectRoot/ui-layout-check.ps1" -FontSize $size
        if($LASTEXITCODE -ne 0){throw "Layout checks failed at font size $size"}
    }
} finally {Pop-Location}
