param([float]$FontSize=9)
$ErrorActionPreference='Stop'
$Dpi=if($env:SUPCRAFT_TEST_DPI){[int]$env:SUPCRAFT_TEST_DPI}else{96}
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class UiLayoutDpi {
    [DllImport("user32.dll")]public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
'@
$previousDpi=[UiLayoutDpi]::SetThreadDpiAwarenessContext([IntPtr](-4))
[Windows.Forms.Application]::EnableVisualStyles()
$assembly=[Reflection.Assembly]::LoadFile((Join-Path $PSScriptRoot 'app\SupCraft.exe'))
$type=$assembly.GetType('StandaloneAssRocket.MainForm')
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$form=[Activator]::CreateInstance($type)
$form.Font=New-Object Drawing.Font('Microsoft YaHei UI',$FontSize)
$form.Show()
[Windows.Forms.Application]::DoEvents();[Windows.Forms.Application]::DoEvents()
$type.GetMethod('ApplyUiScale',$flags).Invoke($form,@($Dpi,$null,$false)) | Out-Null
function Field($name){$type.GetField($name,$flags).GetValue($form)}
function Check-Children($parent){
  foreach($control in $parent.Controls){
    if(-not $control.Visible){continue}
    if($control.Left -lt 0 -or $control.Top -lt 0 -or $control.Right -gt $parent.ClientSize.Width -or $control.Bottom -gt $parent.ClientSize.Height){throw "Clipped $($control.Bounds) within $($parent.ClientSize) $($control.GetType().Name) '$($control.Text)' in $($parent.GetType().Name)"}
    Check-Children $control
  }
}
try {
  foreach($width in 1040,1180,1280,1320,1480,1520,1920){
    $form.Size=New-Object Drawing.Size([int]($width*$Dpi/96),[int](840*$Dpi/96))
    foreach($engine in 0,1){
      (Field engine).SelectedIndex=$engine
      $indices=if($engine -eq 0){0,1,2,3,4}else{0,1,2,3}
      foreach($index in $indices){
        (Field resolution).SelectedIndex=$index
        $form.PerformLayout();[Windows.Forms.Application]::DoEvents();[Windows.Forms.Application]::DoEvents()
        $left=Field filesCard;$right=Field settingsCard;$logs=Field logsCard;$content=Field settingsContent
        foreach($panel in @($left,$right,$logs)){if($panel.Left -lt 0 -or $panel.Top -lt 0 -or $panel.Right -gt $form.ClientSize.Width -or $panel.Bottom -gt $form.ClientSize.Height){throw 'Panel is outside the window'}}
        if($right.Width -ne [Math]::Round(579*$Dpi/96) -or $right.Height -ne [Math]::Round(552*$Dpi/96) -or $left.Height -ne $right.Height){throw "Fixed settings mismatch: dpi=$Dpi size=$($right.Size) left=$($left.Size) scale=$($type.GetProperty('UiScale',$flags).GetValue($form,$null))"}
        if($left.Right -ge $right.Left -or $left.Top -ne $right.Top -or $left.Bottom -ne $right.Bottom -or $logs.Top -le $left.Bottom){throw 'Incorrect left/right/log arrangement'}
        Check-Children $left
        Check-Children $content
        $status=Field statusPanel;$actions=Field settingsActions;$viewport=Field settingsViewport
        if($status.Parent -ne $right -or $status.Bottom -ge $actions.Top -or $viewport.Bottom -ge $status.Top){throw 'Progress is not between settings viewport and conversion actions'}
        Check-Children $status
        Check-Children (Field settingsActions)
        Check-Children $logs
        $children=@($content.Controls | Where-Object {$_.Visible})
        for($i=0;$i -lt $children.Count;$i++){for($j=$i+1;$j -lt $children.Count;$j++){if($children[$i].Bounds.IntersectsWith($children[$j].Bounds)){throw 'Overlapping settings'}}}
        if((Field optionsPanel).Visible -ne ($engine -eq 0) -or (Field modernPanel).Visible -ne ($engine -eq 1)){throw 'Incorrect engine-specific option visibility'}
        if((Field customSizeGroup).Visible -ne ($engine -eq 0 -and $index -eq 4)){throw 'Incorrect custom field visibility'}
        if((Field resolutionWarning).Visible -ne ($engine -eq 0 -and $index -ge 2)){throw 'Incorrect nonstandard warning visibility'}
        if((Field elapsedLabel).Right -gt (Field statusPanel).ClientSize.Width){throw 'Clipped elapsed time'}
        if($width -in 1040,1280 -and $engine -eq 0 -and $index -eq 4){
          $bitmap=New-Object Drawing.Bitmap($form.Width,$form.Height)
          try{$form.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,$bitmap.Width,$bitmap.Height)));$bitmap.Save((Join-Path $PSScriptRoot "qa\modern-ui-$Dpi-$FontSize-$width.png"),[Drawing.Imaging.ImageFormat]::Png)}finally{$bitmap.Dispose()}
        }
      }
    }
    "PASS modern layout: dpi=$Dpi font=$FontSize logical-width=$width; both engines, all presets, custom Beta and warning"
  }
} finally {$form.Close();$form.Dispose();if($previousDpi -ne [IntPtr]::Zero){[UiLayoutDpi]::SetThreadDpiAwarenessContext($previousDpi) | Out-Null}}
