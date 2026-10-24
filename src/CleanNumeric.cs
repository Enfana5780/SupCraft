using System;
using System.Drawing;
using System.Windows.Forms;

namespace StandaloneAssRocket {
// InputShell owns the arrows. Keep the framework's native arrow child out of
// the drawing area, and paint disabled values without a native edit/caret.
class CleanNumeric:NumericUpDown {
    bool arranging;
    public CleanNumeric(){BorderStyle=BorderStyle.None;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    internal void ArrangeEditor(){
        if(arranging)return;arranging=true;
        try{
            foreach(Control child in Controls){
                var edit=child as TextBoxBase;
                if(edit!=null){edit.Visible=Enabled;edit.BackColor=BackColor;edit.ForeColor=ForeColor;edit.SetBounds(0,0,ClientSize.Width,ClientSize.Height);}
                else{child.Visible=false;child.TabStop=false;if(child.Region==null)child.Region=new Region(Rectangle.Empty);}
            }
        }finally{arranging=false;}
    }
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ArrangeEditor();}
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);ArrangeEditor();}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);ArrangeEditor();Invalidate(true);}
    protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);ArrangeEditor();Invalidate(true);}
    protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(BackColor);
        if(!Enabled)UiTheme.Text(e.Graphics,Text,Font,new Rectangle(0,0,Math.Max(1,Width-3),Height),UiTheme.Faint,(TextAlign==HorizontalAlignment.Right?TextFormatFlags.Right:TextFormatFlags.Left)|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
}
}
