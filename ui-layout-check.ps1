param([float]$FontSize=9)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$assembly=[Reflection.Assembly]::LoadFile((Join-Path $PSScriptRoot 'app\SupCraft.exe'))
$type=$assembly.GetType('StandaloneAssRocket.MainForm')
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$form=[Activator]::CreateInstance($type)
$form.Font=New-Object Drawing.Font('Microsoft YaHei UI',$FontSize)
$form.Show()
try {
  $engine=$type.GetField('engine',$flags).GetValue($form)
  $resolution=$type.GetField('resolution',$flags).GetValue($form)
  $format=$type.GetField('format',$flags).GetValue($form)
  $engine.SelectedIndex=1
  $resolution.SelectedIndex=3
  $form.PerformLayout()
  [Windows.Forms.Application]::DoEvents()
  $panels=@($form.Controls[0].Controls | Where-Object {$_ -is [Windows.Forms.GroupBox]})
  $parameters=$engine.Parent.Parent
  foreach($width in 1040,1180,1320,1480,1580,1920) {
    $form.Size=New-Object Drawing.Size($width,840)
    $form.PerformLayout()
    [Windows.Forms.Application]::DoEvents()
    $groups=@($parameters.Controls)
    foreach($group in $groups) {
      if($group.Left -lt 0 -or $group.Right -gt $parameters.ClientSize.Width -or $group.Top -lt 0 -or $group.Bottom -gt $parameters.ClientSize.Height){throw "Clipped group at width $width group=$($group.Bounds) panel=$($parameters.ClientSize)"}
      foreach($control in $group.Controls){if($control.Right -gt $group.Width -or $control.Bottom -gt $group.Height){throw "Clipped field at width $width : $($control.Text) $($control.Bounds) group $($group.Bounds)"}}
    }
    for($i=0;$i -lt $groups.Count;$i++){for($j=$i+1;$j -lt $groups.Count;$j++){if($groups[$i].Bounds.IntersectsWith($groups[$j].Bounds)){throw "Overlap at width $width"}}}
    foreach($name in 'engine','resolution','fps','format'){
      $combo=$type.GetField($name,$flags).GetValue($form)
      foreach($item in $combo.Items){$required=[Windows.Forms.TextRenderer]::MeasureText($item,$combo.Font).Width+[Windows.Forms.SystemInformation]::VerticalScrollBarWidth+16;if($combo.Width -lt $required -or $combo.DropDownWidth -lt $required){throw "Clipped option $item"}}
    }
    $rows=@($groups | Group-Object { [int]([Math]::Round(($_.Top+$_.Height/2.0)/5.0)) }).Count
    "PASS width=$width; rows=$rows; all groups and option text fit"
    if($width -in 1040,1580){$bitmap=New-Object Drawing.Bitmap($form.Width,$form.Height);try{$form.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,$bitmap.Width,$bitmap.Height)));$bitmap.Save((Join-Path $PSScriptRoot "ui-reviewed-$width.png"),[Drawing.Imaging.ImageFormat]::Png)}finally{$bitmap.Dispose()}}
  }
} finally {$form.Close();$form.Dispose()}
