using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StandaloneAssRocket {
sealed class CaptionButton:Button {
    public string Kind;
    bool hover;
    public CaptionButton(string kind,string name){Kind=kind;AccessibleName=name;AccessibleRole=AccessibleRole.PushButton;TabStop=true;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=UiTheme.Background;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(UiTheme.Background);
        // Draw the caption glyphs in logical pixels, like the rest of the UI.
        float scale=UiTheme.Pixels(this,100)/100F;
        g.ScaleTransform(scale,scale);int logicalWidth=(int)(Width/scale),logicalHeight=(int)(Height/scale);
        if(hover)UiTheme.Fill(g,new Rectangle(0,0,logicalWidth-1,logicalHeight-1),7,Kind=="close"?Color.FromArgb(220,54,65):UiTheme.CaptionHover);
        using(var pen=new Pen(hover&&Kind=="close"?Color.White:UiTheme.Body,1.5F)){
            int x=logicalWidth/2,y=logicalHeight/2;
            if(Kind=="minimize")g.DrawLine(pen,x-5,y+3,x+5,y+3);
            else if(Kind=="close"){g.DrawLine(pen,x-5,y-5,x+5,y+5);g.DrawLine(pen,x+5,y-5,x-5,y+5);}
            else if(FindForm()!=null&&FindForm().WindowState==FormWindowState.Maximized){g.DrawRectangle(pen,x-3,y-6,9,9);using(var brush=new SolidBrush(hover?UiTheme.CaptionHover:UiTheme.Background))g.FillRectangle(brush,x-6,y-3,9,9);g.DrawRectangle(pen,x-6,y-3,9,9);}
            else g.DrawRectangle(pen,x-5,y-5,10,10);
        }
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,logicalWidth-10,logicalHeight-10));
    }
}

sealed partial class MainForm {
    CaptionButton minimizeButton,maximizeButton,closeButton;
    Rectangle normalWindowBounds;int nativeSizeState;bool restoringBounds;
    [StructLayout(LayoutKind.Sequential)]struct NativePoint {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]struct NativeStyle {public int OldStyle,NewStyle;}
    [StructLayout(LayoutKind.Sequential)]struct NativeRect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]struct MinMaxInfo {public NativePoint Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
    [StructLayout(LayoutKind.Sequential)]struct MonitorInfo {public int Size;public NativeRect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")]static extern bool ReleaseCapture();
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
    [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll")]static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")]static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
    [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    void ConfigureChrome(){
        FormBorderStyle=FormBorderStyle.None;Padding=new Padding(1);
        minimizeButton=new CaptionButton("minimize","最小化");maximizeButton=new CaptionButton("maximize","最大化或还原");closeButton=new CaptionButton("close","关闭");
        minimizeButton.Click+=(s,e)=>WindowState=FormWindowState.Minimized;maximizeButton.Click+=(s,e)=>ToggleMaximize();closeButton.Click+=(s,e)=>Close();
        foreach(var b in new[]{minimizeButton,maximizeButton,closeButton}){headerPanel.Controls.Add(b);tips.SetToolTip(b,b.AccessibleName);}
        MouseEventHandler drag=(s,e)=>{if(e.Button!=MouseButtons.Left)return;if(e.Clicks==2){ToggleMaximize();return;}ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);};
        headerPanel.MouseDown+=drag;foreach(Control c in headerPanel.Controls)if(!(c is CaptionButton))c.MouseDown+=drag;
        Resize+=(s,e)=>{if(maximizeButton!=null)maximizeButton.Invalidate();};
    }
    void LayoutChrome(){
        if(closeButton==null)return;int width=U(46),height=U(36),y=U(10);
        closeButton.SetBounds(headerPanel.Width-width,y,width,height);maximizeButton.SetBounds(headerPanel.Width-2*width-U(4),y,width,height);minimizeButton.SetBounds(headerPanel.Width-3*width-U(8),y,width,height);
        if(themeButton!=null)themeButton.SetBounds(minimizeButton.Left-U(116),U(12),U(104),U(32));
    }
    void ToggleMaximize(){WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;LayoutInterface();}
    protected override CreateParams CreateParams {get{var cp=base.CreateParams;cp.Style=(cp.Style&~0x00C00000)|0x00040000|0x00020000|0x00010000|0x00080000;return cp;}}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);UpdateChromeTheme();}
    void UpdateChromeTheme(){if(!IsHandleCreated)return;try{int corners=2;DwmSetWindowAttribute(Handle,33,ref corners,4);int dark=UiTheme.IsDark?1:0;DwmSetWindowAttribute(Handle,20,ref dark,4);Color color=UiTheme.Line;int border=color.R|(color.G<<8)|(color.B<<16);DwmSetWindowAttribute(Handle,34,ref border,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
    protected override void WndProc(ref Message m){
        // DefWindowProc otherwise paints a standard caption on reactivation,
        // even though NCCALCSIZE has made the entire window client area.
        if(m.Msg==0x85){m.Result=IntPtr.Zero;return;}
        if(m.Msg==0x86&&WindowState!=FormWindowState.Minimized){m.Result=new IntPtr(1);return;}
        if(m.Msg==0x7C&&m.WParam.ToInt64()==-16){var style=(NativeStyle)Marshal.PtrToStructure(m.LParam,typeof(NativeStyle));style.NewStyle&=~0x00C00000;Marshal.StructureToPtr(style,m.LParam,false);}
        if(m.Msg==0x02E0&&uiReady){var suggested=(NativeRect)Marshal.PtrToStructure(m.LParam,typeof(NativeRect));ApplyUiScale((int)(m.WParam.ToInt64()&0xffff),Rectangle.FromLTRB(suggested.Left,suggested.Top,suggested.Right,suggested.Bottom),false);m.Result=IntPtr.Zero;return;}
        if(m.Msg==5){
            int previous=nativeSizeState;nativeSizeState=m.WParam.ToInt32();Rectangle restore=normalWindowBounds;base.WndProc(ref m);
            // WinForms accounts for WS_THICKFRAME when restoring, even though
            // NCCALCSIZE removes it. Preserve the actual borderless outer bounds.
            if(nativeSizeState==0&&!restoringBounds){if(previous!=0&&!restore.IsEmpty){restoringBounds=true;Bounds=restore;restoringBounds=false;BeginInvoke((Action)(()=>{if(IsDisposed||WindowState!=FormWindowState.Normal)return;restoringBounds=true;Bounds=restore;normalWindowBounds=restore;restoringBounds=false;}));}normalWindowBounds=Bounds;}return;
        }
        if(m.Msg==3){base.WndProc(ref m);if(!restoringBounds&&IsHandleCreated&&!IsZoomed(Handle)&&!IsIconic(Handle))normalWindowBounds=Bounds;return;}
        if(m.Msg==0x83){m.Result=IntPtr.Zero;return;}
        if(m.Msg==0x24){
            base.WndProc(ref m);var monitor=MonitorFromWindow(m.HWnd,2);var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};
            if(monitor!=IntPtr.Zero&&GetMonitorInfo(monitor,ref info)){
                var limits=(MinMaxInfo)Marshal.PtrToStructure(m.LParam,typeof(MinMaxInfo));limits.MaxPosition.X=info.Work.Left-info.Monitor.Left;limits.MaxPosition.Y=info.Work.Top-info.Monitor.Top;limits.MaxSize.X=info.Work.Right-info.Work.Left;limits.MaxSize.Y=info.Work.Bottom-info.Work.Top;limits.MinTrackSize.X=MinimumSize.Width;limits.MinTrackSize.Y=MinimumSize.Height;Marshal.StructureToPtr(limits,m.LParam,false);
            }return;
        }
        if(m.Msg==0x84&&IsHandleCreated){
            var point=PointToClient(new Point((short)(m.LParam.ToInt64()&0xffff),(short)((m.LParam.ToInt64()>>16)&0xffff)));int hit=0;
            if(WindowState!=FormWindowState.Maximized){int edge=U(7);bool left=point.X<edge,right=point.X>=ClientSize.Width-edge,top=point.Y<edge,bottom=point.Y>=ClientSize.Height-edge;
                hit=top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:0;
            }
            if(hit==0&&headerPanel!=null&&point.Y>=0&&point.Y<headerPanel.Bottom&&minimizeButton!=null&&point.X<headerPanel.Left+HeaderInteractiveLeft)hit=2;
            if(hit!=0){m.Result=new IntPtr(hit);return;}
        }
        base.WndProc(ref m);
    }
}
}
