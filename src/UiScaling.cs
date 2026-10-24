using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StandaloneAssRocket {
sealed partial class MainForm {
    float uiScale=1F;bool applyingUiScale;
    readonly Dictionary<Control,Font> designFonts=new Dictionary<Control,Font>();
    readonly Dictionary<Font,Font> scaledFonts=new Dictionary<Font,Font>();
    internal float UiScale{get{return uiScale;}}
    int U(int pixels){return Math.Max(pixels==0?0:1,(int)Math.Round(pixels*uiScale));}
    [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr window);
    int WindowDpi(){try{uint dpi=GetDpiForWindow(Handle);if(dpi>0)return (int)dpi;}catch(EntryPointNotFoundException){}using(var g=CreateGraphics())return (int)g.DpiX;}
    static IEnumerable<Control> Descendants(Control root){yield return root;foreach(Control child in root.Controls)foreach(var item in Descendants(child))yield return item;}
    Font ScaleFont(Font source){Font font;if(!scaledFonts.TryGetValue(source,out font)){float pixels=source.Unit==GraphicsUnit.Pixel?source.Size:source.SizeInPoints*96F/72F;font=new Font(source.FontFamily,pixels*uiScale,source.Style,GraphicsUnit.Pixel);scaledFonts.Add(source,font);}return font;}
    internal void ApplyUiScale(int dpi,Rectangle? suggested,bool initial){
        float scale=Math.Max(1F,Math.Min(4F,dpi/96F));if(applyingUiScale)return;
        if(!initial&&Math.Abs(uiScale-scale)<.001F){if(suggested.HasValue&&WindowState==FormWindowState.Normal)Bounds=suggested.Value;return;}
        applyingUiScale=true;SuspendLayout();var oldFonts=scaledFonts.Values.ToArray();scaledFonts.Clear();
        try{
            foreach(var select in Descendants(this).OfType<UiSelect>())if(select.Popup!=null)select.Popup.Close();
            foreach(var c in Descendants(this))if(!designFonts.ContainsKey(c))designFonts[c]=c.Font;
            uiScale=scale;foreach(var pair in designFonts)if(!pair.Key.IsDisposed)pair.Key.Font=ScaleFont(pair.Value);
            var work=Screen.FromControl(this).WorkingArea;MinimumSize=new Size(Math.Min(U(1040),work.Width),Math.Min(U(800),work.Height));
            if(WindowState==FormWindowState.Normal){
                if(suggested.HasValue)Bounds=suggested.Value;
                else if(initial){int width=Math.Max(MinimumSize.Width,Math.Min(U(1280),work.Width-U(24))),height=Math.Max(MinimumSize.Height,Math.Min(U(840),work.Height-U(24)));Bounds=new Rectangle(work.Left+(work.Width-width)/2,work.Top+(work.Height-height)/2,width,height);}
            }
            var images=queue.SmallImageList;if(images!=null){images.ImageSize=new Size(1,U(34));}
            foreach(var c in Descendants(this).Reverse())c.PerformLayout();
        }finally{applyingUiScale=false;ResumeLayout(false);LayoutInterface();foreach(var font in oldFonts)font.Dispose();Invalidate(true);}
    }
    void LayoutHeader(){
        var mark=headerPanel.Controls["brandMark"];var brand=headerPanel.Controls["brandTitle"];var divider=headerPanel.Controls["brandDivider"];var subtitle=headerPanel.Controls["brandSubtitle"];
        mark.SetBounds(0,U(14),U(36),U(36));int titleWidth=TextRenderer.MeasureText(brand.Text,brand.Font).Width+U(10);brand.SetBounds(U(50),U(4),titleWidth,U(48));
        divider.SetBounds(brand.Right+U(18),U(23),U(1),U(18));int left=divider.Right+U(18);subtitle.SetBounds(left,U(10),Math.Max(0,HeaderInteractiveLeft-left-U(12)),U(44));
    }
}
}
