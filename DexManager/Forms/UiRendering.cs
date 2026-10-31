using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DexManager.Forms
{
    internal static class UiRendering
    {
        private const int WmSetRedraw = 0x000B;
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr value, IntPtr other);

        public static IDisposable Freeze(Control control)
        {
            return new Batch(control);
        }

        private sealed class Batch : IDisposable
        {
            private readonly Control _control;
            public Batch(Control control)
            {
                _control = control;
                control.SuspendLayout();
                if (control.IsHandleCreated)
                    SendMessage(control.Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
            }
            public void Dispose()
            {
                if (_control.IsDisposed) return;
                _control.ResumeLayout(true);
                if (_control.IsHandleCreated)
                    SendMessage(_control.Handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
                _control.Invalidate(true);
            }
        }

        public static void ClipRounded(Control control, int radius)
        {
            // Binary window regions cut antialiased edge pixels. Painted corners
            // instead restore the exact parent background outside the curve.
            var previous=control.Region;
            if(previous!=null){control.Region=null;previous.Dispose();}
        }
    }

    // Native gutters are covered by a stable sibling control; geometry is never
    // toggled on hover or scroll. Both input paths use the same offset and painting.
    internal sealed class ThemedScrollBar : Control
    {
        [StructLayout(LayoutKind.Sequential)] private struct ScrollInfo {
            public int Size, Mask, Min, Max; public uint Page; public int Position, TrackPosition;
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
        [DllImport("user32.dll")] private static extern bool GetScrollInfo(IntPtr window,int bar,ref ScrollInfo info);
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr value,ref NativePoint point);
        private readonly Control _target;
        private readonly ScrollableControl _panel;
        private readonly RichTextBox _text;
        private readonly bool _horizontal;
        private readonly Observer _observer;
        private bool _updating, _queued;
        private int _dragStart, _dragOffset;
        private int _lastOffset=-1, _lastRange=-1, _lastTrack=-1, _lastColor;
        private int Thickness { get {return _horizontal?SystemInformation.HorizontalScrollBarHeight:SystemInformation.VerticalScrollBarWidth;} }
        private int TrackLength { get { return _horizontal?_target.ClientSize.Width:_target.ClientSize.Height; } }
        public ThemedScrollBar(Control target,bool horizontal=false) {
            _target=target;_panel=target as ScrollableControl;_text=target as RichTextBox;_horizontal=horizontal;
            TabStop=false;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            _observer=new Observer(this);
            target.ParentChanged+=delegate {if(target.Parent!=null){Parent=target.Parent;RefreshBounds();}};
            target.Layout+=delegate {RefreshBounds();};target.SizeChanged+=delegate {RefreshBounds();};target.VisibleChanged+=delegate {RefreshBounds();};
            if(_panel!=null)_panel.Scroll+=delegate {RefreshBounds();};
            if(_text!=null){_text.VScroll+=delegate {RefreshBounds();};_text.HScroll+=delegate {RefreshBounds();};_text.TextChanged+=delegate {QueueRefresh();};}
            target.HandleCreated+=delegate {_observer.AssignHandle(target.Handle);QueueRefresh();};
            target.Disposed+=delegate {if(_observer.Handle!=IntPtr.Zero)_observer.ReleaseHandle();Dispose();};
            if(target.IsHandleCreated)_observer.AssignHandle(target.Handle);
            if(target.Parent!=null)Parent=target.Parent;
        }
        private ScrollInfo Info { get {var info=new ScrollInfo {Size=Marshal.SizeOf(typeof(ScrollInfo)),Mask=7};if(_target.IsHandleCreated)GetScrollInfo(_target.Handle,_horizontal?0:1,ref info);return info;} }
        private int Range {get {if(_panel!=null){var metrics=_horizontal?(ScrollProperties)_panel.HorizontalScroll:_panel.VerticalScroll;return metrics.Visible?Math.Max(0,metrics.Maximum-metrics.LargeChange+1):0;}var i=Info;return Math.Max(0,i.Max-i.Min+1-(int)i.Page);}}
        private int Offset {get {if(_panel!=null)return Math.Max(0,_horizontal?-_panel.AutoScrollPosition.X:-_panel.AutoScrollPosition.Y);var i=Info;return Math.Max(0,i.Position-i.Min);}}
        private Rectangle Thumb {get {
            int length=Math.Max(1,TrackLength);int page=_panel!=null?TrackLength:(int)Info.Page;
            int size=Math.Min(length,Math.Max(28,(int)(length*(double)Math.Max(1,page)/Math.Max(1,page+Range))));
            int start=Range==0?0:(int)((length-size)*(double)Math.Min(Range,Offset)/Range);
            int center=(Thickness-4)/2;
            return _horizontal?new Rectangle(start,center,size,4):new Rectangle(center,start,4,size);
        }}
        private void QueueRefresh() {
            if(_queued||IsDisposed||_target.IsDisposed||!_target.IsHandleCreated)return;
            _queued=true;
            try {_target.BeginInvoke((Action)delegate {_queued=false;RefreshBounds();});}
            catch(InvalidOperationException){_queued=false;}
        }
        private sealed class Observer : NativeWindow {
            private readonly ThemedScrollBar _bar;
            public Observer(ThemedScrollBar bar){_bar=bar;}
            protected override void WndProc(ref Message message){base.WndProc(ref message);if(message.Msg==0xF||message.Msg==0x85||message.Msg==0x114||message.Msg==0x115||message.Msg==0x20A)_bar.QueueRefresh();}
        }
        private void RefreshBounds() {
            if(_updating||IsDisposed||_target.IsDisposed||_target.Parent==null)return;
            _updating=true;
            try {
                var bounds=_horizontal?new Rectangle(_target.Left,_target.Bottom-Thickness,_target.Width,Thickness):new Rectangle(_target.Right-Thickness,_target.Top,Thickness,_target.Height);
                int offset=Offset,range=Range,track=TrackLength,color=ThemeColors.Current.WindowBackground.ToArgb();
                bool emptyHorizontalGutter=_horizontal&&_panel!=null&&_target.Height-_target.ClientSize.Height>=Thickness;
                bool visible=_target.Visible&&(range>0||emptyHorizontalGutter);
                bool changed=Bounds!=bounds||Visible!=visible||offset!=_lastOffset||range!=_lastRange||track!=_lastTrack||color!=_lastColor;
                if(Bounds!=bounds)Bounds=bounds;
                Visible=visible;
                _lastOffset=offset;_lastRange=range;_lastTrack=track;_lastColor=color;
                if(visible && Parent.Controls.GetChildIndex(this)>Parent.Controls.GetChildIndex(_target))BringToFront();
                if(changed)Invalidate();
            } finally {_updating=false;}
        }
        protected override void OnPaint(PaintEventArgs e) {
            var theme=ThemeColors.Current;
            DesktopSurface.PaintBehind(e.Graphics,this);
            if(Range<=0)return;
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=DesktopSurface.RoundedPath(Thumb,2))using(var brush=new SolidBrush(theme.TextTertiary))e.Graphics.FillPath(brush,path);
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            if(e.Button!=MouseButtons.Left||Range<=0)return;
            int coordinate=_horizontal?e.X:e.Y;int start=_horizontal?Thumb.Left:Thumb.Top;
            if(!Thumb.Contains(e.Location))SetOffset(Offset+(coordinate<start?-TrackLength:TrackLength));
            _dragStart=coordinate;_dragOffset=Offset;Capture=true;
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            if(!Capture)return;
            int coordinate=_horizontal?e.X:e.Y;int size=_horizontal?Thumb.Width:Thumb.Height;
            SetOffset(_dragOffset+(int)((coordinate-_dragStart)*(double)Range/Math.Max(1,TrackLength-size)));
        }
        protected override void OnMouseUp(MouseEventArgs e){Capture=false;}
        private void SetOffset(int value) {
            value=Math.Max(0,Math.Min(Range,value));
            if(_panel!=null)_panel.AutoScrollPosition=new Point(_horizontal?value:Math.Max(0,-_panel.AutoScrollPosition.X),_horizontal?Math.Max(0,-_panel.AutoScrollPosition.Y):value);
            else if(_text!=null){var point=new NativePoint();SendMessage(_text.Handle,0x4DD,IntPtr.Zero,ref point);if(_horizontal)point.X=value;else point.Y=value;SendMessage(_text.Handle,0x4DE,IntPtr.Zero,ref point);}
            RefreshBounds();
        }
        public static void Attach(ScrollableControl target){new ThemedScrollBar(target);new ThemedScrollBar(target,true);}
        public static void Attach(RichTextBox target){new ThemedScrollBar(target);new ThemedScrollBar(target,true);}
        public static void RefreshFor(Control target){if(target.Parent==null)return;foreach(Control control in target.Parent.Controls){var bar=control as ThemedScrollBar;if(bar!=null&&ReferenceEquals(bar._target,target))bar.RefreshBounds();}}
    }

    internal sealed class BufferedFlowPanel : FlowLayoutPanel
    {
        public BufferedFlowPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnScroll(ScrollEventArgs e)
        {
            base.OnScroll(e);
            Invalidate(true);
        }
    }
    internal sealed class BufferedPanel : Panel
    {
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                // Windows 7 supports composition of child controls as one redraw.
                parameters.ExStyle |= 0x02000000;
                return parameters;
            }
        }
        public BufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnScroll(ScrollEventArgs e)
        {
            base.OnScroll(e);
            Invalidate(true);
        }
    }
}
