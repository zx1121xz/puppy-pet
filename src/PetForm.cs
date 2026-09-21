using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PuppyPet
{
    /// <summary>
    /// 桌宠窗口：分层窗口（逐像素透明）+ 定时器驱动的大脑 + 鼠标交互 + 托盘菜单。
    /// 全部绘制在内存位图上，通过 UpdateLayeredWindow 一次性贴到桌面。
    /// </summary>
    public class PetForm : Form
    {
        // ---------------------------------------------------------- Win32
        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int x; public int y; public POINT(int x, int y) { this.x = x; this.y = y; } }
        [StructLayout(LayoutKind.Sequential)]
        struct SIZE { public int cx; public int cy; public SIZE(int w, int h) { cx = w; cy = h; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BLENDFUNCTION { public byte BlendOp; public byte BlendFlags; public byte SourceConstantAlpha; public byte AlphaFormat; }

        const int WS_EX_LAYERED = 0x00080000;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const int ULW_ALPHA = 0x00000002;
        const byte AC_SRC_OVER = 0x00;
        const byte AC_SRC_ALPHA = 0x01;
        const int WM_NCHITTEST = 0x0084;
        const int HTTRANSPARENT = -1;

        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey,
            ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObj);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);

        // ---------------------------------------------------------- 状态
        readonly DogBrain _brain;
        readonly Timer _timer;
        readonly ContextMenuStrip _menu;
        readonly ToolStripMenuItem _miShow, _miPause, _miTop, _miAuto, _miFull;
        NotifyIcon _tray;
        IntPtr _trayIcon = IntPtr.Zero;

        Bitmap _bmp;
        Graphics _g;
        float _scale = 1f;
        int _bmpW, _bmpH;
        double _lastTime;
        bool _dragging;
        bool _movedWhileDown;
        Point _downAt;
        float _dragDx, _dragDy;
        int _fps = 30;

        public PetForm()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            _brain = new DogBrain(new Rectangle(wa.Left, wa.Top, wa.Width, wa.Height), Environment.TickCount & 0x7fff);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Text = "小黄狗";
            BackColor = Color.Black;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            _scale = Config.GetFloat("scale", 1f);
            _brain.Paused = Config.GetInt("paused", 0) == 1;
            _brain.FullScreen = Config.GetInt("fullscreen", 1) == 1;
            TopMost = Config.GetInt("topmost", 1) == 1;

            _timer = new Timer();
            _timer.Interval = 33;
            _timer.Tick += Tick;

            _menu = new ContextMenuStrip();
            _miShow = new ToolStripMenuItem("显示小狗", null, OnToggleShow);
            _miShow.Checked = true;
            _miPause = new ToolStripMenuItem("暂停活动", null, OnTogglePause);
            _miPause.Checked = _brain.Paused;

            ToolStripMenuItem miAct = new ToolStripMenuItem("让它做点什么");
            miAct.DropDownItems.Add(new ToolStripMenuItem("跑起来", null, delegate { _brain.Poke(Now); _brain.SetState(PetState.Run, Now); _brain.Hovering = false; _brain.StartSneak(Now); }));
            miAct.DropDownItems.Add(new ToolStripMenuItem("坐下", null, delegate { _brain.SetState(PetState.Sit, Now); }));
            miAct.DropDownItems.Add(new ToolStripMenuItem("睡觉", null, delegate { _brain.SetState(PetState.Sleep, Now); }));
            miAct.DropDownItems.Add(new ToolStripMenuItem("挠挠痒", null, delegate { _brain.SetState(PetState.Scratch, Now); }));

            ToolStripMenuItem miSize = new ToolStripMenuItem("大小");
            miSize.DropDownItems.Add(new ToolStripMenuItem("小", null, delegate { SetScale(0.8f); }));
            miSize.DropDownItems.Add(new ToolStripMenuItem("中", null, delegate { SetScale(1.0f); }));
            miSize.DropDownItems.Add(new ToolStripMenuItem("大", null, delegate { SetScale(1.35f); }));

            _miFull = new ToolStripMenuItem("满屏跑动", null, OnToggleFull);
            _miFull.Checked = _brain.FullScreen;
            _miTop = new ToolStripMenuItem("总在最前", null, OnToggleTop);
            _miTop.Checked = TopMost;
            _miAuto = new ToolStripMenuItem("开机自动启动", null, OnToggleAuto);
            _miAuto.Checked = AutoStart.IsOn();

            _menu.Items.Add(_miShow);
            _menu.Items.Add(_miPause);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(miAct);
            _menu.Items.Add(miSize);
            _menu.Items.Add(_miFull);
            _menu.Items.Add(_miTop);
            _menu.Items.Add(_miAuto);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem("关于小黄狗", null, OnAbout));
            _menu.Items.Add(new ToolStripMenuItem("退出", null, delegate { Close(); }));

            CreateTray();
            ResizeCanvas();
            _brain.DragTo(_brain.X, _brain.Y);
        }

        static double Now { get { return Environment.TickCount / 1000.0; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _lastTime = Now;
            SyncWindow();
            Render();
            _timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
                if (_trayIcon != IntPtr.Zero) DestroyIcon(_trayIcon);
                if (_g != null) _g.Dispose();
                if (_bmp != null) _bmp.Dispose();
            }
            base.Dispose(disposing);
        }

        // ---------------------------------------------------------- 主循环
        void Tick(object sender, EventArgs e)
        {
            double now = Now;
            double dt = now - _lastTime;
            _lastTime = now;
            if (dt < 0) dt = 0.033;

            UpdateHover(now);
            _brain.Update(now, dt);
            SyncWindow();
            Render();
            AdaptFps();
        }

        /// <summary>用全局光标位置判定悬停：窗口本身在移动，靠 Enter/Leave 事件不可靠</summary>
        void UpdateHover(double now)
        {
            if (_dragging) return;
            Point mp = Cursor.Position;
            bool on = _brain.HitTest(mp.X, mp.Y) && IsOpaque(mp);
            if (on && !_brain.Hovering) _brain.OnHover(now);
            else if (!on && _brain.Hovering) _brain.OnMouseLeave(now);
        }

        /// <summary>该屏幕坐标是否落在小狗的不透明像素上</summary>
        bool IsOpaque(Point screenPt)
        {
            if (_bmp == null) return true;
            int x = screenPt.X - Left;
            int y = screenPt.Y - Top;
            if (x < 0 || y < 0 || x >= _bmp.Width || y >= _bmp.Height) return false;
            return _bmp.GetPixel(x, y).A > 40;
        }

        void SyncWindow()
        {
            int w = _bmpW, h = _bmpH;
            float bodyCY = _brain.VisualY + (DogArt.CanvasH * 0.5f - 60f) * _scale;
            int left = (int)Math.Round(_brain.X - w * 0.5f);
            int top = (int)Math.Round(bodyCY - h * 0.5f);
            if (Left != left || Top != top) Location = new Point(left, top);

            // 落到屏幕外时拉回来
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            if (Top > wa.Bottom + 60 || Top < wa.Top - 300 || Left < wa.Left - 300 || Left > wa.Right + 300)
            {
                _brain.DragTo(wa.Left + wa.Width * 0.5f, wa.Bottom - 80);
                _brain.GoToGround(Now);
            }
        }

        void AdaptFps()
        {
            int want;
            if (_brain.Hovering || _brain.IsMoving || _brain.Airborne) want = 33;
            else if (_brain.State == PetState.Sleep) want = 160;
            else want = 66;
            if (want != _fps)
            {
                _fps = want;
                _timer.Interval = want;
            }
        }

        // ---------------------------------------------------------- 绘制
        void ResizeCanvas()
        {
            int w = Math.Max(60, (int)Math.Round(DogArt.CanvasW * _scale));
            int h = Math.Max(60, (int)Math.Round(DogArt.CanvasH * _scale));
            if (_bmp != null && _bmpW == w && _bmpH == h) return;
            if (_g != null) { _g.Dispose(); _g = null; }
            if (_bmp != null) { _bmp.Dispose(); _bmp = null; }
            _bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            _g = Graphics.FromImage(_bmp);
            _g.ScaleTransform(_scale, _scale);
            _bmpW = w;
            _bmpH = h;
        }

        void Render()
        {
            if (_bmp == null) return;
            _g.Clear(Color.Transparent);
            DogArt.Draw(_g, _brain.GetPose());

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = _bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr old = SelectObject(memDc, hBitmap);
            try
            {
                SIZE size = new SIZE(_bmpW, _bmpH);
                POINT src = new POINT(0, 0);
                POINT dst = new POINT(Left, Top);
                BLENDFUNCTION bf = new BLENDFUNCTION();
                bf.BlendOp = AC_SRC_OVER;
                bf.SourceConstantAlpha = 255;
                bf.AlphaFormat = AC_SRC_ALPHA;
                UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref bf, ULW_ALPHA);
            }
            finally
            {
                SelectObject(memDc, old);
                DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        // ---------------------------------------------------------- 鼠标
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                // 透明像素不挡鼠标：可以点到桌面图标
                POINT p = new POINT(m.LParam.ToInt32() & 0xFFFF, (m.LParam.ToInt32() >> 16) & 0xFFFF);
                if (p.x > 32767) p.x -= 65536;
                if (p.y > 32767) p.y -= 65536;
                if (!IsOpaque(new Point(p.x, p.y)))
                {
                    m.Result = (IntPtr)HTTRANSPARENT;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _movedWhileDown = false;
                _downAt = Cursor.Position;
                _dragDx = _brain.X - _downAt.X;
                _dragDy = _brain.Y - _downAt.Y;
                _brain.BeginDrag(Now);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;
            Point mp = Cursor.Position;
            if (Math.Abs(mp.X - _downAt.X) + Math.Abs(mp.Y - _downAt.Y) > 6) _movedWhileDown = true;
            _brain.DragTo(mp.X + _dragDx, mp.Y + _dragDy);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !_dragging) return;
            _dragging = false;
            if (_movedWhileDown) _brain.EndDrag(Now);
            else
            {
                _brain.Airborne = false;
                _brain.SetState(PetState.Idle, Now);
                _brain.Poke(Now);       // 点一下：开心地跳一下
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Right) _menu.Show(Cursor.Position);
        }

        // ---------------------------------------------------------- 菜单动作
        void SetScale(float s)
        {
            _scale = s;
            ResizeCanvas();
            Config.SetFloat("scale", s);
            SyncWindow();
            Render();
        }

        void OnToggleShow(object sender, EventArgs e)
        {
            _miShow.Checked = !_miShow.Checked;
            Visible = _miShow.Checked;
            if (_miShow.Checked) { _timer.Start(); } else { _timer.Stop(); }
            if (_tray != null) _tray.Visible = true;
        }

        void OnTogglePause(object sender, EventArgs e)
        {
            _brain.Paused = !_brain.Paused;
            _miPause.Checked = _brain.Paused;
            Config.SetInt("paused", _brain.Paused ? 1 : 0);
        }

        void OnToggleFull(object sender, EventArgs e)
        {
            _brain.FullScreen = !_brain.FullScreen;
            _miFull.Checked = _brain.FullScreen;
            Config.SetInt("fullscreen", _brain.FullScreen ? 1 : 0);
            if (!_brain.FullScreen && !_brain.OnGround) _brain.GoToGround(Now);
        }

        void OnToggleTop(object sender, EventArgs e)
        {
            TopMost = !TopMost;
            _miTop.Checked = TopMost;
            Config.SetInt("topmost", TopMost ? 1 : 0);
        }

        void OnToggleAuto(object sender, EventArgs e)
        {
            bool on = !AutoStart.IsOn();
            AutoStart.Set(on);
            _miAuto.Checked = AutoStart.IsOn();
        }

        void OnAbout(object sender, EventArgs e)
        {
            MessageBox.Show(
                "小黄狗 · 桌面宠物\n\n" +
                "· 鼠标移到它身上：摸摸头 / 挠痒痒\n" +
                "· 鼠标离开约 10 秒：它会偷偷跑掉去玩\n" +
                "· 左键按住可以把它拎起来\n" +
                "· 右键（或托盘图标）：菜单\n\n" +
                "纯 C# 绘制，无第三方依赖，退出请在托盘菜单选择「退出」。",
                "关于小黄狗", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Config.SetInt("paused", _brain.Paused ? 1 : 0);
            base.OnFormClosing(e);
        }

        // ---------------------------------------------------------- 托盘
        void CreateTray()
        {
            _tray = new NotifyIcon();
            _tray.Text = "小黄狗 · 桌面宠物";
            _tray.Icon = MakeIcon();
            _tray.ContextMenuStrip = _menu;
            _tray.Visible = true;
            _tray.DoubleClick += delegate { _miShow.Checked = true; Visible = true; _timer.Start(); };
        }

        Icon MakeIcon()
        {
            using (Bitmap b = new Bitmap(32, 32, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    g.ScaleTransform(0.24f, 0.24f);
                    g.TranslateTransform(-16f, -10f);
                    DogPose p = new DogPose();
                    p.State = PetState.Idle;
                    p.Facing = 1;
                    p.Squash = 1f;
                    p.Mood = 1f;
                    p.T = 0.5f;
                    DogArt.Draw(g, p);
                }
                IntPtr h = b.GetHicon();
                Icon icon = Icon.FromHandle(h);
                _trayIcon = h;
                return icon;
            }
        }
    }
}
