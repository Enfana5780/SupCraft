$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    & (Join-Path $projectRoot 'build.ps1')
    & python (Join-Path $PSScriptRoot 'prepare-fixture.py')
    if($LASTEXITCODE -ne 0){throw 'Fixture preparation failed'}
    & python (Join-Path $PSScriptRoot 'prepare-canvas-fixture.py')
    if($LASTEXITCODE -ne 0){throw 'Canvas fixture preparation failed'}
    $qa=Join-Path $projectRoot 'qa'
    New-Item -ItemType Directory -Path $qa -Force | Out-Null
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
    foreach($test in 'SupportTests','ParserTests','ConversionTests','CustomCanvasTests','UiInteractionTests','AppearanceTests','FontManagerTests','OutputDefaultsTests') {
        & $compiler /nologo /target:exe /platform:x86 /utf8output "/win32manifest:$projectRoot\app\app.manifest" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Xml.dll "/main:$test" "/out:$qa\$test.exe" "$projectRoot\app\VersionInfo.g.cs" "$projectRoot\src\Program.cs" "$projectRoot\src\ConversionSupport.cs" "$projectRoot\src\WindowsUi.cs" "$projectRoot\src\WindowChrome.cs" "$projectRoot\src\UiScaling.cs" "$projectRoot\src\Appearance.cs" "$projectRoot\src\CleanNumeric.cs" "$projectRoot\src\AssFonts.cs" "$projectRoot\src\FontManager.cs" "$PSScriptRoot\$test.cs"
        if($LASTEXITCODE -ne 0){throw "Compile failed: $test"}
    }
    & "$qa/SupportTests.exe" "$qa/support"
    if($LASTEXITCODE -ne 0){throw 'Support checks failed'}
    & "$qa/ParserTests.exe" "$qa/parser"
    if($LASTEXITCODE -ne 0){throw 'Parser checks failed'}
    & "$qa/ConversionTests.exe" $projectRoot
    if($LASTEXITCODE -ne 0){throw 'Conversion checks failed'}
    & "$qa/CustomCanvasTests.exe" $projectRoot
    if($LASTEXITCODE -ne 0){throw 'Custom canvas checks failed'}
    & "$qa/UiInteractionTests.exe"
    if($LASTEXITCODE -ne 0){throw 'UI interaction checks failed'}
    & "$qa/AppearanceTests.exe"
    if($LASTEXITCODE -ne 0){throw 'Appearance checks failed'}
    & "$qa/FontManagerTests.exe" $projectRoot
    if($LASTEXITCODE -ne 0){throw 'Font manager checks failed'}
    & "$qa/OutputDefaultsTests.exe" $projectRoot
    if($LASTEXITCODE -ne 0){throw 'Default output checks failed'}
    $x86=Join-Path $env:WINDIR 'SysWOW64/WindowsPowerShell/v1.0/powershell.exe'
    foreach($size in 9,14) {
        & $x86 -NoProfile -ExecutionPolicy Bypass -File "$projectRoot/ui-layout-check.ps1" -FontSize $size
        if($LASTEXITCODE -ne 0){throw "Layout checks failed at font size $size"}
    }
    foreach($dpi in 144,192) {
        $env:SUPCRAFT_TEST_DPI=$dpi
        & $x86 -NoProfile -ExecutionPolicy Bypass -File "$projectRoot/ui-layout-check.ps1"
        if($LASTEXITCODE -ne 0){throw "Layout checks failed at DPI $dpi"}
    }
} finally {Remove-Item Env:SUPCRAFT_TEST_DPI -ErrorAction SilentlyContinue;Pop-Location}
