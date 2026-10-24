using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StandaloneAssRocket {
static partial class UiTheme {
    public static readonly Font BaseFont=MakeFont(12F,FontStyle.Regular),SmallFont=MakeFont(11F,FontStyle.Regular),LabelFont=MakeFont(12F,FontStyle.Regular);
    public static readonly Font CardTitleFont=MakeFont(15.5F,FontStyle.Bold),SectionFont=MakeFont(13F,FontStyle.Bold),ButtonFont=MakeFont(12F,FontStyle.Regular);
    public static readonly Font CheckFont=MakeFont(12F,FontStyle.Regular),TableFont=MakeFont(12F,FontStyle.Regular),TableHeaderFont=MakeFont(11F,FontStyle.Bold);
    public static readonly Font MonoFont=new Font("Consolas",12F,FontStyle.Regular,GraphicsUnit.Pixel);
    public const int FieldInlineWidth=240;
    public static int Pixels(Control control,int value){var window=control.FindForm();var form=window as MainForm;var dialog=window as FontDialog;float scale=form!=null?form.UiScale:dialog!=null?dialog.UiScale:1F;return Math.Max(value==0?0:1,(int)Math.Round(value*scale));}
    public static Font MakeFont(float size,FontStyle style){return new Font("Microsoft YaHei UI",size,style,GraphicsUnit.Pixel);}
    public static GraphicsPath Round(Rectangle rect,int radius){
        var path=new GraphicsPath();
        int d=radius*2;
        if(d<=0||rect.Width<=0||rect.Height<=0){path.AddRectangle(rect);return path;}
        d=Math.Min(d,Math.Min(rect.Width,rect.Height));
        path.AddArc(rect.X,rect.Y,d,d,180,90);path.AddArc(rect.Right-d,rect.Y,d,d,270,90);path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);path.AddArc(rect.X,rect.Bottom-d,d,d,90,90);path.CloseFigure();return path;
    }
    public static void Fill(Graphics g,Rectangle rect,int radius,Color color){if(rect.Width<=0||rect.Height<=0)return;using(var path=Round(rect,radius))using(var brush=new SolidBrush(color))g.FillPath(brush,path);}
    public static void Stroke(Graphics g,Rectangle rect,int radius,Color color,float width){if(rect.Width<=0||rect.Height<=0)return;using(var path=Round(rect,radius))using(var pen=new Pen(color,width))g.DrawPath(pen,path);}
    public static void Text(Graphics g,string text,Font font,Rectangle rect,Color color,TextFormatFlags flags){TextRenderer.DrawText(g,text,font,rect,color,flags|TextFormatFlags.NoPadding);}
}

sealed class Surface:Panel {
    int P(int value){return UiTheme.Pixels(this,value);}

    bool dragging;
    public Surface(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=UiTheme.Background;Padding=new Padding(22);}
    public bool Dragging{get{return dragging;}set{if(dragging==value)return;dragging=value;Invalidate();}}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        var rect=new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1));
        using(var fill=new SolidBrush(UiTheme.Surface))using(var path=UiTheme.Round(rect,P(16)))e.Graphics.FillPath(fill,path);
        using(var pen=new Pen(dragging?UiTheme.Accent:UiTheme.Line,dragging?1.7f:1f))using(var path=UiTheme.Round(rect,P(16)))e.Graphics.DrawPath(pen,path);
        if(dragging)using(var glow=new Pen(Color.FromArgb(60,UiTheme.Accent),4f))using(var path=UiTheme.Round(Rectangle.Inflate(rect,-3,-3),P(13)))e.Graphics.DrawPath(glow,path);
    }
}

sealed class ActionButton:Button {
    int P(int value){return UiTheme.Pixels(this,value);}

    public bool Primary;
    bool hover,pressed;
    public ActionButton(){
        FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;BackColor=UiTheme.Surface;Font=UiTheme.ButtonFont;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
    }
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){pressed=true;Invalidate();}base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);
        var rect=new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1));Color text;
        if(!Enabled){
            using(var brush=new SolidBrush(UiTheme.InputDisabled))using(var path=UiTheme.Round(rect,P(10)))g.FillPath(brush,path);
            using(var pen=new Pen(UiTheme.DisabledBorder))using(var path=UiTheme.Round(rect,P(10)))g.DrawPath(pen,path);text=UiTheme.Faint;
        }else if(Primary){
            Color top=pressed?Color.FromArgb(45,77,186):hover?Color.FromArgb(92,132,248):Color.FromArgb(78,122,242),bottom=pressed?Color.FromArgb(37,66,164):hover?Color.FromArgb(50,88,214):Color.FromArgb(43,80,206);
            using(var brush=new LinearGradientBrush(new Rectangle(0,0,Width,Height+1),top,bottom,90f))using(var path=UiTheme.Round(rect,P(10)))g.FillPath(brush,path);
            using(var pen=new Pen(pressed?Color.FromArgb(34,61,150):UiTheme.AccentDeep,1f))using(var path=UiTheme.Round(rect,P(10)))g.DrawPath(pen,path);
            using(var pen=new Pen(Color.FromArgb(58,255,255,255),1f))g.DrawLine(pen,rect.Left+P(14),rect.Top+1,rect.Right-P(14),rect.Top+1);text=Color.White;
        }else{
            Color fill=hover?UiTheme.Hover:UiTheme.Surface;
            using(var brush=new SolidBrush(fill))using(var path=UiTheme.Round(rect,P(10)))g.FillPath(brush,path);
            using(var pen=new Pen(hover?UiTheme.AccentLine:UiTheme.Line,1f))using(var path=UiTheme.Round(rect,P(10)))g.DrawPath(pen,path);text=UiTheme.Body;
        }
        UiTheme.Text(g,Text,Font,Rectangle.Inflate(ClientRectangle,-P(8),-1),text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused&&ShowFocusCues&&Enabled){using(var pen=new Pen(Color.FromArgb(175,Primary?Color.White:UiTheme.Accent),1.4f))using(var path=UiTheme.Round(Rectangle.Inflate(rect,-3,-3),P(8)))g.DrawPath(pen,path);}
    }
}

sealed class BrandMark:Control {
    int P(int value){return UiTheme.Pixels(this,value);}

    public BrandMark(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=UiTheme.Background;}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);
        var rect=new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1));
        using(var brush=new LinearGradientBrush(new Rectangle(0,0,Width,Height),Color.FromArgb(96,139,250),Color.FromArgb(45,84,208),55f))using(var path=UiTheme.Round(rect,P(13)))g.FillPath(brush,path);
        using(var pen=new Pen(Color.FromArgb(70,255,255,255),1f))using(var path=UiTheme.Round(rect,P(13)))g.DrawPath(pen,path);
        using(var brush=new SolidBrush(Color.FromArgb(245,255,255,255))){
            int left=P(12),top=P(15),w=Width-P(24);
            using(var p=UiTheme.Round(new Rectangle(left,top,w,3),1))g.FillPath(brush,p);
            using(var p=UiTheme.Round(new Rectangle(left,top+P(7),(int)(w*.74),3),1))g.FillPath(brush,p);
            using(var p=UiTheme.Round(new Rectangle(left,top+P(14),(int)(w*.46),3),1))g.FillPath(brush,p);
        }
    }
}

sealed class UiCheckBox:CheckBox {
    int P(int value){return UiTheme.Pixels(this,value);}

    bool hover;
    public UiCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Cursor=Cursors.Hand;BackColor=UiTheme.Surface;Font=UiTheme.CheckFont;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
        int size=P(16),top=Math.Max(0,(Height-size)/2),left=1;var box=new Rectangle(left,top,size,size);
        if(Checked){
            Color tone=!Enabled?UiTheme.CheckDisabled:hover?UiTheme.AccentDeep:UiTheme.Accent;
            using(var brush=new SolidBrush(tone))using(var path=UiTheme.Round(box,P(5)))g.FillPath(brush,path);
            if(Enabled)using(var pen=new Pen(Color.FromArgb(225,255,255,255),2.1f))g.DrawLines(pen,new[]{new Point(left+P(4),top+P(9)),new Point(left+P(7),top+P(13)),new Point(left+P(14),top+P(5))});
        }else{
            using(var brush=new SolidBrush(!Enabled?UiTheme.InputDisabled:UiTheme.Surface))using(var path=UiTheme.Round(box,P(5)))g.FillPath(brush,path);
            using(var pen=new Pen(!Enabled?UiTheme.Line:hover?UiTheme.AccentLine:UiTheme.CheckBorder,1.2f))using(var path=UiTheme.Round(box,P(5)))g.DrawPath(pen,path);
        }
        var textRect=new Rectangle(left+size+P(9),0,Math.Max(0,Width-size-P(10)),Height);
        UiTheme.Text(g,Text,Font,textRect,!Enabled?UiTheme.Faint:UiTheme.Body,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused&&ShowFocusCues&&Enabled){using(var pen=new Pen(Color.FromArgb(130,UiTheme.Accent),1f))using(var path=UiTheme.Round(Rectangle.Inflate(textRect,2,-P(4)),P(5)))g.DrawPath(pen,path);}
    }
}

sealed class CanvasNumeric:CleanNumeric {
    public CanvasNumeric(){Increment=4;}
    protected override void ValidateEditText(){int value;if(int.TryParse(Text,out value)&&value>=Minimum&&value<=Maximum)base.ValidateEditText();}
    public override void UpButton(){Nudge(true);}
    public override void DownButton(){Nudge(false);}
    void Nudge(bool up){long value;bool parsed=long.TryParse(Text,out value);if(!parsed)value=(long)Value;value=Math.Max((long)Minimum,Math.Min((long)Maximum,value));long next=up?(value/4+1)*4:((value+3)/4-1)*4;Value=Math.Max(Minimum,Math.Min(Maximum,next));}
}

sealed class UiSelect:Control {
    int P(int value){return UiTheme.Pixels(this,value);}

    readonly List<object> values=new List<object>();readonly ItemList items;int selectedIndex=-1;bool hover;SelectPopup popup;
    public UiSelect(){
        items=new ItemList(this);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
        BackColor=UiTheme.Surface;ForeColor=UiTheme.Ink;Font=UiTheme.BaseFont;Cursor=Cursors.Hand;TabStop=true;Height=38;AccessibleRole=AccessibleRole.ComboBox;
    }
    public ItemList Items{get{return items;}}
    internal List<object> Values{get{return values;}}
    internal SelectPopup Popup{get{return popup;}}
    public int SelectedIndex{
        get{return selectedIndex;}
        set{
            int index=values.Count==0?-1:Math.Max(-1,Math.Min(values.Count-1,value));
            if(index==selectedIndex)return;
            selectedIndex=index;Text=index<0?"":ItemText(values[index]);Invalidate();
            var handler=SelectedIndexChanged;if(handler!=null)handler(this,EventArgs.Empty);
        }
    }
    public object SelectedItem{get{return selectedIndex>=0&&selectedIndex<values.Count?values[selectedIndex]:null;}}
    public event EventHandler SelectedIndexChanged;
    public static string ItemText(object item){return item==null?"":item.ToString();}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
        var rect=new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1));
        using(var brush=new SolidBrush(!Enabled?UiTheme.InputDisabled:hover?UiTheme.InputFocus:UiTheme.Input))using(var path=UiTheme.Round(rect,P(9)))g.FillPath(brush,path);
        UiTheme.Text(g,Text,Font,new Rectangle(P(13),0,Math.Max(P(10),Width-P(46)),Height),!Enabled?UiTheme.Faint:UiTheme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        int cx=Width-P(19),cy=Height/2;
        using(var pen=new Pen(!Enabled?UiTheme.Faint:hover?UiTheme.Body:UiTheme.Muted,1.7f)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;g.DrawLines(pen,new[]{new Point(cx-P(4),cy-2),new Point(cx,cy+2),new Point(cx+P(4),cy-2)});}
        Color border=!Enabled?UiTheme.Line:Focused||popup!=null?UiTheme.Accent:hover?UiTheme.AccentLine:UiTheme.Line;
        if(Focused||popup!=null)using(var glow=new Pen(Color.FromArgb(32,UiTheme.Accent),3f))using(var path=UiTheme.Round(Rectangle.Inflate(rect,-1,-1),P(8)))g.DrawPath(glow,path);
        using(var pen=new Pen(border,Focused||popup!=null?1.6f:1f))using(var path=UiTheme.Round(rect,P(9)))g.DrawPath(pen,path);
    }
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnEnabledChanged(EventArgs e){if(!Enabled&&popup!=null)popup.Close();Invalidate();base.OnEnabledChanged(e);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left&&Enabled)Open();base.OnMouseDown(e);}
    protected override bool IsInputKey(Keys keyData){return keyData==Keys.Up||keyData==Keys.Down||keyData==Keys.Enter||keyData==Keys.Space||base.IsInputKey(keyData);}
    protected override void OnKeyDown(KeyEventArgs e){
        if(e.Alt&&e.KeyCode==Keys.Down){Open();e.Handled=true;}
        else if(e.KeyCode==Keys.Down&&values.Count>0){SelectedIndex=Math.Min(values.Count-1,selectedIndex+1);e.Handled=true;}
        else if(e.KeyCode==Keys.Up&&values.Count>0){SelectedIndex=Math.Max(0,selectedIndex-1);e.Handled=true;}
        else if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space||e.KeyCode==Keys.F4){Open();e.Handled=true;}
        base.OnKeyDown(e);
    }
    internal void Open(){
        if(!Enabled||values.Count==0||popup!=null)return;
        var host=FindForm();if(host==null)return;
        popup=new SelectPopup(this);popup.Show(host);popup.Activate();Invalidate();
    }
    internal void PopupClosed(){popup=null;Invalidate();if(!IsDisposed&&IsHandleCreated&&CanFocus)Focus();}
}
sealed class ItemList:IEnumerable {
    readonly UiSelect owner;
    internal ItemList(UiSelect owner){this.owner=owner;}
    public int Count{get{return owner.Values.Count;}}
    public object this[int index]{get{return owner.Values[index];}}
    public void Add(object item){owner.Values.Add(item);owner.Invalidate();}
    public void AddRange(object[] source){owner.Values.AddRange(source);owner.Invalidate();}
    public void Clear(){owner.Values.Clear();owner.SelectedIndex=-1;owner.Text="";owner.Invalidate();}
    public IEnumerator GetEnumerator(){return owner.Values.GetEnumerator();}
}
sealed class SelectPopup:Form {
    readonly UiSelect owner;int hover;int RowHeight{get{return UiTheme.Pixels(owner,40);}}int Pad{get{return UiTheme.Pixels(owner,6);}}
    public SelectPopup(UiSelect owner){
        this.owner=owner;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;BackColor=UiTheme.Surface;DoubleBuffered=true;KeyPreview=true;MinimizeBox=false;MaximizeBox=false;Font=owner.Font;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        int rows=Math.Min(owner.Values.Count,8),width=Math.Max(owner.Width,UiTheme.Pixels(owner,220));
        var area=Screen.FromControl(owner).WorkingArea;var origin=owner.PointToScreen(new Point(0,owner.Height+4));
        int height=rows*RowHeight+Pad*2;
        if(origin.Y+height>area.Bottom)origin.Y=owner.PointToScreen(Point.Empty).Y-height-4;
        Bounds=new Rectangle(origin.X,origin.Y,width,height);hover=owner.SelectedIndex;
        Deactivate+=(s,e)=>Close();FormClosed+=(s,e)=>owner.PopupClosed();
    }
    protected override void OnShown(EventArgs e){base.OnShown(e);UpdateRegion();Activate();}
    protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);UpdateRegion();}
    void UpdateRegion(){if(Width<=0||Height<=0)return;using(var path=UiTheme.Round(new Rectangle(0,0,Width,Height),10))Region=new Region(path);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.Clear(UiTheme.Surface);g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var brush=new SolidBrush(UiTheme.Surface))using(var path=UiTheme.Round(new Rectangle(0,0,Width-1,Height-1),10))g.FillPath(brush,path);
        for(int i=0;i<owner.Values.Count&&i<8;i++){
            var row=new Rectangle(Pad,Pad+i*RowHeight,Math.Max(10,Width-Pad*2),RowHeight-2);bool active=i==hover;bool selected=i==owner.SelectedIndex;
            if(active){using(var brush=new SolidBrush(UiTheme.AccentSoft))using(var path=UiTheme.Round(row,7))g.FillPath(brush,path);}
            if(selected)using(var brush=new SolidBrush(UiTheme.Accent))using(var path=UiTheme.Round(new Rectangle(row.X+4,row.Top+9,3,row.Height-18),1))g.FillPath(brush,path);
            UiTheme.Text(g,UiSelect.ItemText(owner.Values[i]),Font,new Rectangle(row.X+15,row.Y,Math.Max(10,row.Width-24),row.Height),active?UiTheme.AccentDeep:selected?UiTheme.Ink:UiTheme.Body,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
        using(var pen=new Pen(UiTheme.Line,1f))using(var path=UiTheme.Round(new Rectangle(0,0,Width-1,Height-1),10))g.DrawPath(pen,path);
    }
    protected override void OnMouseMove(MouseEventArgs e){int row=(e.Y-Pad)/RowHeight;int index=row>=0&&row<Math.Min(owner.Values.Count,8)?row:hover;if(index!=hover){hover=index;Invalidate();}base.OnMouseMove(e);}
    protected override void OnMouseLeave(EventArgs e){hover=owner.SelectedIndex;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseClick(MouseEventArgs e){int row=(e.Y-Pad)/RowHeight;if(row>=0&&row<owner.Values.Count){owner.SelectedIndex=row;Close();}base.OnMouseClick(e);}
    protected override void OnKeyDown(KeyEventArgs e){
        int count=Math.Min(owner.Values.Count,8);
        if(e.KeyCode==Keys.Down){hover=Math.Min(count-1,hover+1);Invalidate();e.Handled=true;}
        else if(e.KeyCode==Keys.Up){hover=Math.Max(0,hover-1);Invalidate();e.Handled=true;}
        else if(e.KeyCode==Keys.Enter){owner.SelectedIndex=hover;Close();e.Handled=true;}
        else if(e.KeyCode==Keys.Escape){Close();e.Handled=true;}
        base.OnKeyDown(e);
    }
}

sealed class SectionTitle:Label {
    int P(int value){return UiTheme.Pixels(this,value);}

    public bool Secondary=false;
    public SectionTitle(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=UiTheme.Surface;Font=UiTheme.CardTitleFont;ForeColor=UiTheme.Ink;AutoSize=false;}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
        Color bar=Secondary?UiTheme.SecondaryBar:UiTheme.Accent;
        int top=Math.Max(0,(Height-P(15))/2);
        UiTheme.Fill(g,new Rectangle(0,top,3,P(15)),2,bar);
        UiTheme.Text(g,Text,Font,new Rectangle(P(12),0,Math.Max(0,Width-P(12)),Height),Secondary?UiTheme.SecondaryInk:UiTheme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
}

sealed class PillLabel:Label {
    int P(int value){return UiTheme.Pixels(this,value);}

    public bool RightAligned,LeadingDot;
    public PillLabel(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=UiTheme.Surface;Font=UiTheme.SmallFont;ForeColor=UiTheme.Muted;AutoSize=false;}
    protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
        var size=TextRenderer.MeasureText(Text,Font);
        int dot=LeadingDot?P(13):0,w=Math.Min(Width,size.Width+P(22)+dot),h=Math.Min(Height,P(24)),x=RightAligned?Math.Max(0,Width-w):0,y=Math.Max(0,(Height-h)/2);
        if(w<=0||h<=0)return;
        var rect=new Rectangle(x,y,w,h);
        using(var brush=new SolidBrush(UiTheme.PillFill))using(var path=UiTheme.Round(rect,P(12)))g.FillPath(brush,path);
        using(var pen=new Pen(UiTheme.PillLine,1f))using(var path=UiTheme.Round(rect,P(12)))g.DrawPath(pen,path);
        if(LeadingDot){using(var brush=new SolidBrush(UiTheme.Accent))g.FillEllipse(brush,x+P(10),y+h/2-2,P(4),P(4));}
        UiTheme.Text(g,Text,Font,new Rectangle(x+P(11)+dot,y,Math.Max(0,w-P(18)-dot),h),UiTheme.Muted,(RightAligned?TextFormatFlags.HorizontalCenter:TextFormatFlags.Left)|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
}

sealed class NoticeLabel:Label {
    int P(int value){return UiTheme.Pixels(this,value);}

    bool error;
    public bool IsError{get{return error;}set{if(error==value)return;error=value;Invalidate();}}
    public NoticeLabel(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=UiTheme.Surface;Font=UiTheme.SmallFont;ForeColor=UiTheme.Warning;AutoSize=false;}
    protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
        var rect=new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1));
        using(var brush=new SolidBrush(error?UiTheme.DangerSoft:UiTheme.WarningSoft))using(var path=UiTheme.Round(rect,P(10)))g.FillPath(brush,path);
        using(var pen=new Pen(error?UiTheme.DangerLine:UiTheme.WarningLine,1f))using(var path=UiTheme.Round(rect,P(10)))g.DrawPath(pen,path);
        using(var pen=new Pen(error?UiTheme.Danger:UiTheme.Warning,2f)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;g.DrawLine(pen,1,P(12),1,Math.Max(P(13),Height-P(12)));}
        UiTheme.Text(g,Text,Font,new Rectangle(P(15),P(8),Math.Max(0,Width-P(27)),Math.Max(0,Height-P(16))),error?UiTheme.Danger:UiTheme.Warning,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);
    }
}

sealed class InputShell:Panel {
    int P(int value){return UiTheme.Pixels(this,value);}

    bool invalid;
    public bool Invalid{get{return invalid;}set{if(invalid==value)return;invalid=value;Invalidate();}}
    readonly Control inner;readonly NumericUpDown numeric;bool hover,focus,upHover,downHover;
    public InputShell(Control control){
        inner=control;numeric=control as NumericUpDown;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        BackColor=UiTheme.Surface;Controls.Add(inner);inner.BackColor=UiTheme.Input;inner.ForeColor=UiTheme.Ink;
        if(numeric!=null){numeric.TextAlign=HorizontalAlignment.Right;foreach(Control child in numeric.Controls)if(child.GetType().Name.IndexOf("UpDownButtons",StringComparison.Ordinal)>=0)child.Visible=false;}
        inner.Enter+=(s,e)=>{focus=true;SyncBackground();Invalidate();};inner.Leave+=(s,e)=>{focus=false;SyncBackground();Invalidate();};
        inner.MouseEnter+=(s,e)=>{hover=true;Invalidate();};inner.MouseLeave+=(s,e)=>{hover=false;Invalidate();};
        inner.EnabledChanged+=(s,e)=>{SyncBackground();Invalidate();};Click+=(s,e)=>{if(inner.CanFocus)inner.Focus();};
    }
    public Control Inner{get{return inner;}}
    internal void RefreshAppearance(){SyncBackground();inner.ForeColor=UiTheme.Ink;Invalidate(true);}
    Color FieldBackground{get{return Enabled&&inner.Enabled?(focus?UiTheme.InputFocus:UiTheme.Input):UiTheme.InputDisabled;}}
    void SyncBackground(){if(inner==null)return;ApplyBackground(inner,FieldBackground);}
    static void ApplyBackground(Control control,Color color){
        if(control.BackColor!=color)control.BackColor=color;
        // NumericUpDown contains a separate native edit window. It must share
        // the shell's disabled/focused background instead of drawing a rectangle.
        foreach(Control child in control.Controls)if(child is TextBoxBase)ApplyBackground(child,color);
    }
    protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);SyncBackground();Invalidate();}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;upHover=false;downHover=false;Cursor=Cursors.Default;Invalidate();base.OnMouseLeave(e);}
    protected override void OnLayout(LayoutEventArgs e){
        base.OnLayout(e);if(inner==null)return;
        int padX=numeric!=null?P(8):P(11),reserved=numeric!=null?P(30):0;
        int height=inner.PreferredSize.Height;
        if(height<=0||height>Height-2)height=Math.Max(P(12),Height-P(12));
        height=Math.Min(height,Math.Max(P(12),Height-2));
        inner.SetBounds(padX,(Height-height)/2,Math.Max(P(10),Width-padX*2-reserved),height);
    }
    protected override void OnPaint(PaintEventArgs e){
        SyncBackground();base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        var rect=new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1));bool on=Enabled&&inner.Enabled;
        Color fill=FieldBackground;
        using(var brush=new SolidBrush(fill))using(var path=UiTheme.Round(rect,P(9)))g.FillPath(brush,path);
        Color border=!on?UiTheme.Line:invalid?UiTheme.Danger:focus?UiTheme.Accent:hover?UiTheme.AccentLine:UiTheme.Line;
        if(focus)using(var glow=new Pen(Color.FromArgb(36,UiTheme.Accent),3f))using(var path=UiTheme.Round(Rectangle.Inflate(rect,-1,-1),P(8)))g.DrawPath(glow,path);
        using(var pen=new Pen(border,focus?1.6f:1f))using(var path=UiTheme.Round(rect,P(9)))g.DrawPath(pen,path);
        if(numeric!=null){
            int x=Width-P(18),cy=Height/2;
            decimal current;bool parsed=decimal.TryParse(numeric.Text,out current);bool canUp=on&&parsed&&current<numeric.Maximum,canDown=on&&parsed&&current>numeric.Minimum;
            using(var pen=new Pen(!canUp?UiTheme.Faint:upHover?UiTheme.Accent:UiTheme.Muted,1.6f)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;g.DrawLines(pen,new[]{new Point(x-P(4),cy-P(4)),new Point(x,cy-P(8)),new Point(x+P(4),cy-P(4))});}
            using(var pen=new Pen(!canDown?UiTheme.Faint:downHover?UiTheme.Accent:UiTheme.Muted,1.6f)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;g.DrawLines(pen,new[]{new Point(x-P(4),cy+P(4)),new Point(x,cy+P(8)),new Point(x+P(4),cy+P(4))});}
        }
    }
    protected override void OnMouseMove(MouseEventArgs e){
        if(numeric!=null&&Enabled&&numeric.Enabled){bool up=e.X>=Width-P(30)&&e.Y<Height/2,down=e.X>=Width-P(30)&&e.Y>=Height/2;Cursor=e.X>=Width-P(30)?Cursors.Hand:Cursors.Default;if(up!=upHover||down!=downHover){upHover=up;downHover=down;Invalidate();}}
        base.OnMouseMove(e);
    }
    protected override void OnMouseDown(MouseEventArgs e){
        if(numeric!=null&&Enabled&&numeric.Enabled&&e.Button==MouseButtons.Left&&e.X>=Width-P(30)){if(e.Y<Height/2)numeric.UpButton();else numeric.DownButton();numeric.Focus();upHover=false;downHover=false;Invalidate();return;}
        base.OnMouseDown(e);
    }
}

sealed class WellPanel:Panel {
    public Color FillColor=UiTheme.LogBackground;public Color BorderColor=UiTheme.LineSoft;public int Radius=11;
    public WellPanel(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=UiTheme.Surface;}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        var rect=new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1));
        using(var brush=new SolidBrush(FillColor))using(var path=UiTheme.Round(rect,Radius))g.FillPath(brush,path);
        using(var pen=new Pen(BorderColor,1f))using(var path=UiTheme.Round(rect,Radius))g.DrawPath(pen,path);
    }
}

sealed class ScrollHost:Panel {
    int P(int value){return UiTheme.Pixels(this,value);}

    readonly WheelFilter filter;int offset;bool hover,drag;int dragStartY,dragStartOffset;bool wasScroll;
    public readonly Panel Content;
    public ScrollHost(){
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=UiTheme.Surface;
        Content=new Panel{BackColor=UiTheme.Surface};Controls.Add(Content);filter=new WheelFilter(this);
    }
    public int UsableWidth{get{return Math.Max(P(120),Width-P(12));}}
    internal int Offset{get{return offset;}}
    internal int MaxOffset{get{return Math.Max(0,Content.Height-Height);}}
    bool Scrollable{get{return Content.Height>Height+1;}}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Application.AddMessageFilter(filter);}
    protected override void OnHandleDestroyed(EventArgs e){Application.RemoveMessageFilter(filter);base.OnHandleDestroyed(e);}
    public void Configure(int width,int height){
        if(offset>Math.Max(0,height-Height))offset=Math.Max(0,height-Height);
        Content.SetBounds(0,-offset,Math.Max(P(60),width),Math.Max(1,height));
        bool now=Scrollable;if(now!=wasScroll){wasScroll=now;Refresh();}else Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);if(!Scrollable)return;var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var pen=new Pen(UiTheme.LineSoft,1f))g.DrawLine(pen,Width-P(6),P(8),Width-P(6),Height-P(8));
        var rect=ThumbRect();using(var brush=new SolidBrush(hover||drag?UiTheme.ScrollHover:UiTheme.ScrollThumb))using(var path=UiTheme.Round(rect,3))g.FillPath(brush,path);
    }
    Rectangle ThumbRect(){
        int track=Math.Max(P(20),Height-P(8));
        int thumbHeight=Scrollable?(int)((double)Height*Height/Math.Max(Height,Content.Height)):track;
        thumbHeight=Math.Max(P(34),Math.Min(track,thumbHeight));int max=MaxOffset,span=Math.Max(0,track-thumbHeight),y=P(4)+(max<=0||span==0?0:(int)((double)offset*span/max));
        return new Rectangle(Math.Max(0,Width-P(9)),y,P(6),thumbHeight);
    }
    internal void ScrollBy(int delta){SetOffset(offset+delta);}
    internal void SetOffset(int value){int next=Math.Max(0,Math.Min(MaxOffset,value));if(next==offset)return;offset=next;Content.Top=-offset;Invalidate();}
    protected override void OnMouseMove(MouseEventArgs e){
        bool over=e.X>=Width-P(12);Cursor=over?Cursors.Hand:Cursors.Default;if(over!=hover){hover=over;Invalidate();}
        if(drag&&Scrollable){
            int track=Math.Max(P(20),Height-P(8)),thumbHeight=Math.Max(P(34),Math.Min(track,(int)((double)Height*Height/Math.Max(Height,Content.Height)))),span=Math.Max(1,track-thumbHeight);
            SetOffset(dragStartOffset+(int)((e.Y-dragStartY)*(double)MaxOffset/span));
        }
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){
        if(e.Button==MouseButtons.Left&&e.X>=Width-P(12)&&Scrollable){
            var rect=ThumbRect();if(rect.Contains(e.Location)){drag=true;dragStartY=e.Y;dragStartOffset=offset;Capture=true;Invalidate();}
            else ScrollBy(e.Y<rect.Top?-Height:Height);
        }
        base.OnMouseDown(e);
    }
    protected override void OnMouseUp(MouseEventArgs e){if(drag){drag=false;Capture=false;Invalidate();}base.OnMouseUp(e);}
}
sealed class WheelFilter:IMessageFilter {
    readonly ScrollHost host;
    public WheelFilter(ScrollHost host){this.host=host;}
    public bool PreFilterMessage(ref Message m){
        if(m.Msg!=0x020A||!host.IsHandleCreated||!host.Visible)return false;
        if(!host.RectangleToScreen(host.ClientRectangle).Contains(Control.MousePosition))return false;
        int delta=(short)((long)m.WParam>>16);host.ScrollBy(delta>0?-80:80);return true;
    }
}

sealed class UiProgress:Control {
    int P(int value){return UiTheme.Pixels(this,value);}

    int value;ProgressBarStyle style=ProgressBarStyle.Blocks;
    public UiProgress(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=UiTheme.Background;TabStop=false;}
    public int Value{get{return value;}set{int v=Math.Max(0,Math.Min(100,value));if(v==this.value)return;this.value=v;Invalidate();}}
    public ProgressBarStyle Style{get{return style;}set{if(style==value)return;style=value;Invalidate();}}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
        int h=P(4),y=Math.Max(0,(Height-h)/2),w=Math.Max(1,Width-1);
        UiTheme.Fill(g,new Rectangle(0,y,w,h),2,UiTheme.ProgressTrack);
        if(value>0){
            int fill=Math.Max(P(4),(int)(w*value/100.0));
            using(var brush=new LinearGradientBrush(new Rectangle(0,0,Math.Max(2,w),Math.Max(2,h+1)),Color.FromArgb(96,140,250),UiTheme.Accent,0f))using(var path=UiTheme.Round(new Rectangle(0,y,Math.Min(w,fill),h),2))g.FillPath(brush,path);
        }
    }
}

sealed class UiListView:ListView {
    [DllImport("user32.dll")] static extern bool ShowScrollBar(IntPtr hWnd,int wBar,bool bShow);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out GridRect rect);
    [StructLayout(LayoutKind.Sequential)]struct GridRect {public int Left,Top,Right,Bottom;}
    public UiListView(){SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);DoubleBuffered=true;}
    public void HideHorizontalScrollBar(){if(IsHandleCreated)ShowScrollBar(Handle,0,false);}
    protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);HideHorizontalScrollBar();Invalidate();}
    void DrawBackgroundGrid(Graphics graphics){
        if(View!=View.Details||Columns.Count==0||ClientSize.Width<2||ClientSize.Height<2)return;
        var header=SendMessage(Handle,0x101F,IntPtr.Zero,IntPtr.Zero);GridRect bounds;int top=0;
        if(header!=IntPtr.Zero&&GetWindowRect(header,out bounds))top=PointToClient(new Point(bounds.Left,bounds.Bottom)).Y;
        int rowHeight=SmallImageList==null?Font.Height+8:SmallImageList.ImageSize.Height+2,first=top;
        if(Items.Count>0){var item=TopItem;if(item!=null){var row=item.Bounds;rowHeight=Math.Max(1,row.Height);first=row.Top;}}
        rowHeight=Math.Max(1,rowHeight);
        var state=graphics.Save();graphics.SetClip(ClientRectangle);
        using(var pen=new Pen(UiTheme.Line,1F)){
            int x=0;graphics.DrawLine(pen,0,0,0,ClientSize.Height-1);
            foreach(ColumnHeader column in Columns){x+=column.Width;if(x>=ClientSize.Width)break;graphics.DrawLine(pen,x-1,0,x-1,ClientSize.Height-1);}
            graphics.DrawLine(pen,ClientSize.Width-1,0,ClientSize.Width-1,ClientSize.Height-1);
            graphics.DrawLine(pen,0,Math.Max(0,top-1),ClientSize.Width-1,Math.Max(0,top-1));
            for(int y=first+rowHeight-1;y<ClientSize.Height;y+=rowHeight)if(y>=top)graphics.DrawLine(pen,0,y,ClientSize.Width-1,y);
            graphics.DrawLine(pen,0,ClientSize.Height-1,ClientSize.Width-1,ClientSize.Height-1);
        }
        graphics.Restore(state);
    }
    protected override void WndProc(ref Message message){
        base.WndProc(ref message);
        // Native ListView only draws item rows. Extend the grid through empty
        // space too, including the printing path used by accessibility/previews.
        if(message.Msg==0xF&&IsHandleCreated){using(var graphics=Graphics.FromHwnd(Handle))DrawBackgroundGrid(graphics);}
        else if((message.Msg==0x317||message.Msg==0x318)&&message.WParam!=IntPtr.Zero){using(var graphics=Graphics.FromHdc(message.WParam))DrawBackgroundGrid(graphics);}
    }
}
sealed class UiLogBox:TextBox {
    public event EventHandler ViewportChanged;
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int msg,IntPtr wp,IntPtr lp);
    public int FirstLine{get{return IsHandleCreated?SendMessage(Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32():0;}}
    public int LineCount{get{return IsHandleCreated?SendMessage(Handle,0xBA,IntPtr.Zero,IntPtr.Zero).ToInt32():1;}}
    public int VisibleLines{get{return Math.Max(1,ClientSize.Height/Math.Max(1,Font.Height));}}
    public void ScrollLines(int lines){if(IsHandleCreated)SendMessage(Handle,0xB6,IntPtr.Zero,new IntPtr(lines));}
    protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg==0x115||m.Msg==0x20A||m.Msg==0x101||m.Msg==0xC2||m.Msg==0xB6||m.Msg==0xB7){var changed=ViewportChanged;if(changed!=null)changed(this,EventArgs.Empty);}}
}
sealed class LogScrollBar:Control {
    int P(int value){return UiTheme.Pixels(this,value);}

    readonly UiLogBox box;bool dragging;int startY,startLine;
    public LogScrollBar(UiLogBox box){this.box=box;TabStop=false;Cursor=Cursors.Hand;BackColor=UiTheme.LogBackground;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);box.ViewportChanged+=(s,e)=>Invalidate();box.TextChanged+=(s,e)=>Invalidate();box.Resize+=(s,e)=>Invalidate();}
    int Maximum{get{return Math.Max(0,box.LineCount-box.VisibleLines);}}
    Rectangle Thumb{get{int height=Math.Max(24,(int)((long)Height*box.VisibleLines/Math.Max(1,box.LineCount)));height=Math.Min(Height,height);int top=Maximum==0?0:(int)((long)Math.Min(Maximum,box.FirstLine)*(Height-height)/Maximum);return new Rectangle(1,top,Math.Max(2,Width-2),height);}}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);if(Maximum==0)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;UiTheme.Fill(e.Graphics,Thumb,3,UiTheme.ScrollThumb);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left&&Maximum>0){if(Thumb.Contains(e.Location)){dragging=true;startY=e.Y;startLine=box.FirstLine;Capture=true;}else box.ScrollLines(e.Y<Thumb.Top?-box.VisibleLines:box.VisibleLines);}base.OnMouseDown(e);}
    protected override void OnMouseMove(MouseEventArgs e){if(dragging){int next=startLine+(int)((long)(e.Y-startY)*Maximum/Math.Max(1,Height-Thumb.Height));next=Math.Max(0,Math.Min(Maximum,next));box.ScrollLines(next-box.FirstLine);}base.OnMouseMove(e);}
    protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;base.OnMouseUp(e);}
}

sealed partial class MainForm {
    readonly Label elapsedLabel=new Label();
    readonly PillLabel canvasSummary=new PillLabel(),taskCount=new PillLabel();
    readonly NoticeLabel resolutionWarning=new NoticeLabel(),customValidation=new NoticeLabel{IsError=true};
    bool customCanvasInvalid;
    readonly Stopwatch batchClock=new Stopwatch();
    readonly System.Windows.Forms.Timer elapsedTimer=new System.Windows.Forms.Timer();
    Surface filesCard,settingsCard,logsCard;
    Panel headerPanel,statusPanel,settingsContent,settingsActions,directoryPanel,optionsPanel,modernPanel;
    ScrollHost settingsViewport;
    Control engineField,resolutionRow,outputRow,brightnessRow;
    InputShell outputShell,languageShell,fontDirectoryShell,dimShell,customWidthShell,customHeightShell;
    WellPanel logWell;
    int hotRow=-1;
    bool uiReady,layingOut;
    static Label UiLabel(string text,bool muted){return new Label{Text=text,AutoSize=false,ForeColor=muted?UiTheme.Muted:UiTheme.Body,TextAlign=ContentAlignment.MiddleLeft,BackColor=UiTheme.Surface,Font=UiTheme.BaseFont};}
    static ActionButton UiButton(string text,Action action,bool primary){var b=new ActionButton{Text=text,Primary=primary};b.Click+=(s,e)=>action();return b;}
    static int FieldHeight(Control control){return UiTheme.Pixels(control,32);}
    static Panel Field(string caption,Control control){
        var p=new Panel{BackColor=UiTheme.Surface};control.AccessibleName=caption;var shell=control as InputShell;if(shell!=null)shell.Inner.AccessibleName=caption;var label=UiLabel(caption,false);label.Font=UiTheme.LabelFont;p.Controls.Add(label);p.Controls.Add(control);
        p.Layout+=(s,e)=>{
            Func<int,int> u=n=>UiTheme.Pixels(p,n);int labelHeight=Math.Max(u(22),label.Font.Height+u(4));
            if(p.Width>=u(UiTheme.FieldInlineWidth)){
                int labelWidth=Math.Min(Math.Max(u(68),TextRenderer.MeasureText(caption,label.Font).Width+u(14)),Math.Max(u(68),p.Width/2));
                label.SetBounds(0,0,labelWidth,p.Height);int h=Math.Min(FieldHeight(control),Math.Max(u(12),p.Height));
                control.SetBounds(labelWidth+u(2),Math.Max(0,(p.Height-h)/2),Math.Max(u(24),p.Width-labelWidth-u(2)),h);
            }else{label.SetBounds(0,0,p.Width,labelHeight);control.SetBounds(0,labelHeight+u(6),p.Width,Math.Max(u(24),p.Height-labelHeight-u(6)));}
        };return p;
    }
    static Panel Pair(Control left,Control right){var p=new Panel{BackColor=UiTheme.Surface};p.Controls.Add(left);p.Controls.Add(right);p.Layout+=(s,e)=>{int gap=UiTheme.Pixels(p,16),width=Math.Max(0,(p.Width-gap)/2);left.SetBounds(0,0,width,p.Height);right.SetBounds(width+gap,0,Math.Max(0,p.Width-width-gap),p.Height);};return p;}
    InputShell Shell(TextBox box){box.BorderStyle=BorderStyle.None;return new InputShell(box);}
    InputShell Shell(NumericUpDown box){box.BorderStyle=BorderStyle.None;return new InputShell(box);}
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageW")]static extern IntPtr SetEditHint(IntPtr window,int message,IntPtr unused,string text);
    void BuildInterface(){
        SuspendLayout();AutoScaleMode=AutoScaleMode.None;Text="SupCraft · ASS → SUP / BDN v"+typeof(MainForm).Assembly.GetName().Version.ToString(typeof(MainForm).Assembly.GetName().Version.Build==0?2:3);Icon=Icon.ExtractAssociatedIcon(typeof(MainForm).Assembly.Location);Size=new Size(1280,840);MinimumSize=new Size(1040,800);StartPosition=FormStartPosition.CenterScreen;Font=UiTheme.BaseFont;ForeColor=UiTheme.Ink;BackColor=UiTheme.Background;AllowDrop=true;DoubleBuffered=true;
        outputShell=Shell(output);languageShell=Shell(language);fontDirectoryShell=Shell(fontDirectory);dimShell=Shell(dim);customWidthShell=Shell(customWidth);customHeightShell=Shell(customHeight);
        headerPanel=new Panel{BackColor=UiTheme.Background};var mark=new BrandMark{Name="brandMark",Bounds=new Rectangle(0,17,46,46)};headerPanel.Controls.Add(mark);
        var brand=UiLabel("SupCraft",false);brand.Name="brandTitle";brand.Font=UiTheme.MakeFont(23F,FontStyle.Bold);brand.BackColor=UiTheme.Background;
        int brandWidth=TextRenderer.MeasureText("SupCraft",brand.Font).Width;brand.SetBounds(60,4,brandWidth+12,50);headerPanel.Controls.Add(brand);
        var divider=new Panel{Name="brandDivider",BackColor=UiTheme.Divider};divider.SetBounds(60+brandWidth+18,31,1,22);headerPanel.Controls.Add(divider);
        var subtitle=UiLabel("ASS → SUP / BDN 字幕转换器",true);subtitle.Name="brandSubtitle";subtitle.Font=UiTheme.MakeFont(12F,FontStyle.Regular);subtitle.BackColor=UiTheme.Background;subtitle.SetBounds(60+brandWidth+32,18,460,44);headerPanel.Controls.Add(subtitle);
        Controls.Add(headerPanel);ConfigureChrome();
        filesCard=new Surface();settingsCard=new Surface();logsCard=new Surface();Controls.Add(filesCard);Controls.Add(settingsCard);Controls.Add(logsCard);
        var filesTitle=new SectionTitle{Text="任务与文件",Name="title"};filesCard.Controls.Add(filesTitle);taskCount.Text="0 个任务";taskCount.RightAligned=true;filesCard.Controls.Add(taskCount);
        var actions=new Panel{BackColor=UiTheme.Surface};fileActionsPanel=actions;var add=UiButton("＋ 添加 ASS",PickInput,true);remove.Text="移除";remove.Click+=(s,e)=>RemoveSelected();clear.Text="清空";clear.Click+=(s,e)=>ClearQueue();fontCheck.Text="字体管理";fontCheck.Click+=(s,e)=>CheckFonts();
        actions.Controls.AddRange(new Control[]{add,remove,clear,fontCheck});actions.Layout+=(s,e)=>{int gap=U(8),w=Math.Max(U(66),(actions.Width-3*gap)/4);var buttons=new[]{add,remove,clear,fontCheck};for(int i=0;i<buttons.Length;i++)buttons[i].SetBounds(i*(w+gap),0,w,actions.Height);};filesCard.Controls.Add(actions);
        queue.View=View.Details;queue.FullRowSelect=true;queue.GridLines=false;queue.HideSelection=false;queue.BorderStyle=BorderStyle.None;queue.OwnerDraw=true;queue.BackColor=UiTheme.Surface;queue.ForeColor=UiTheme.Ink;queue.HeaderStyle=ColumnHeaderStyle.Nonclickable;queue.Font=UiTheme.TableFont;queue.Columns.Add("状态",92);queue.Columns.Add("ASS 文件",340);queue.Columns.Add("输出 / 错误",260);var rowImages=new ImageList{ImageSize=new Size(1,34)};rowImages.Images.Add(new Bitmap(1,40));queue.SmallImageList=rowImages;
        queue.DrawColumnHeader+=(s,e)=>{using(var brush=new SolidBrush(UiTheme.Surface))e.Graphics.FillRectangle(brush,e.Bounds);using(var pen=new Pen(UiTheme.LineSoft,1f))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);UiTheme.Text(e.Graphics,e.Header.Text,ScaleFont(UiTheme.TableHeaderFont),new Rectangle(e.Bounds.X+U(12),e.Bounds.Y,Math.Max(U(10),e.Bounds.Width-U(18)),e.Bounds.Height),UiTheme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};
        queue.DrawItem+=(s,e)=>{};
        queue.DrawSubItem+=(s,e)=>{
            bool selected=e.Item.Selected;bool hot=!selected&&e.ItemIndex==hotRow;
            using(var brush=new SolidBrush(selected?UiTheme.RowSelected:hot?UiTheme.RowHover:UiTheme.Surface))e.Graphics.FillRectangle(brush,e.Bounds);
            using(var pen=new Pen(selected?UiTheme.AccentLine:UiTheme.LineSoft,1f))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);
            string text=e.SubItem.Text;var textRect=new Rectangle(e.Bounds.X+U(12),e.Bounds.Y,Math.Max(U(10),e.Bounds.Width-U(20)),e.Bounds.Height);
            if(e.ColumnIndex==0){
                Color foreground=StatusColor(text),background=StatusBackground(text);
                int pillWidth=Math.Min(Math.Max(U(42),TextRenderer.MeasureText(text,queue.Font).Width+U(20)),Math.Max(U(20),textRect.Width-U(4))),pillHeight=Math.Min(U(24),e.Bounds.Height-U(10));
                var pill=new Rectangle(textRect.X,e.Bounds.Y+(e.Bounds.Height-pillHeight)/2,pillWidth,pillHeight);
                e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var brush=new SolidBrush(background))using(var path=UiTheme.Round(pill,U(11)))e.Graphics.FillPath(brush,path);
                UiTheme.Text(e.Graphics,text,queue.Font,new Rectangle(pill.X+U(10),pill.Y,Math.Max(0,pill.Width-U(16)),pill.Height),foreground,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            }else{
                Color color=e.ColumnIndex==2&&text.Length>0&&e.Item.SubItems[0].Text=="失败"?UiTheme.Danger:UiTheme.Body;
                if(e.ColumnIndex==2&&text.Length==0)color=UiTheme.Faint;
                UiTheme.Text(e.Graphics,text,queue.Font,textRect,color,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            }
        };
        queue.SizeChanged+=(s,e)=>{int available=Math.Max(U(240),queue.ClientSize.Width-1);queue.Columns[0].Width=U(112);queue.Columns[1].Width=(int)((available-U(112))*.55);queue.Columns[2].Width=Math.Max(U(80),available-U(112)-queue.Columns[1].Width);queue.HideHorizontalScrollBar();queue.Invalidate();};
        queue.DoubleClick+=(s,e)=>ShowSelectedDetails();queue.MouseWheel+=(s,e)=>ClearQueuePathTip();queue.Leave+=(s,e)=>ClearQueuePathTip();
        queue.MouseMove+=(s,e)=>{UpdateQueuePathTip(e.Location);var item=queue.GetItemAt(e.X,e.Y);int index=item==null?-1:item.Index;if(index!=hotRow){hotRow=index;queue.Invalidate();}};
        queue.MouseLeave+=(s,e)=>{ClearQueuePathTip();if(hotRow!=-1){hotRow=-1;queue.Invalidate();}};
        filesCard.Controls.Add(queue);
        var dragHint=UiLabel("支持拖入多个 ASS 文件",true);dragHint.Name="dragHint";dragHint.Font=UiTheme.SmallFont;filesCard.Controls.Add(dragHint);
        directoryPanel=new Panel{BackColor=UiTheme.Surface};outputBrowse=UiButton("浏览…",PickOutput,false);fontBrowse=UiButton("浏览…",PickFontDirectory,false);var outputLabel=UiLabel("输出目录",false);var fontsLabel=UiLabel("字体目录（可选）",false);outputLabel.Font=UiTheme.LabelFont;fontsLabel.Font=UiTheme.LabelFont;
        directoryPanel.Controls.AddRange(new Control[]{outputLabel,outputShell,outputBrowse,fontsLabel,fontDirectoryShell,fontBrowse});
        directoryPanel.Paint+=(s,e)=>{using(var pen=new Pen(UiTheme.LineSoft,1f))e.Graphics.DrawLine(pen,0,0,directoryPanel.Width,0);};
        directoryPanel.Layout+=(s,e)=>{
            int row=U(32),gap=U(8),top=U(12),labelWidth=Math.Max(U(96),TextRenderer.MeasureText("字体目录（可选）",fontsLabel.Font).Width+U(12)),buttonWidth=U(76);
            int fieldX=labelWidth+U(10),fieldWidth=Math.Max(U(80),directoryPanel.Width-labelWidth-U(10)-buttonWidth-U(8));
            outputLabel.SetBounds(0,top,labelWidth,row);outputShell.SetBounds(fieldX,top,fieldWidth,row);outputBrowse.SetBounds(directoryPanel.Width-buttonWidth,top,buttonWidth,row);
            fontsLabel.SetBounds(0,top+row+gap,labelWidth,row);fontDirectoryShell.SetBounds(fieldX,top+row+gap,fieldWidth,row);fontBrowse.SetBounds(directoryPanel.Width-buttonWidth,top+row+gap,buttonWidth,row);
        };filesCard.Controls.Add(directoryPanel);
        output.HandleCreated+=(s,e)=>SetEditHint(output.Handle,0x1501,new IntPtr(1),"同源文件（留空：各 ASS 所在目录）");tips.SetToolTip(output,"留空时输出到每个 ASS 所在目录；选择目录后，所有任务统一输出到该目录。");output.AccessibleDescription="留空使用每个 ASS 所在目录";
        var settingsTitle=new SectionTitle{Text="转换设置",Name="title"};settingsCard.Controls.Add(settingsTitle);
        settingsViewport=new ScrollHost();settingsContent=settingsViewport.Content;settingsContent.BackColor=UiTheme.Surface;settingsCard.Controls.Add(settingsViewport);
        foreach(var combo in new[]{engine,resolution,fps,format}){combo.Font=UiTheme.BaseFont;combo.BackColor=UiTheme.Input;combo.ForeColor=UiTheme.Ink;}
        engine.Items.AddRange(new object[]{"AviSynth / avs2bdnxml 2.08","libass / ass2bdnxml / SUPer"});engine.SelectedIndex=1;engine.SelectedIndexChanged+=(s,e)=>UpdateEngineMode();resolution.SelectedIndexChanged+=(s,e)=>UpdateResolutionMode();fps.Items.AddRange(new object[]{"23.976","24","25","29.97","50","59.94"});fps.SelectedIndex=0;format.Items.AddRange(new object[]{"SUP","BDN XML + PNG","SUP + BDN"});format.SelectedIndex=0;format.SelectedIndexChanged+=(s,e)=>UpdateOutputOptions();language.Text="zho";language.Font=output.Font=fontDirectory.Font=dim.Font=customWidth.Font=customHeight.Font=UiTheme.BaseFont;
        engineField=Field("转换核心",engine);resolutionRow=Pair(Field("分辨率",resolution),Field("帧率 (fps)",fps));customWidth.Minimum=customHeight.Minimum=CustomCanvas.Minimum;customWidth.Maximum=customHeight.Maximum=CustomCanvas.Maximum;customWidth.Value=1440;customHeight.Value=3200;customSizeGroup=Pair(Field("宽度 (px)",customWidthShell),Field("高度 (px)",customHeightShell));customSizeGroup.Visible=false;
        customWidth.ValueChanged+=(s,e)=>UpdateCanvasNotice();customHeight.ValueChanged+=(s,e)=>UpdateCanvasNotice();customWidth.TextChanged+=(s,e)=>UpdateCanvasNotice();customHeight.TextChanged+=(s,e)=>UpdateCanvasNotice();customValidation.Visible=false;resolutionWarning.Visible=false;canvasSummary.LeadingDot=true;canvasSummary.Font=UiTheme.SmallFont;
        outputRow=Pair(Field("输出格式",format),Field("语言代码",languageShell));optionsPanel=Pair(new Panel{BackColor=UiTheme.Surface},new Panel{BackColor=UiTheme.Surface});autocrop.Text="自动裁剪";autocrop.Checked=true;palette.Text="调色板 PNG";palette.Checked=true;buffer.Text="PG 缓冲优化";buffer.Checked=true;strict.Text="严格 SUP 写出";
        optionsPanel.Controls[0].Controls.AddRange(new Control[]{autocrop,palette});optionsPanel.Controls[1].Controls.AddRange(new Control[]{buffer,strict});foreach(Control col in optionsPanel.Controls)col.Layout+=(s,e)=>{var c=(Control)s;c.Controls[0].SetBounds(0,0,c.Width,U(24));c.Controls[1].SetBounds(0,U(28),c.Width,U(24));};
        hdr.Text="HDR / BT.2020";gpu.Text="GPU 加速 (OpenCL)";modernPanel=new Panel{BackColor=UiTheme.Surface};dim.Minimum=0;dim.Maximum=100;dim.DecimalPlaces=1;dim.Value=33;dim.TextAlign=HorizontalAlignment.Right;brightnessRow=new Panel{BackColor=UiTheme.Surface};var dimLabel=UiLabel("亮度 %",true);dimLabel.Font=UiTheme.SmallFont;brightnessRow.Controls.AddRange(new Control[]{dimLabel,dimShell});brightnessRow.Layout+=(s,e)=>{int caption=U(76);dimLabel.SetBounds(0,0,caption,brightnessRow.Height);dimShell.SetBounds(caption,0,Math.Max(U(42),brightnessRow.Width-caption),brightnessRow.Height);};modernPanel.Controls.AddRange(new Control[]{hdr,brightnessRow,gpu});modernPanel.Layout+=(s,e)=>{int width=modernPanel.Width;if(width>=U(520)){int unit=(width-U(20))/3;hdr.SetBounds(0,0,unit,U(32));brightnessRow.SetBounds(unit+U(10),0,unit,U(32));gpu.SetBounds(2*(unit+U(10)),0,width-2*(unit+U(10)),U(32));}else{int half=(width-U(12))/2;hdr.SetBounds(0,0,half,U(24));gpu.SetBounds(half+U(12),0,width-half-U(12),U(24));brightnessRow.SetBounds(0,U(28),width,U(32));}};hdr.CheckedChanged+=(s,e)=>dim.Enabled=cts==null&&hdr.Checked&&engine.SelectedIndex==1;
        settingsContent.Controls.AddRange(new Control[]{engineField,resolutionRow,customSizeGroup,canvasSummary,customValidation,resolutionWarning,outputRow,optionsPanel,modernPanel});        settingsActions=new Panel{BackColor=UiTheme.Surface};settingsActions.Paint+=(s,e)=>{using(var pen=new Pen(UiTheme.LineSoft,1f))e.Graphics.DrawLine(pen,0,0,settingsActions.Width,0);};start.Text="开始批量转换";start.Click+=async(s,e)=>await ConvertAsync();cancel.Text="取消";cancel.Enabled=false;cancel.Click+=(s,e)=>Cancel();settingsActions.Controls.AddRange(new Control[]{start,cancel});settingsActions.Layout+=(s,e)=>{int cancelWidth=U(96),gap=U(12);start.SetBounds(0,3,Math.Max(U(40),settingsActions.Width-cancelWidth-gap),Math.Max(U(30),settingsActions.Height-U(6)));cancel.SetBounds(Math.Max(0,settingsActions.Width-cancelWidth),3,cancelWidth,Math.Max(U(30),settingsActions.Height-U(6)));};settingsCard.Controls.Add(settingsActions);
        statusPanel=new Panel{BackColor=UiTheme.Surface};progress.BackColor=UiTheme.Surface;status.Text="就绪 · 等待添加 ASS 文件";status.ForeColor=UiTheme.Muted;status.Font=UiTheme.LabelFont;status.AutoEllipsis=true;status.TextAlign=ContentAlignment.MiddleLeft;status.BackColor=UiTheme.Surface;elapsedLabel.ForeColor=UiTheme.Muted;elapsedLabel.Font=UiTheme.SmallFont;elapsedLabel.TextAlign=ContentAlignment.MiddleRight;elapsedLabel.BackColor=UiTheme.Surface;statusPanel.Controls.AddRange(new Control[]{status,progress,elapsedLabel});statusPanel.Paint+=(s,e)=>{
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;string text=status.Text??"";Color tone=text.IndexOf("失败",StringComparison.Ordinal)>=0?UiTheme.Danger:text.IndexOf("取消",StringComparison.Ordinal)>=0?UiTheme.Warning:text.IndexOf("完成",StringComparison.Ordinal)>=0?UiTheme.Success:UiTheme.Accent;
            using(var glow=new SolidBrush(Color.FromArgb(34,tone)))e.Graphics.FillEllipse(glow,0,status.Top+Math.Max(0,(status.Height-U(13))/2),U(10),U(10));
            using(var brush=new SolidBrush(tone))e.Graphics.FillEllipse(brush,U(2),status.Top+Math.Max(0,(status.Height-U(7))/2),U(7),U(7));
        };settingsCard.Controls.Add(statusPanel);status.TextChanged+=(s,e)=>{statusPanel.Invalidate();tips.SetToolTip(status,status.Text);};UpdateElapsed();elapsedTimer.Interval=250;elapsedTimer.Tick+=(s,e)=>UpdateElapsed();
        var logsTitle=new SectionTitle{Text="运行日志",Name="title"};logsCard.Controls.Add(logsTitle);var clearLog=UiButton("清空日志",()=>log.Clear(),false);clearLog.Name="clearLog";logsCard.Controls.Add(clearLog);log.Multiline=true;log.ReadOnly=true;log.WordWrap=true;log.ScrollBars=ScrollBars.None;log.BorderStyle=BorderStyle.None;log.Font=UiTheme.MonoFont;log.BackColor=UiTheme.LogBackground;log.ForeColor=UiTheme.LogText;logWell=new WellPanel();var logScroll=new LogScrollBar((UiLogBox)log);logWell.Controls.AddRange(new Control[]{log,logScroll});logWell.Layout+=(s,e)=>{log.SetBounds(U(12),U(10),Math.Max(U(10),logWell.Width-U(36)),Math.Max(U(10),logWell.Height-U(20)));logScroll.SetBounds(logWell.Width-U(14),U(10),U(8),Math.Max(U(10),logWell.Height-U(20)));};logsCard.Controls.Add(logWell);
        foreach(var button in new[]{remove,clear,fontCheck,start,cancel}){button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=0;button.Cursor=Cursors.Hand;}
        tips.SetToolTip(fontDirectory,"递归读取 TTF / OTF / TTC。兼容模式临时加载，现代模式直接读取。");tips.SetToolTip(customWidth,"64–4096 像素；宽×高须为 4 的倍数。自定义为 Beta 功能。");tips.SetToolTip(customHeight,tips.GetToolTip(customWidth));tips.SetToolTip(autocrop,"裁去透明空白，保留字幕在画布中的位置；兼容模式有效。");tips.SetToolTip(buffer,"分割字幕图形，降低 PGS 缓冲占用；兼容模式有效。");tips.SetToolTip(palette,"对适用的 BDN 输出生成索引 PNG。");tips.SetToolTip(strict,"兼容 SUP 写出时严格检查缓冲和调色板限制。");tips.SetToolTip(gpu,"仅现代 SUP 的相似度计算；设备不可用时行为由上游决定。");
        DragEnter+=(s,e)=>{if(cts==null&&e.Data.GetDataPresent(DataFormats.FileDrop)){e.Effect=DragDropEffects.Copy;filesCard.Dragging=true;}};
        DragLeave+=(s,e)=>{filesCard.Dragging=false;};
        DragDrop+=(s,e)=>{filesCard.Dragging=false;AddFiles(((string[])e.Data.GetData(DataFormats.FileDrop)).Where(x=>x.EndsWith(".ass",StringComparison.OrdinalIgnoreCase)));};
        FormClosing+=(s,e)=>{if(cts!=null){e.Cancel=true;closeAfterBatch=true;Cancel();}};FormClosed+=(s,e)=>{elapsedTimer.Stop();elapsedTimer.Dispose();batchClock.Stop();rowImages.Dispose();pathTip.Dispose();tips.Dispose();foreach(var font in scaledFonts.Values)font.Dispose();scaledFonts.Clear();};FontChanged+=(s,e)=>LayoutInterface();SizeChanged+=(s,e)=>LayoutInterface();settingsViewport.SizeChanged+=(s,e)=>LayoutSettings();Shown+=(s,e)=>{ApplyUiScale(WindowDpi(),null,true);LayoutInterface();Log("INFO","程序已启动","添加 ASS 文件并设置转换参数后即可开始。",null);};
        uiReady=true;UpdateEngineMode();ConfigureAppearance();ResumeLayout(false);LayoutInterface();
    }
    static Color StatusColor(string text){
        if(text=="完成")return UiTheme.Success;if(text=="失败")return UiTheme.Danger;if(text=="已取消")return UiTheme.Warning;if(text=="转换中"||text=="预检")return UiTheme.AccentDeep;return UiTheme.WaitFore;
    }
    static Color StatusBackground(string text){
        if(text=="完成")return UiTheme.SuccessSoft;if(text=="失败")return UiTheme.DangerSoft;if(text=="已取消")return UiTheme.WarningSoft;if(text=="转换中"||text=="预检")return UiTheme.AccentSoft;return UiTheme.WaitBack;
    }
    void LayoutInterface(){
        if(!uiReady||layingOut||applyingUiScale)return;layingOut=true;try{
            int margin=U(12),gap=U(10),header=U(64),statusHeight=U(56),w=Math.Max(U(800),ClientSize.Width-margin*2);
            // Keep settings at the default design size. Width changes go to the
            // task area; height changes go to the log below this fixed top row.
            int topHeight=U(552),right=U(579),left=w-gap-right,pad=U(16),line=U(26);
            headerPanel.SetBounds(margin,0,w,header);LayoutChrome();LayoutHeader();
            filesCard.SetBounds(margin,header,left,topHeight);settingsCard.SetBounds(margin+left+gap,header,right,topHeight);
            int logsTop=header+topHeight+gap;
            logsCard.SetBounds(margin,logsTop,w,Math.Max(U(100),ClientSize.Height-logsTop-margin));
            filesCard.Controls["title"].SetBounds(pad,U(14),Math.Max(U(60),left-2*pad-U(110)),line);taskCount.Text=queue.Items.Count+" 个任务";taskCount.SetBounds(left-pad-U(104),U(14),U(104),line);
            fileActionsPanel.SetBounds(pad,U(14)+line+U(10),left-2*pad,U(36));
            int dirs=U(12+32+8+32);directoryPanel.SetBounds(pad,topHeight-pad-dirs,left-2*pad,dirs);
            var dragHint=filesCard.Controls["dragHint"];dragHint.SetBounds(pad,directoryPanel.Top-U(26),left-2*pad,U(20));
            int tableTop=fileActionsPanel.Bottom+U(10);queue.SetBounds(pad,tableTop,left-2*pad,Math.Max(U(40),dragHint.Top-tableTop-U(6)));
            settingsCard.Controls["title"].SetBounds(pad,U(14),right-2*pad,line);
            settingsActions.SetBounds(pad,topHeight-pad-U(42),right-2*pad,U(42));
            statusPanel.SetBounds(pad,settingsActions.Top-U(12)-statusHeight,right-2*pad,statusHeight);
            int viewportTop=U(14)+line+U(10);settingsViewport.SetBounds(pad,viewportTop,right-2*pad,Math.Max(U(60),statusPanel.Top-U(12)-viewportTop));LayoutSettings();
            int timeWidth=Math.Max(U(140),TextRenderer.MeasureText("已运行 000:00:00",elapsedLabel.Font).Width+U(10));
            status.SetBounds(U(20),U(3),Math.Max(U(100),statusPanel.Width-timeWidth-U(26)),U(26));elapsedLabel.SetBounds(statusPanel.Width-timeWidth-U(4),U(3),timeWidth,U(26));progress.SetBounds(0,U(38),statusPanel.Width,U(8));
            logsCard.Controls["title"].SetBounds(pad,U(12),Math.Max(U(80),w-2*pad-U(100)),line);logsCard.Controls["clearLog"].SetBounds(w-pad-U(96),U(12),U(96),U(28));logWell.SetBounds(pad,U(12)+line+U(8),w-2*pad,Math.Max(U(40),logsCard.Height-(U(12)+line+U(8))-pad));
        }finally{layingOut=false;}
    }
    void LayoutSettings(){
        if(!uiReady||settingsViewport==null||applyingUiScale)return;
        int width=Math.Max(U(200),settingsViewport.UsableWidth),row=Math.Max(U(32),Font.Height+U(10)),gap=U(6),labelHeight=Math.Max(U(18),ScaleFont(UiTheme.LabelFont).Height+U(2)),stackHeight=row+labelHeight+U(4);
        bool pairInline=(width-U(16))/2>=U(UiTheme.FieldInlineWidth);int pairHeight=pairInline?row:stackHeight,fieldHeight=width>=U(UiTheme.FieldInlineWidth)?row:stackHeight,y=0;
        engineField.SetBounds(0,y,width,fieldHeight);y+=fieldHeight+gap;
        resolutionRow.SetBounds(0,y,width,pairHeight);y+=pairHeight+U(4);
        customSizeGroup.SetBounds(0,y,width,pairHeight);if(customSizeGroup.Visible)y+=pairHeight+U(4);
        canvasSummary.Visible=!IsCustomResolution();if(canvasSummary.Visible){canvasSummary.SetBounds(0,y,width,U(24));y+=U(26);}
        if(customValidation.Visible){int h=Math.Max(U(44),TextRenderer.MeasureText(customValidation.Text,customValidation.Font,new Size(Math.Max(U(80),width-U(30)),U(600)),TextFormatFlags.WordBreak).Height+U(18));customValidation.SetBounds(0,y+U(2),width,h);y=customValidation.Bottom+gap;}
        if(resolutionWarning.Visible){int h=Math.Max(U(44),TextRenderer.MeasureText(resolutionWarning.Text,resolutionWarning.Font,new Size(Math.Max(U(80),width-U(30)),U(600)),TextFormatFlags.WordBreak).Height+U(18));resolutionWarning.SetBounds(0,y+U(2),width,h);y=resolutionWarning.Bottom+gap;}else y+=gap-U(2);
        outputRow.SetBounds(0,y,width,pairHeight);y+=pairHeight+U(10);
        optionsPanel.SetBounds(0,y,width,U(52));if(engine.SelectedIndex==0)y+=U(52+4);
        int modernHeight=width>=U(520)?U(32):U(60);modernPanel.SetBounds(0,y,width,modernHeight);if(engine.SelectedIndex==1)y+=modernHeight+U(4);
        settingsViewport.Configure(width,y);
    }
    bool TryReadCustomCanvas(out int width,out int height,out string error){
        width=0;height=0;error="";
        if(!int.TryParse(customWidth.Text,NumberStyles.Integer,CultureInfo.CurrentCulture,out width)||!int.TryParse(customHeight.Text,NumberStyles.Integer,CultureInfo.CurrentCulture,out height)){error="请输入完整的整数宽度和高度。";return false;}
        if(width<CustomCanvas.Minimum||width>CustomCanvas.Maximum||height<CustomCanvas.Minimum||height>CustomCanvas.Maximum){error="宽度和高度须在 64–4096 像素之间。";return false;}
        if(width%4!=0||height%4!=0){string which=width%4!=0&&height%4!=0?"宽度和高度":width%4!=0?"宽度":"高度";error=which+"必须为 4 的倍数。\r\n当前输入："+width+" × "+height+"；请调整后再转换。";return false;}
        return true;
    }
    void UpdateCanvasNotice(){
        if(!uiReady)return;int w=0,h=0;string error="";bool custom=IsCustomResolution();customCanvasInvalid=custom&&!TryReadCustomCanvas(out w,out h,out error);
        if(!custom){var wh=Resolution();w=wh[0];h=wh[1];}canvasSummary.Text=w+" × "+h+" 像素";
        customValidation.Text=error;customValidation.Visible=customCanvasInvalid;
        int width,height;customWidthShell.Invalid=custom&&( !int.TryParse(customWidth.Text,out width)||width<64||width>4096||width%4!=0);customHeightShell.Invalid=custom&&(!int.TryParse(customHeight.Text,out height)||height<64||height>4096||height%4!=0);
        resolutionWarning.Text="非蓝光标准尺寸 · 软件播放用\r\n请确认目标播放器或制作工具支持该画布。";resolutionWarning.Visible=IsNonStandardCanvas();start.Enabled=cts==null&&!customCanvasInvalid;LayoutSettings();
    }
    static string FormatElapsed(TimeSpan time){return ((long)time.TotalHours).ToString("00",CultureInfo.InvariantCulture)+":"+time.Minutes.ToString("00",CultureInfo.InvariantCulture)+":"+time.Seconds.ToString("00",CultureInfo.InvariantCulture);}
    void UpdateElapsed(){elapsedLabel.Text="已运行 "+FormatElapsed(batchClock.Elapsed);}
    void StartElapsed(){batchClock.Restart();UpdateElapsed();elapsedTimer.Start();}
    void StopElapsed(){batchClock.Stop();elapsedTimer.Stop();UpdateElapsed();}
    internal void SelectPreviewResolution(bool custom){engine.SelectedIndex=0;resolution.SelectedIndex=custom?4:3;}
    internal void SelectInvalidPreview(){engine.SelectedIndex=0;resolution.SelectedIndex=4;customWidth.Text="1441";UpdateCanvasNotice();}
}
}
