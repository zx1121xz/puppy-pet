using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PuppyPet
{
    /// <summary>
    /// 自测：行为大脑 + 矢量绘制。
    ///   PuppyPetTests.exe              跑全部断言
    ///   PuppyPetTests.exe --snapshot f.png   生成姿态总览图（开发时肉眼检查画法）
    /// </summary>
    static class SelfTest
    {
        static int _pass, _fail;
        static double _t;

        static void Section(string t) { Console.WriteLine(); Console.WriteLine(t); }

        static void Check(string name, bool ok) { Check(name, ok, null); }

        static void Check(string name, bool ok, string extra)
        {
            if (ok) { _pass++; Console.WriteLine("  [OK]   " + name); }
            else { _fail++; Console.WriteLine("  [FAIL] " + name + (extra == null ? "" : "  -> " + extra)); }
        }

        static DogBrain NewBrain(int seed)
        {
            return new DogBrain(new Rectangle(0, 0, 1920, 1080), seed);
        }

        static void Step(DogBrain b, double seconds, double dt)
        {
            double left = seconds;
            while (left > 0.0001)
            {
                double s = left < dt ? left : dt;
                _t += s;
                b.Update(_t, s);
                left -= s;
            }
        }

        static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            if (args.Length >= 2 && args[0] == "--snapshot")
            {
                Snapshot(args[1]);
                Console.WriteLine("已生成姿态总览图：" + args[1]);
                return 0;
            }

            if (args.Length >= 2 && args[0] == "--icon")
            {
                WriteIcon(args[1], new int[] { 16, 24, 32, 48, 64 });
                Console.WriteLine("已生成图标：" + args[1]);
                return 0;
            }

            Console.WriteLine("小黄狗 · 自测");

            // ---------------------------------------------------------- 行为
            Section("行为：初始状态");
            DogBrain b = NewBrain(1234);
            _t = 0;
            Check("站在地面上", Math.Abs(b.Y - b.GroundY) < 0.01f, "Y=" + b.Y + " ground=" + b.GroundY);
            Check("初始为待机", b.State == PetState.Idle, b.State.ToString());
            Check("初始不在空中", !b.Airborne);
            Check("命中测试：脚下能点到", b.HitTest(b.X, b.Y));
            Check("命中测试：远处点不到", !b.HitTest(b.X + 220f, b.Y));

            Section("行为：鼠标移上去 → 摸头 / 挠痒");
            _t = 0;
            b = NewBrain(7);
            b.OnHover(_t);
            Check("鼠标移入立刻变开心", b.State == PetState.Happy, b.State.ToString());
            Step(b, 1.0, 0.03);
            Check("心情值上升", b.Mood > 0.5f, "mood=" + b.Mood.ToString("0.00"));
            bool sawScratch = false, backToHappy = false, scratchSeen = false;
            for (int i = 0; i < 700; i++)
            {
                _t += 0.03;
                b.Update(_t, 0.03);
                if (b.State == PetState.Scratch) { sawScratch = true; scratchSeen = true; }
                if (scratchSeen && b.State == PetState.Happy) backToHappy = true;
            }
            Check("摸着摸着会挠痒痒", sawScratch);
            Check("挠痒之后还会回到摸头（来回切换）", backToHappy);
            Check("被摸期间不会睡着", b.State != PetState.Sleep, b.State.ToString());

            Section("行为：鼠标移开约 10 秒 → 偷偷跑掉");
            _t = 0;
            b = NewBrain(99);
            b.OnHover(_t);
            Step(b, 2.0, 0.03);
            float hoverX = b.X;
            b.OnMouseLeave(_t);
            Step(b, 5.0, 0.03);
            Check("移开后 5 秒还没跑（耐心等着被摸）", b.State != PetState.Sneak, b.State.ToString());
            Step(b, 6.5, 0.03);
            Check("移开后约 10 秒开始偷偷溜走", b.State == PetState.Sneak || b.IsMoving, b.State.ToString());

            bool sawSneak = false;
            float sneaked = 0f;
            for (int i = 0; i < 900; i++)      // 再看 27 秒
            {
                _t += 0.03;
                float before = b.X;
                b.Update(_t, 0.03);
                if (b.State == PetState.Sneak) { sawSneak = true; sneaked += Math.Abs(b.X - before); }
            }
            Check("确实偷偷挪动了一段距离", sawSneak && sneaked > 150f, "累计移动=" + sneaked.ToString("0"));

            Section("行为：长时间无人理 → 自己跑动但不越界");
            _t = 0;
            b = NewBrain(2024);
            bool inBounds = true;
            bool sawRun = false, sawSleep = false, sawSit = false;
            double minX = double.MaxValue, maxX = double.MinValue;
            for (int i = 0; i < 6000; i++)     // 180 秒
            {
                _t += 0.03;
                b.Update(_t, 0.03);
                if (b.State == PetState.Run) sawRun = true;
                if (b.State == PetState.Sleep) sawSleep = true;
                if (b.State == PetState.Sit) sawSit = true;
                if (b.X < 0 || b.X > 1920) inBounds = false;
                if (b.Y > b.GroundY + 0.5f) inBounds = false;
                if (b.X < minX) minX = b.X;
                if (b.X > maxX) maxX = b.X;
            }
            Check("180 秒内始终待在屏幕里", inBounds);
            Check("会自己跑起来玩", sawRun);
            Check("会自己坐下", sawSit);
            Check("会自己睡觉", sawSleep);
            Check("活动范围够大（不是原地抖）", maxX - minX > 300, "范围=" + (maxX - minX).ToString("0"));

            Section("行为：被拎起来 → 松手落地 → 抖毛");
            _t = 0;
            b = NewBrain(5);
            b.BeginDrag(_t);
            Check("按下左键进入被拎状态", b.State == PetState.Drag, b.State.ToString());
            b.DragTo(900, 300);
            Check("跟随鼠标移动", Math.Abs(b.X - 900) < 1 && Math.Abs(b.Y - 300) < 1, b.X + "," + b.Y);
            b.EndDrag(_t);
            Check("松手后开始下落", b.State == PetState.Fall && b.Airborne, b.State.ToString());
            Step(b, 3.0, 0.03);
            Check("最终落回地面", Math.Abs(b.Y - b.GroundY) < 1.5f, "Y=" + b.Y.ToString("0.0"));
            Check("落地后不再悬空", !b.Airborne);

            Section("行为：点一下会开心地跳");
            _t = 0;
            b = NewBrain(11);
            b.Poke(_t);
            Check("点击后离地起跳", b.Airborne, "airborne=" + b.Airborne);
            Check("点击后很开心", b.State == PetState.Happy && b.Mood > 0.9f);
            Step(b, 2.5, 0.03);
            Check("跳完落回地面", !b.Airborne && Math.Abs(b.Y - b.GroundY) < 1.5f);

            Section("行为：健壮性");
            _t = 0;
            b = NewBrain(3);
            float x0 = b.X;
            b.Update(0.033, 5.0);         // 模拟卡顿 5 秒
            Check("卡顿后不会瞬移出格", Math.Abs(b.X - x0) < 40f, "dx=" + (b.X - x0).ToString("0.0"));
            b.Paused = true;
            float xp = b.X, yp = b.Y;
            for (int i = 0; i < 100; i++) { _t += 0.03; b.Update(_t, 0.03); }
            Check("暂停后完全不动", Math.Abs(b.X - xp) < 0.001f && Math.Abs(b.Y - yp) < 0.001f);
            b.Paused = false;

            _t = 0;
            DogBrain b1 = NewBrain(42), b2 = NewBrain(42);
            for (int i = 0; i < 2000; i++)
            {
                _t += 0.03;
                b1.Update(_t, 0.03);
                b2.Update(_t, 0.03);
            }
            Check("相同种子 → 完全相同的轨迹（可复现）",
                Math.Abs(b1.X - b2.X) < 0.001f && b1.State == b2.State);

            Section("行为：命中测试");
            _t = 0;
            b = NewBrain(1);
            Check("中心命中", b.HitTest(b.X, b.Y));
            Check("头顶上方命中", b.HitTest(b.X, b.Y - 30f));
            Check("屏幕另一头不命中", !b.HitTest(b.X + 400f, b.Y));
            b.SetState(PetState.Sleep, _t);
            Check("睡着时判定范围变小", !b.HitTest(b.X, b.Y + 40f) && b.HitTest(b.X, b.Y));

            // ---------------------------------------------------------- 绘制
            Section("绘制：每个姿态都能画出来");
            string[] names = { "Idle", "Walk", "Run", "Sit", "Sleep", "Happy", "Scratch", "Sneak", "Drag", "Fall", "Shake" };
            PetState[] states = { PetState.Idle, PetState.Walk, PetState.Run, PetState.Sit, PetState.Sleep,
                                  PetState.Happy, PetState.Scratch, PetState.Sneak, PetState.Drag, PetState.Fall, PetState.Shake };
            bool allOk = true, allInk = true, allInside = true;
            string detail = "";
            for (int i = 0; i < states.Length; i++)
            {
                double cov, border;
                try
                {
                    Bitmap img = RenderPose(states[i], 0.6f, 1, out cov, out border);
                    img.Dispose();
                }
                catch (Exception ex)
                {
                    allOk = false; detail = names[i] + ": " + ex.Message; continue;
                }
                if (cov < 0.02 || cov > 0.65) { allInk = false; detail = names[i] + " 覆盖率=" + cov.ToString("0.00"); }
                if (border > 0.10) { allInside = false; detail = names[i] + " 贴边率=" + border.ToString("0.00"); }
            }
            Check("11 种姿态全部绘制成功且不抛异常", allOk, detail);
            Check("每种姿态都有合理的图形覆盖率(2%~65%)", allInk, detail);
            Check("图形没有大量溢出画布边缘", allInside, detail);

            double covR, edgeR, covL, edgeL;
            Bitmap flipA = RenderPose(PetState.Walk, 0.3f, 1, out covR, out edgeR);
            Bitmap flipB = RenderPose(PetState.Walk, 0.3f, -1, out covL, out edgeL);
            bool mirrored = Math.Abs(covR - covL) < 0.02;
            flipA.Dispose(); flipB.Dispose();
            Check("转身后图形面积基本一致（朝向镜像正常）",
                mirrored, "朝向右=" + covR.ToString("0.000") + " 朝向左=" + covL.ToString("0.000"));

            Console.WriteLine();
            Console.WriteLine("结果：" + _pass + " 通过，" + _fail + " 失败");
            return _fail == 0 ? 0 : 1;
        }

        static bool Measure(Bitmap b, out double coverage, out double border)
        {
            int w = b.Width, h = b.Height;
            int hit = 0, edge = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (b.GetPixel(x, y).A > 40)
                    {
                        hit++;
                        if (x < 2 || y < 2 || x >= w - 2 || y >= h - 2) edge++;
                    }
                }
            }
            coverage = (double)hit / (w * h);
            border = hit == 0 ? 0 : (double)edge / hit;
            return hit > 0;
        }

        static Bitmap RenderPose(PetState state, float t, int facing, out double coverage, out double border)
        {
            Bitmap bmp = new Bitmap(DogArt.CanvasW, DogArt.CanvasH, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                DogPose p = new DogPose();
                p.State = state;
                p.Phase = 0.3f;
                p.T = t;
                p.Facing = facing;
                p.Squash = 1f;
                p.Mood = state == PetState.Happy || state == PetState.Scratch ? 1f : 0.3f;
                p.Blink = false;
                p.Lift = state == PetState.Fall ? 40f : 0f;
                p.Airborne = state == PetState.Fall;
                DogArt.Draw(g, p);
            }
            Measure(bmp, out coverage, out border);
            return bmp;
        }

        // ---------------------------------------------------------- 生成 .ico
        /// <summary>把小狗画成多尺寸 32 位 ICO（BMP 条目，兼容性最好）</summary>
        static void WriteIcon(string path, int[] sizes)
        {
            List<byte[]> blobs = new List<byte[]>();
            foreach (int size in sizes) blobs.Add(IconImage(size));

            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                w.Write((ushort)0);                  // reserved
                w.Write((ushort)1);                  // type = icon
                w.Write((ushort)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0);                // 调色板数
                    w.Write((byte)0);                // reserved
                    w.Write((ushort)1);              // planes
                    w.Write((ushort)32);             // bpp
                    w.Write(blobs[i].Length);
                    w.Write(offset);
                    offset += blobs[i].Length;
                }
                foreach (byte[] b in blobs) w.Write(b);
            }
        }

        static byte[] IconImage(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    float k = size / 96f;                  // 取头部区域铺满
                    g.ScaleTransform(k, k);
                    g.TranslateTransform(-30f, -18f);
                    DogPose p = new DogPose();
                    p.State = PetState.Happy;
                    p.Facing = 1;
                    p.Squash = 1f;
                    p.Mood = 1f;
                    p.T = 0.4f;
                    DogArt.Draw(g, p);
                }

                int stride = size * 4;
                byte[] xor = new byte[stride * size];
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    for (int y = 0; y < size; y++)
                    {
                        // ICO 里的位图是自下而上存放的
                        IntPtr row = new IntPtr(data.Scan0.ToInt64() + (long)(size - 1 - y) * data.Stride);
                        Marshal.Copy(row, xor, y * stride, stride);
                    }
                }
                finally { bmp.UnlockBits(data); }

                int andStride = ((size + 31) / 32) * 4;
                byte[] and = new byte[andStride * size];   // 全 0：完全由 alpha 决定

                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter w = new BinaryWriter(ms))
                {
                    w.Write(40);                    // BITMAPINFOHEADER
                    w.Write(size); w.Write(size * 2);   // 高度 = XOR + AND
                    w.Write((ushort)1); w.Write((ushort)32);
                    w.Write(0); w.Write(xor.Length + and.Length);
                    w.Write(0); w.Write(0); w.Write(0); w.Write(0);
                    w.Write(xor);
                    w.Write(and);
                    w.Flush();
                    return ms.ToArray();
                }
            }
        }

        // ---------------------------------------------------------- 姿态总览图
        static void Snapshot(string path)
        {
            string[] names = { "Idle", "Walk", "Run", "Sit", "Sleep", "Happy", "Scratch", "Sneak", "Drag", "Fall", "Shake" };
            PetState[] states = { PetState.Idle, PetState.Walk, PetState.Run, PetState.Sit, PetState.Sleep,
                                  PetState.Happy, PetState.Scratch, PetState.Sneak, PetState.Drag, PetState.Fall, PetState.Shake };
            float[] phases = { 0f, 0.25f, 0.5f, 0.75f };
            int cols = phases.Length, rows = states.Length;
            int cw = DogArt.CanvasW, ch = DogArt.CanvasH, label = 20;
            int width = cols * cw, height = rows * (ch + label) + 26;

            using (Bitmap sheet = new Bitmap(width, height, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(sheet))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.FromArgb(255, 24, 20, 38));
                    using (Font title = new Font("Segoe UI", 10f, FontStyle.Bold))
                    using (Font small = new Font("Consolas", 9f))
                    using (SolidBrush white = new SolidBrush(Color.White))
                    using (SolidBrush grey = new SolidBrush(Color.FromArgb(255, 190, 185, 220)))
                    {
                        g.DrawString("PuppyPet poses  (columns: walk phase 0 / .25 / .5 / .75)", title, white, 6, 6);
                        for (int r = 0; r < rows; r++)
                        {
                            for (int c = 0; c < cols; c++)
                            {
                                int x = c * cw;
                                int y = 26 + r * (ch + label);
                                GraphicsState st = g.Save();
                                g.TranslateTransform(x, y + label);
                                DogPose p = new DogPose();
                                p.State = states[r];
                                p.Phase = phases[c];
                                p.T = 0.35f + c * 0.28f;
                                p.Facing = 1;
                                p.Squash = 1f;
                                p.Mood = (states[r] == PetState.Happy || states[r] == PetState.Scratch) ? 1f : 0.35f;
                                p.Lift = states[r] == PetState.Fall ? 46f : 0f;
                                p.Airborne = states[r] == PetState.Fall;
                                DogArt.Draw(g, p);
                                g.Restore(st);
                                g.FillRectangle(new SolidBrush(Color.FromArgb(30, 255, 255, 255)), x, y, cw - 1, label - 1);
                                g.DrawString(names[r] + "  φ=" + phases[c].ToString("0.00"), small, grey, x + 4, y + 3);
                            }
                        }
                    }
                }
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                sheet.Save(path, ImageFormat.Png);
            }
        }
    }
}
