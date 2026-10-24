using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace StandaloneAssRocket {
static partial class UiTheme {
    public static bool IsDark{get;private set;}
    public static void UseDark(bool dark){IsDark=dark;}
    static Color Tone(int r,int g,int b,int dr,int dg,int db){return IsDark?Color.FromArgb(dr,dg,db):Color.FromArgb(r,g,b);}
    public static Color Accent{get{return Tone(62,107,232,112,150,255);}}
    public static Color AccentDeep{get{return Tone(43,79,196,159,185,255);}}
    public static Color AccentSoft{get{return Tone(237,243,255,34,48,76);}}
    public static Color AccentLine{get{return Tone(190,209,250,76,104,158);}}
    public static Color Surface{get{return Tone(255,255,255,26,32,44);}}
    public static Color Background{get{return Tone(243,246,251,15,19,27);}}
    public static Color Ink{get{return Tone(30,41,59,231,237,247);}}
    public static Color Body{get{return Tone(60,74,94,194,205,223);}}
    public static Color Muted{get{return Tone(122,135,155,143,160,185);}}
    public static Color Faint{get{return Tone(158,169,186,99,117,143);}}
    public static Color Line{get{return Tone(228,234,244,58,72,94);}}
    public static Color LineSoft{get{return Tone(239,243,249,40,51,69);}}
    public static Color Input{get{return Tone(247,249,253,31,39,53);}}
    public static Color InputDisabled{get{return Tone(242,245,250,27,34,46);}}
    public static Color InputFocus{get{return Tone(255,255,255,36,46,64);}}
    public static Color Success{get{return Tone(18,132,84,100,215,164);}}
    public static Color SuccessSoft{get{return Tone(231,247,239,29,60,49);}}
    public static Color Danger{get{return Tone(190,45,45,255,132,143);}}
    public static Color DangerSoft{get{return Tone(253,237,237,65,35,46);}}
    public static Color DangerLine{get{return Tone(240,185,185,136,65,79);}}
    public static Color Warning{get{return Tone(158,98,10,241,192,107);}}
    public static Color WarningSoft{get{return Tone(255,248,232,61,48,30);}}
    public static Color WarningLine{get{return Tone(242,220,175,114,86,48);}}
    public static Color Hover{get{return Tone(244,248,255,35,46,64);}}
    public static Color DisabledBorder{get{return Tone(232,237,245,44,55,73);}}
    public static Color CheckDisabled{get{return Tone(191,204,224,55,70,94);}}
    public static Color CheckBorder{get{return Tone(197,209,227,77,94,119);}}
    public static Color PillFill{get{return Tone(244,247,252,33,43,60);}}
    public static Color PillLine{get{return Tone(234,240,248,48,63,85);}}
    public static Color SecondaryInk{get{return Tone(104,119,140,157,175,201);}}
    public static Color SecondaryBar{get{return Tone(198,210,228,69,85,111);}}
    public static Color LogBackground{get{return Tone(249,251,254,18,24,34);}}
    public static Color LogText{get{return Tone(57,71,92,174,192,217);}}
    public static Color ScrollThumb{get{return Tone(187,201,222,67,84,110);}}
    public static Color ScrollHover{get{return Tone(150,172,204,102,127,164);}}
    public static Color ProgressTrack{get{return Tone(230,236,245,35,46,64);}}
    public static Color RowSelected{get{return Tone(238,244,255,38,58,94);}}
    public static Color RowHover{get{return Tone(250,252,255,31,42,58);}}
    public static Color CaptionHover{get{return Tone(229,235,245,42,54,74);}}
    public static Color WaitFore{get{return Tone(112,128,150,160,177,201);}}
    public static Color WaitBack{get{return Tone(242,245,250,37,48,66);}}
    public static Color Divider{get{return Tone(219,226,238,50,64,85);}}
}

sealed partial class MainForm {
    ActionButton themeButton;
    internal bool IsDarkMode{get{return UiTheme.IsDark;}}
    int HeaderInteractiveLeft{get{return themeButton==null?minimizeButton.Left:themeButton.Left;}}
    void ConfigureAppearance(){
        themeButton=UiButton("深色模式",()=>SetDarkMode(!IsDarkMode),false);
        themeButton.AccessibleName="切换深色模式";themeButton.AccessibleDescription="切换程序的浅色和深色外观";
        headerPanel.Controls.Add(themeButton);themeButton.BackColor=UiTheme.Background;
        tips.SetToolTip(themeButton,"切换浅色 / 深色模式");
        UpdateThemeButton();
    }
    void UpdateThemeButton(){if(themeButton==null)return;themeButton.Text=IsDarkMode?"浅色模式":"深色模式";themeButton.AccessibleName=IsDarkMode?"切换浅色模式":"切换深色模式";}
    internal void SetDarkMode(bool dark){
        if(UiTheme.IsDark==dark){UpdateThemeButton();return;}
        var controls=Descendants(this).ToArray();
        foreach(var select in controls.OfType<UiSelect>())if(select.Popup!=null)select.Popup.Close();
        var properties=typeof(UiTheme).GetProperties(BindingFlags.Public|BindingFlags.Static).Where(p=>p.PropertyType==typeof(Color)).ToArray();
        var oldPalette=properties.Select(p=>(Color)p.GetValue(null,null)).ToArray();
        var backgrounds=controls.Select(c=>c.BackColor.ToArgb()).ToArray();var foregrounds=controls.Select(c=>c.ForeColor.ToArgb()).ToArray();
        SuspendLayout();queue.BeginUpdate();
        try{
            UiTheme.UseDark(dark);var colors=new Dictionary<int,Color>();
            for(int i=0;i<properties.Length;i++)if(!colors.ContainsKey(oldPalette[i].ToArgb()))colors.Add(oldPalette[i].ToArgb(),(Color)properties[i].GetValue(null,null));
            // Surface and focused input are both white in the light palette.
            // Containers keep the surface role; input shells restore focus color.
            colors[oldPalette[Array.FindIndex(properties,p=>p.Name=="Surface")].ToArgb()]=UiTheme.Surface;
            for(int i=0;i<controls.Length;i++){Color color;if(colors.TryGetValue(backgrounds[i],out color))controls[i].BackColor=color;if(colors.TryGetValue(foregrounds[i],out color))controls[i].ForeColor=color;}
            BackColor=headerPanel.BackColor=UiTheme.Background;
            statusPanel.BackColor=status.BackColor=elapsedLabel.BackColor=progress.BackColor=UiTheme.Surface;
            foreach(Control c in headerPanel.Controls)c.BackColor=c.Name=="brandDivider"?UiTheme.Divider:UiTheme.Background;
            foreach(var card in new[]{filesCard,settingsCard,logsCard})card.BackColor=UiTheme.Background;
            queue.BackColor=UiTheme.Surface;queue.ForeColor=UiTheme.Ink;
            log.BackColor=UiTheme.LogBackground;log.ForeColor=UiTheme.LogText;
            logWell.FillColor=UiTheme.LogBackground;logWell.BorderColor=UiTheme.LineSoft;
            foreach(Control c in logWell.Controls)if(c!=log)c.BackColor=UiTheme.LogBackground;
            foreach(var shell in controls.OfType<InputShell>())shell.RefreshAppearance();
            UpdateThemeButton();UpdateChromeTheme();
        }finally{queue.EndUpdate();ResumeLayout(false);Invalidate(true);Refresh();}
    }
}
}
