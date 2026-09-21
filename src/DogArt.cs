using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PuppyPet
{
    /// <summary>
    /// 小黄狗的矢量画法：所有零件都是圆角图形，不使用任何图片素材。
    /// 同一套骨架按 DogPose 变形，得到待机/走/跑/坐/睡/开心/挠痒/潜伏/被拎/下落/抖毛等姿态。
    ///
    /// 坐标约定（画布 132×120，脚底 y=112）：
    ///   尾巴(10~40)  后腿(42,52) 身体(26~90) 前腿(74,84) 脖子(78) 头(75~117) 口鼻(99~121)
    /// </summary>
    public static class DogArt
    {
        public const int CanvasW = 132;
        public const int CanvasH = 120;

        const float GroundY = 112f;

        // 身体
        const float BodyCX = 58f, BodyCY = 77f, BodyRX = 32f, BodyRY = 19f;
        // 头
        const float HeadCX = 96f, HeadCY = 50f, HeadR = 21f;
        // 腿
        const float LegTop = 86f;
        static readonly float[] HindX = { 42f, 53f };
        static readonly float[] FrontX = { 74f, 85f };

        // ---- 配色（治愈系暖黄）----
        static readonly Color FurTop = Color.FromArgb(255, 255, 228, 150);
        static readonly Color FurBottom = Color.FromArgb(255, 242, 172, 62);
        static readonly Color FurLight = Color.FromArgb(255, 255, 248, 226);
        static readonly Color FurDark = Color.FromArgb(255, 226, 158, 50);
        static readonly Color FurShade = Color.FromArgb(255, 214, 142, 40);
        static readonly Color Outline = Color.FromArgb(255, 178, 112, 26);
        static readonly Color Dark = Color.FromArgb(255, 74, 53, 32);
        static readonly Color TongueC = Color.FromArgb(255, 255, 143, 163);

        static readonly SolidBrush FurLightBrush = new SolidBrush(FurLight);
        static readonly SolidBrush FurDarkBrush = new SolidBrush(FurDark);
        static readonly SolidBrush FurShadeBrush = new SolidBrush(FurShade);
        static readonly SolidBrush DarkBrush = new SolidBrush(Dark);
        static readonly SolidBrush TongueBrush = new SolidBrush(TongueC);
        static readonly SolidBrush BlushBrush = new SolidBrush(Color.FromArgb(96, 255, 158, 177));
        static readonly SolidBrush ShadowBrush = new SolidBrush(Color.FromArgb(48, 60, 40, 20));
        static readonly SolidBrush HighlightBrush = new SolidBrush(Color.FromArgb(210, 255, 255, 255));
        static readonly Pen OutlinePen = new Pen(Outline, 1.7f);
        static readonly Pen ThinPen = new Pen(Outline, 1.2f);
        static readonly Pen EyePen = new Pen(Dark, 2.5f);
        static readonly Pen MouthPen = new Pen(Dark, 1.8f);
        static readonly LinearGradientBrush FurBrush;
        static readonly LinearGradientBrush HeadBrush;

        static DogArt()
        {
            OutlinePen.LineJoin = LineJoin.Round;
            OutlinePen.StartCap = LineCap.Round;
            OutlinePen.EndCap = LineCap.Round;
            ThinPen.LineJoin = LineJoin.Round;
            EyePen.StartCap = LineCap.Round;
            EyePen.EndCap = LineCap.Round;
            MouthPen.StartCap = LineCap.Round;
            MouthPen.EndCap = LineCap.Round;
            FurBrush = new LinearGradientBrush(new RectangleF(0, 52, 1, 60), FurTop, FurBottom, LinearGradientMode.Vertical);
            HeadBrush = new LinearGradientBrush(new RectangleF(0, 28, 1, 46), FurTop, FurBottom, LinearGradientMode.Vertical);
        }

        // ================================================================ 入口

        public static void Draw(Graphics g, DogPose pose)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            GraphicsState st = g.Save();
            if (pose.Facing < 0)
            {
                g.TranslateTransform(CanvasW, 0);
                g.ScaleTransform(-1, 1);
            }

            // 各姿态的整体位移/缩放
            float dy = 0f;
            float bodySquash = pose.Squash;
            float bodyRotate = 0f;
            if (pose.State == PetState.Sleep) { dy = 22f; bodySquash *= 1.02f; }
            else if (pose.State == PetState.Sit) { dy = 4f; bodyRotate = -11f; }
            else if (pose.State == PetState.Sneak) { dy = 7f; }
            else if (pose.State == PetState.Happy && pose.Airborne) { dy = -5f; }
            else if (pose.State == PetState.Drag || pose.State == PetState.Fall) { bodySquash = 1.05f; }

            if (pose.State == PetState.Shake) bodyRotate += (float)Math.Sin(pose.T * 34.0) * 7f;
            else if (pose.State == PetState.Scratch) bodyRotate += (float)Math.Sin(pose.T * 9.0) * 2.5f;

            g.TranslateTransform(0, dy);
            g.ScaleTransform(1f, bodySquash);
            if (bodyRotate != 0f)
            {
                g.TranslateTransform(BodyCX + 14f, GroundY - 16f);
                g.RotateTransform(bodyRotate);
                g.TranslateTransform(-(BodyCX + 14f), -(GroundY - 16f));
            }

            DrawShadow(g, pose);
            DrawTail(g, pose);
            DrawLegs(g, pose, true);
            DrawBody(g, pose);
            DrawLegs(g, pose, false);
            DrawHead(g, pose);
            DrawEffects(g, pose);

            g.Restore(st);
        }

        // ================================================================ 零件

        static void DrawShadow(Graphics g, DogPose pose)
        {
            float k = 1f - Math.Min(1f, pose.Lift / 150f) * 0.5f;
            float y = GroundY + 2f - pose.Lift * 0f;
            g.FillEllipse(ShadowBrush, BodyCX - 30f * k + 6f, y - 4f * k, 60f * k, 9f * k);
        }

        static void DrawTail(Graphics g, DogPose pose)
        {
            float wag;
            if (pose.State == PetState.Happy) wag = (float)Math.Sin(pose.T * 15.0) * 26f;
            else if (pose.State == PetState.Run) wag = (float)Math.Sin(pose.T * 12.0) * 18f;
            else if (pose.State == PetState.Sleep) wag = (float)Math.Sin(pose.T * 1.8) * 3f;
            else if (pose.State == PetState.Sneak) wag = (float)Math.Sin(pose.T * 6.0) * 6f;
            else wag = (float)Math.Sin(pose.T * 3.2) * 10f;

            float baseX = 40f, baseY = 76f;          // 起点藏在身体里，看起来是长在身上的
            float tipX = 12f, tipY = 46f;
            if (pose.State == PetState.Sleep) { baseY = 84f; tipX = 20f; tipY = 82f; }
            if (pose.State == PetState.Sneak) { tipX = 16f; tipY = 74f; }
            if (pose.State == PetState.Sit) { tipX = 14f; tipY = 60f; }

            GraphicsState st = g.Save();
            g.TranslateTransform(baseX, baseY);
            g.RotateTransform(wag);
            g.TranslateTransform(-baseX, -baseY);

            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddBezier(baseX, baseY, 24f, 68f, 12f, 58f, tipX, tipY);
                using (Pen pen = new Pen(FurShade, 10.5f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    g.DrawPath(pen, p);
                }
                using (Pen pen = new Pen(FurTop, 6.4f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    g.DrawPath(pen, p);
                }
            }
            g.Restore(st);
        }

        static void DrawBody(Graphics g, DogPose pose)
        {
            float cx = BodyCX, cy = BodyCY;
            float rx = BodyRX, ry = BodyRY;

            if (pose.State == PetState.Sleep)
            {
                // 趴着：身子压扁、拉长
                rx = 34f; ry = 15f; cy += 4f;
            }
            else if (pose.State == PetState.Sit)
            {
                rx = 29f; ry = 20f; cy += 2f;
            }
            else if (pose.State == PetState.Sneak)
            {
                ry = 16f; cy += 2f;
            }

            RectangleF r = new RectangleF(cx - rx, cy - ry, rx * 2f, ry * 2f);
            g.FillEllipse(FurBrush, r);

            // 肚子浅色
            g.FillEllipse(FurLightBrush, cx - 20f, cy + 1f, 36f, 15f);

            if (pose.State == PetState.Sit || pose.State == PetState.Sleep)
            {
                // 坐/趴时露出的后腿根
                g.FillEllipse(FurShadeBrush, cx - 28f, cy - 4f, 26f, 22f);
                g.FillEllipse(FurLightBrush, cx - 24f, cy + 2f, 14f, 10f);
            }

            g.DrawEllipse(OutlinePen, r);

            // 脖子：把头和身体连起来
            g.FillEllipse(FurBrush, 70f, 52f, 26f, 26f);
            g.FillEllipse(FurLightBrush, 76f, 62f, 16f, 14f);
        }

        static void DrawLegs(Graphics g, DogPose pose, bool hind)
        {
            float swing = 0f;
            if (pose.State == PetState.Walk) swing = (float)Math.Sin(pose.Phase * Math.PI * 2.0) * 8f;
            else if (pose.State == PetState.Run) swing = (float)Math.Sin(pose.Phase * Math.PI * 2.0) * 15f;
            else if (pose.State == PetState.Sneak) swing = (float)Math.Sin(pose.Phase * Math.PI * 2.0) * 5f;

            if (pose.State == PetState.Sleep)
            {
                // 趴着：前爪伸出来
                g.FillEllipse(FurLightBrush, 66f, GroundY - 16f, 26f, 15f);
                g.DrawEllipse(ThinPen, 66f, GroundY - 16f, 26f, 15f);
                return;
            }

            float[] xs = hind ? HindX : FrontX;
            for (int i = 0; i < 2; i++)
            {
                float x = xs[i];
                float s = (i == 0) ? swing : -swing;
                float footY = GroundY;
                bool far = (i == 0);

                if (pose.State == PetState.Drag || pose.State == PetState.Fall)
                {
                    footY = GroundY + 6f;
                    x += (i == 0 ? -5f : 5f);
                }
                else if (pose.State == PetState.Run || pose.State == PetState.Walk || pose.State == PetState.Sneak)
                {
                    footY -= Math.Max(0f, s) * 0.5f;
                }
                if (pose.State == PetState.Scratch && hind && i == 1)
                {
                    footY = HeadCY + 8f;      // 后腿抬起来挠耳朵
                    x += 12f;
                }

                using (GraphicsPath leg = new GraphicsPath())
                {
                    leg.AddLine(x + s * 0.15f, LegTop, x, footY - 4f);
                    using (Pen pen = new Pen(far ? FurShade : FurDark, far ? 10f : 11f))
                    {
                        pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                        g.DrawPath(pen, leg);
                    }
                    using (Pen pen = new Pen(FurTop, far ? 6f : 7f))
                    {
                        pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                        g.DrawPath(pen, leg);
                    }
                }
                // 小爪
                float pw = far ? 13f : 15f;
                g.FillEllipse(FurLightBrush, x - pw * 0.5f, footY - 8f, pw, 11f);
                g.DrawEllipse(ThinPen, x - pw * 0.5f, footY - 8f, pw, 11f);
            }
        }

        static void DrawEars(Graphics g, DogPose pose)
        {
            float flap;
            if (pose.State == PetState.Run) flap = -18f + (float)Math.Sin(pose.T * 14.0) * 6f;
            else if (pose.State == PetState.Walk) flap = (float)Math.Sin(pose.T * 7.0) * 7f;
            else if (pose.State == PetState.Happy) flap = (float)Math.Sin(pose.T * 12.0) * 12f - 8f;
            else if (pose.State == PetState.Scratch) flap = (float)Math.Sin(pose.T * 16.0) * 14f + 10f;
            else if (pose.State == PetState.Drag || pose.State == PetState.Fall) flap = -24f;
            else if (pose.State == PetState.Sneak) flap = 8f;
            else if (pose.State == PetState.Sleep) flap = 14f;
            else flap = (float)Math.Sin(pose.T * 1.7) * 4f;

            // 后耳（靠后、略深）
            Ear(g, 80f, 33f, 70f, 66f, 88f, 58f, flap * 0.55f, true);
            // 前耳
            Ear(g, 104f, 30f, 113f, 64f, 92f, 56f, flap, false);
        }

        static void Ear(Graphics g, float x1, float y1, float x2, float y2, float x3, float y3, float rotate, bool back)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(x1, y1);
            g.RotateTransform(rotate);
            g.TranslateTransform(-x1, -y1);

            using (GraphicsPath ear = new GraphicsPath())
            {
                ear.AddBezier(x1, y1, x1 - 4f, y1 + 20f, x2 - 3f, y2 - 14f, x2, y2);
                ear.AddBezier(x2, y2, x3 + 3f, y3 + 6f, x3 + 2f, y3 - 4f, x3, y3);
                ear.CloseFigure();
                g.FillPath(back ? FurShadeBrush : FurDarkBrush, ear);
                if (!back)
                {
                    using (GraphicsPath inner = new GraphicsPath())
                    {
                        inner.AddBezier(x1 + 4f, y1 + 5f, x1 + 1f, y1 + 17f, x2 - 4f, y2 - 13f, x2 - 2f, y2 - 6f);
                        inner.AddBezier(x2 - 2f, y2 - 6f, x3 + 1f, y3 + 3f, x3, y3 - 3f, x3 - 1f, y3 - 1f);
                        inner.CloseFigure();
                        g.FillPath(FurLightBrush, inner);
                    }
                }
                g.DrawPath(OutlinePen, ear);
            }
            g.Restore(st);
        }

        static void DrawHead(Graphics g, DogPose pose)
        {
            float hx = HeadCX, hy = HeadCY;
            if (pose.State == PetState.Sneak) { hy += 8f; hx += 3f; }
            if (pose.State == PetState.Sleep) { hy += 24f; hx -= 8f; }
            if (pose.State == PetState.Scratch) hy += 3f;
            if (pose.State == PetState.Happy) hy -= 1f;

            DrawEars(g, pose);

            RectangleF head = new RectangleF(hx - HeadR, hy - HeadR, HeadR * 2f, HeadR * 2f);
            g.FillEllipse(HeadBrush, head);
            g.FillEllipse(HighlightBrush, hx - 13f, hy - 16f, 17f, 10f);

            // 口鼻
            float mx = hx + 13f, my = hy + 8f;
            g.FillEllipse(FurLightBrush, mx - 12f, my - 8f, 24f, 17f);
            g.DrawEllipse(ThinPen, mx - 12f, my - 8f, 24f, 17f);

            Face(g, pose, hx, hy, mx, my);

            g.DrawEllipse(OutlinePen, head);
        }

        static void Face(Graphics g, DogPose pose, float hx, float hy, float mx, float my)
        {
            float eyeY = hy - 4f;
            float eye1 = hx - 7f, eye2 = hx + 7f;

            bool sleeping = pose.State == PetState.Sleep;
            bool happyEyes = pose.State == PetState.Happy || pose.State == PetState.Scratch;
            bool wide = pose.State == PetState.Drag || pose.State == PetState.Fall;

            if (sleeping || happyEyes)
            {
                Arc(g, eye1, eyeY);
                Arc(g, eye2, eyeY);
            }
            else if (pose.Blink)
            {
                g.DrawLine(EyePen, eye1 - 4f, eyeY + 2f, eye1 + 4f, eyeY + 2f);
                g.DrawLine(EyePen, eye2 - 4f, eyeY + 2f, eye2 + 4f, eyeY + 2f);
            }
            else
            {
                float r = wide ? 6.2f : 5.1f;
                g.FillEllipse(DarkBrush, eye1 - r * 0.5f, eyeY - r, r, r * 1.3f);
                g.FillEllipse(DarkBrush, eye2 - r * 0.5f, eyeY - r, r, r * 1.3f);
                g.FillEllipse(HighlightBrush, eye1 - 0.5f, eyeY - r + 1f, 2.3f, 2.3f);
                g.FillEllipse(HighlightBrush, eye2 - 0.5f, eyeY - r + 1f, 2.3f, 2.3f);
            }

            // 鼻子
            using (GraphicsPath nose = new GraphicsPath())
            {
                float nx = mx + 6f, ny = my - 4f;
                nose.AddBezier(nx - 4.5f, ny - 1.5f, nx + 4f, ny - 3.5f, nx + 4.5f, ny + 1f, nx, ny + 3.5f);
                nose.AddBezier(nx, ny + 3.5f, nx - 4.5f, ny + 1.5f, nx - 4.5f, ny - 1.5f, nx - 4.5f, ny - 1.5f);
                nose.CloseFigure();
                g.FillPath(DarkBrush, nose);
            }

            // 嘴
            if (pose.State == PetState.Happy || pose.State == PetState.Run)
            {
                using (GraphicsPath m = new GraphicsPath())
                {
                    m.AddBezier(mx - 6f, my + 1f, mx - 2f, my + 9f, mx + 5f, my + 9f, mx + 8f, my + 1f);
                    g.FillPath(DarkBrush, m);
                }
                g.FillEllipse(TongueBrush, mx - 1f, my + 5f, 8f, 8f);
            }
            else if (wide)
            {
                g.FillEllipse(DarkBrush, mx - 1f, my, 7f, 8f);
            }
            else
            {
                using (GraphicsPath m = new GraphicsPath())
                {
                    m.AddBezier(mx - 6f, my + 1f, mx - 2f, my + 7f, mx + 4f, my + 6f, mx + 7f, my + 1f);
                    g.DrawPath(MouthPen, m);
                }
            }

            // 腮红
            int a = (int)(70 + 110 * Math.Min(1f, pose.Mood));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 255, 150, 170)))
            {
                g.FillEllipse(b, hx - 17f, hy + 6f, 11f, 7f);
                g.FillEllipse(b, hx + 8f, hy + 8f, 10f, 6f);
            }
        }

        static void Arc(Graphics g, float x, float y)
        {
            using (GraphicsPath e = new GraphicsPath())
            {
                e.AddArc(x - 5.5f, y - 2f, 11f, 9f, 200f, 140f);
                g.DrawPath(EyePen, e);
            }
        }

        // ================================================================ 特效

        static void DrawEffects(Graphics g, DogPose pose)
        {
            if (pose.State == PetState.Happy)
            {
                for (int i = 0; i < 3; i++)
                {
                    float t = (pose.T * 0.85f + i * 0.33f) % 1f;
                    float x = BodyCX - 2f + i * 15f + (float)Math.Sin((pose.T + i) * 3.0) * 4f;
                    float y = 30f - t * 26f;
                    int a = (int)(215 * (1f - t));
                    if (a < 10) continue;
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 255, 122, 150)))
                    {
                        Heart(g, b, x, y, 6.2f);
                    }
                }
            }
            else if (pose.State == PetState.Sleep)
            {
                using (Font f = new Font("Segoe UI", 11f, FontStyle.Bold))
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float t = (pose.T * 0.34f + i * 0.33f) % 1f;
                        int a = (int)(195 * (1f - t));
                        if (a < 10) continue;
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 150, 190, 255)))
                        {
                            g.DrawString("z", f, b, 76f + i * 9f + t * 6f, 52f - t * 26f);
                        }
                    }
                }
            }
            else if (pose.State == PetState.Run || pose.State == PetState.Sneak)
            {
                for (int i = 0; i < 3; i++)
                {
                    float t = (pose.T * 2.2f + i * 0.33f) % 1f;
                    int a = (int)(120 * (1f - t));
                    if (a < 10) continue;
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 214, 198, 168)))
                    {
                        g.FillEllipse(b, 26f - t * 24f, GroundY - 5f - i * 4f, 9f * (1f - t * 0.4f), 5f * (1f - t * 0.4f));
                    }
                }
            }
            else if (pose.State == PetState.Shake)
            {
                for (int i = 0; i < 6; i++)
                {
                    float ang = i * 1.05f;
                    float d = 16f + pose.T * 44f;
                    int a = (int)(200 * Math.Max(0f, 1f - pose.T * 1.6f));
                    if (a < 10) continue;
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 165, 215, 255)))
                    {
                        g.FillEllipse(b, BodyCX + (float)Math.Cos(ang) * d, BodyCY + (float)Math.Sin(ang) * d * 0.55f, 4f, 4f);
                    }
                }
            }
            else if (pose.State == PetState.Scratch)
            {
                for (int i = 0; i < 3; i++)
                {
                    float k = (float)Math.Sin(pose.T * 18.0 + i) * 0.5f + 0.5f;
                    int alpha = (int)(60 + 150 * k);
                    using (Pen p = new Pen(Color.FromArgb(alpha, 150, 120, 60), 1.6f))
                    {
                        g.DrawLine(p, 88f + i * 4f, 26f + i * 5f, 97f + i * 4f, 20f + i * 5f);
                    }
                }
            }
        }

        static void Heart(Graphics g, Brush b, float x, float y, float s)
        {
            using (GraphicsPath h = new GraphicsPath())
            {
                h.AddBezier(x, y + s * 0.35f, x - s * 0.5f, y - s * 0.35f, x - s, y + s * 0.3f, x, y + s);
                h.AddBezier(x, y + s, x + s, y + s * 0.3f, x + s * 0.5f, y - s * 0.35f, x, y + s * 0.35f);
                h.CloseFigure();
                g.FillPath(b, h);
            }
        }
    }
}
