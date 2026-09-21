using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PuppyPet
{
    /// <summary>
    /// 小黄狗的矢量画法：所有零件都是圆角图形，不使用任何图片素材。
    ///
    /// 造型参考用户提供的小金毛幼犬照片：奶油金色的蓬松绒毛、又大又圆的黑色眼睛（带两点高光）、
    /// 小巧的黑色鼻头、小而垂的耳朵、短口鼻、四肢有黑色肉垫、整体圆润蓬松。
    ///
    /// 提供两套画法：
    ///   Draw()         —— 侧视图，按 DogPose 变形出 11 种姿态（桌宠主体）
    ///   DrawPortrait() —— 正面坐姿肖像（应用图标 / 展示图）
    /// </summary>
    public static class DogArt
    {
        public const int CanvasW = 132;
        public const int CanvasH = 120;

        const float GroundY = 112f;

        // 侧视图骨架
        const float BodyCX = 58f, BodyCY = 78f, BodyRX = 32f, BodyRY = 20f;
        const float HeadCX = 97f, HeadCY = 50f, HeadR = 22f;
        const float LegTop = 86f;
        static readonly float[] HindX = { 42f, 53f };
        static readonly float[] FrontX = { 74f, 85f };

        // ---- 配色：取自照片的奶油金 ----
        static readonly Color FurLightest = Color.FromArgb(255, 255, 249, 237);
        static readonly Color FurTop = Color.FromArgb(255, 249, 231, 191);
        static readonly Color FurMid = Color.FromArgb(255, 240, 209, 152);
        static readonly Color FurBottom = Color.FromArgb(255, 224, 187, 120);
        static readonly Color FurDark = Color.FromArgb(255, 205, 165, 100);
        static readonly Color FurShade = Color.FromArgb(255, 199, 158, 96);
        static readonly Color Outline = Color.FromArgb(235, 172, 134, 76);
        static readonly Color Dark = Color.FromArgb(255, 38, 27, 20);
        static readonly Color TongueC = Color.FromArgb(255, 246, 152, 165);

        static readonly SolidBrush FurLightestBrush = new SolidBrush(FurLightest);
        static readonly SolidBrush FurTopBrush = new SolidBrush(FurTop);
        static readonly SolidBrush FurMidBrush = new SolidBrush(FurMid);
        static readonly SolidBrush FurDarkBrush = new SolidBrush(FurDark);
        static readonly SolidBrush FurShadeBrush = new SolidBrush(FurShade);
        static readonly SolidBrush DarkBrush = new SolidBrush(Dark);
        static readonly SolidBrush TongueBrush = new SolidBrush(TongueC);
        static readonly SolidBrush ShadowBrush = new SolidBrush(Color.FromArgb(42, 60, 40, 20));
        static readonly SolidBrush HighlightBrush = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
        static readonly SolidBrush BlushBrush = new SolidBrush(Color.FromArgb(90, 246, 168, 160));
        static readonly Pen OutlinePen = new Pen(Outline, 1.5f);
        static readonly Pen ThinPen = new Pen(Outline, 1.1f);
        static readonly Pen EyePen = new Pen(Dark, 2.4f);
        static readonly Pen MouthPen = new Pen(Color.FromArgb(200, 120, 92, 62), 1.5f);
        static readonly LinearGradientBrush FurBrush;
        static readonly LinearGradientBrush HeadBrush;
        static readonly LinearGradientBrush PortraitBrush;

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
            FurBrush = new LinearGradientBrush(new RectangleF(0, 56, 1, 58), FurTop, FurBottom, LinearGradientMode.Vertical);
            HeadBrush = new LinearGradientBrush(new RectangleF(0, 26, 1, 48), FurLightest, FurMid, LinearGradientMode.Vertical);
            PortraitBrush = new LinearGradientBrush(new RectangleF(0, 24, 1, 70), FurLightest, FurMid, LinearGradientMode.Vertical);
        }

        // ================================================================ 侧视图

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

            float dy = 0f;
            float squash = pose.Squash;
            float rotate = 0f;
            if (pose.State == PetState.Sleep) { dy = 22f; }
            else if (pose.State == PetState.Sit) { dy = 4f; rotate = -10f; }
            else if (pose.State == PetState.Sneak) { dy = 7f; }
            else if (pose.State == PetState.Happy && pose.Airborne) { dy = -5f; }
            else if (pose.State == PetState.Drag || pose.State == PetState.Fall) { squash = 1.05f; }

            if (pose.State == PetState.Shake) rotate += (float)Math.Sin(pose.T * 34.0) * 7f;
            else if (pose.State == PetState.Scratch) rotate += (float)Math.Sin(pose.T * 9.0) * 2.5f;

            g.TranslateTransform(0, dy);
            g.ScaleTransform(1f, squash);
            if (rotate != 0f)
            {
                g.TranslateTransform(BodyCX + 14f, GroundY - 16f);
                g.RotateTransform(rotate);
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

        static void DrawShadow(Graphics g, DogPose pose)
        {
            if (pose.Lift > 24f) return;      // 悬在半空时地面上不该有影子
            float k = 1f - Math.Min(1f, pose.Lift / 150f) * 0.5f;
            g.FillEllipse(ShadowBrush, BodyCX - 24f * k, GroundY + 1f - 4f * k, 60f * k, 9f * k);
        }

        static void DrawTail(Graphics g, DogPose pose)
        {
            float wag;
            if (pose.State == PetState.Happy) wag = (float)Math.Sin(pose.T * 15.0) * 26f;
            else if (pose.State == PetState.Run) wag = (float)Math.Sin(pose.T * 12.0) * 18f;
            else if (pose.State == PetState.Sleep) wag = (float)Math.Sin(pose.T * 1.8) * 3f;
            else if (pose.State == PetState.Sneak) wag = (float)Math.Sin(pose.T * 6.0) * 6f;
            else wag = (float)Math.Sin(pose.T * 3.2) * 10f;

            float baseX = 40f, baseY = 78f;
            float tipX = 12f, tipY = 48f;
            if (pose.State == PetState.Sleep) { baseY = 86f; tipX = 20f; tipY = 84f; }
            if (pose.State == PetState.Sneak) { tipX = 16f; tipY = 76f; }
            if (pose.State == PetState.Sit) { tipX = 14f; tipY = 62f; }

            GraphicsState st = g.Save();
            g.TranslateTransform(baseX, baseY);
            g.RotateTransform(wag);
            g.TranslateTransform(-baseX, -baseY);

            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddBezier(baseX, baseY, 24f, 70f, 12f, 60f, tipX, tipY);
                using (Pen pen = new Pen(FurDark, 12f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    g.DrawPath(pen, p);
                }
                using (Pen pen = new Pen(FurTop, 7.5f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    g.DrawPath(pen, p);
                }
                // 尾巴尖的一撮白毛
                g.FillEllipse(FurLightestBrush, tipX - 6f, tipY - 6f, 12f, 12f);
            }
            g.Restore(st);
        }

        static void DrawBody(Graphics g, DogPose pose)
        {
            float cx = BodyCX, cy = BodyCY;
            float rx = BodyRX, ry = BodyRY;

            if (pose.State == PetState.Sleep) { rx = 34f; ry = 16f; cy += 4f; }
            else if (pose.State == PetState.Sit) { rx = 29f; ry = 21f; cy += 2f; }
            else if (pose.State == PetState.Sneak) { ry = 17f; cy += 2f; }

            // 蓬松的轮廓：先铺一层小圆弧，再盖上主体
            Fluff(g, FurTopBrush, cx, cy, rx, ry, 9, 150f, 240f);
            Fluff(g, FurTopBrush, cx, cy, rx, ry, 5, 30f, 120f);

            RectangleF r = new RectangleF(cx - rx, cy - ry, rx * 2f, ry * 2f);
            g.FillEllipse(FurBrush, r);

            // 胸口与肚子的浅色绒毛
            g.FillEllipse(FurLightestBrush, cx - 21f, cy + 1f, 38f, 16f);
            Fluff(g, FurLightestBrush, cx - 2f, cy + 8f, 19f, 8f, 6, 150f, 240f);

            if (pose.State == PetState.Sit || pose.State == PetState.Sleep)
            {
                g.FillEllipse(FurShadeBrush, cx - 28f, cy - 4f, 26f, 22f);
                g.FillEllipse(FurLightestBrush, cx - 24f, cy + 2f, 14f, 10f);
            }

            g.DrawEllipse(OutlinePen, r);

            // 脖子
            g.FillEllipse(FurBrush, 70f, 50f, 28f, 28f);
            g.FillEllipse(FurLightestBrush, 76f, 62f, 17f, 15f);
        }

        /// <summary>沿椭圆边缘铺一圈小圆，做出蓬松的绒毛轮廓</summary>
        static void Fluff(Graphics g, Brush b, float cx, float cy, float rx, float ry, int count, float startDeg, float spreadDeg)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (startDeg + spreadDeg * i / (count - 1f)) * (float)Math.PI / 180f;
                float x = cx + (float)Math.Cos(a) * rx;
                float y = cy + (float)Math.Sin(a) * ry;
                float r = 5.5f + (i % 3) * 1.2f;
                g.FillEllipse(b, x - r, y - r, r * 2f, r * 2f);
            }
        }

        static void DrawLegs(Graphics g, DogPose pose, bool hind)
        {
            float swing = 0f;
            if (pose.State == PetState.Hover) swing = (float)Math.Sin(pose.T * 7.0 + (hind ? 0f : 1.6f)) * 7f;
            else if (pose.State == PetState.Walk) swing = (float)Math.Sin(pose.Phase * Math.PI * 2.0) * 8f;
            else if (pose.State == PetState.Run) swing = (float)Math.Sin(pose.Phase * Math.PI * 2.0) * 15f;
            else if (pose.State == PetState.Sneak) swing = (float)Math.Sin(pose.Phase * Math.PI * 2.0) * 5f;

            if (pose.State == PetState.Sleep)
            {
                g.FillEllipse(FurLightestBrush, 64f, GroundY - 16f, 30f, 16f);
                g.DrawEllipse(ThinPen, 64f, GroundY - 16f, 30f, 16f);
                PawPads(g, 79f, GroundY - 3f, 1f);
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
                else if (pose.State == PetState.Hover)
                {
                    // 空中踩空气：腿一伸一缩
                    footY = GroundY - 2f + (float)Math.Sin(pose.T * 7.0 + i * 1.6f) * 5f;
                }
                else if (pose.State == PetState.Run || pose.State == PetState.Walk || pose.State == PetState.Sneak)
                {
                    footY -= Math.Max(0f, s) * 0.5f;
                }
                if (pose.State == PetState.Scratch && hind && i == 1)
                {
                    footY = HeadCY + 8f;
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
                    using (Pen pen = new Pen(far ? FurTop : FurLightest, far ? 6f : 7.4f))
                    {
                        pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                        g.DrawPath(pen, leg);
                    }
                }
                float pw = far ? 13f : 15f;
                g.FillEllipse(FurLightestBrush, x - pw * 0.5f, footY - 8f, pw, 11f);
                g.DrawEllipse(ThinPen, x - pw * 0.5f, footY - 8f, pw, 11f);
                if (!far) PawPads(g, x, footY - 1f, 1f);
            }
        }

        /// <summary>黑色肉垫（照片里小爪子上的黑点）</summary>
        static void PawPads(Graphics g, float x, float y, float scale)
        {
            g.FillEllipse(DarkBrush, x - 3.1f * scale, y - 3.4f * scale, 6.2f * scale, 5.2f * scale);
            for (int i = -1; i <= 1; i++)
            {
                g.FillEllipse(DarkBrush, x + i * 4.2f * scale - 1.3f * scale, y - 7.2f * scale, 2.6f * scale, 2.6f * scale);
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
            else if (pose.State == PetState.Hover) flap = -14f + (float)Math.Sin(pose.T * 9.0) * 8f;
            else if (pose.State == PetState.Sneak) flap = 8f;
            else if (pose.State == PetState.Sleep) flap = 14f;
            else flap = (float)Math.Sin(pose.T * 1.7) * 4f;

            // 照片里的耳朵小而圆，垂在头两侧
            Ear(g, 80f, 32f, 68f, 64f, 90f, 54f, flap * 0.55f, true);
            Ear(g, 108f, 30f, 116f, 62f, 94f, 52f, flap, false);
        }

        static void Ear(Graphics g, float x1, float y1, float x2, float y2, float x3, float y3, float rotate, bool back)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(x1, y1);
            g.RotateTransform(rotate);
            g.TranslateTransform(-x1, -y1);

            using (GraphicsPath ear = new GraphicsPath())
            {
                ear.AddBezier(x1, y1, x1 - 6f, y1 + 16f, x2 - 5f, y2 - 12f, x2, y2);
                ear.AddBezier(x2, y2, x3 + 4f, y3 + 5f, x3 + 3f, y3 - 4f, x3, y3);
                ear.CloseFigure();
                g.FillPath(back ? FurShadeBrush : FurDarkBrush, ear);
                if (!back)
                {
                    using (GraphicsPath inner = new GraphicsPath())
                    {
                        inner.AddBezier(x1 + 4f, y1 + 4f, x1 + 1f, y1 + 14f, x2 - 5f, y2 - 11f, x2 - 3f, y2 - 5f);
                        inner.AddBezier(x2 - 3f, y2 - 5f, x3 + 1f, y3 + 2f, x3 - 1f, y3 - 3f, x3 - 2f, y3 - 1f);
                        inner.CloseFigure();
                        g.FillPath(FurTopBrush, inner);
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

            // 头顶的绒毛
            Fluff(g, FurTopBrush, hx - 2f, hy - 4f, HeadR * 0.84f, HeadR * 0.84f, 6, 200f, 140f);

            RectangleF head = new RectangleF(hx - HeadR, hy - HeadR, HeadR * 2f, HeadR * 2f);
            g.FillEllipse(HeadBrush, head);
            g.FillEllipse(HighlightBrush, hx - 13f, hy - 17f, 16f, 9f);

            // 脸颊的蓬松绒毛（照片里脸颊鼓鼓的）
            g.FillEllipse(FurTopBrush, hx - 10f, hy + 2f, 24f, 19f);
            g.FillEllipse(FurTopBrush, hx + 4f, hy + 6f, 16f, 14f);

            // 口鼻
            float mx = hx + 13f, my = hy + 7f;
            g.FillEllipse(FurLightestBrush, mx - 11f, my - 7f, 22f, 15f);

            Face(g, pose, hx, hy, mx, my);

            g.DrawEllipse(OutlinePen, head);
        }

        static void Face(Graphics g, DogPose pose, float hx, float hy, float mx, float my)
        {
            float eyeY = hy - 3f;
            float eye1 = hx - 8f, eye2 = hx + 8f;

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
                Eye(g, eye1, eyeY, wide ? 6.6f : 5.6f);
                Eye(g, eye2, eyeY, wide ? 6.6f : 5.6f);
            }

            // 小巧的黑色鼻头（照片里是圆润的倒三角）
            using (GraphicsPath nose = new GraphicsPath())
            {
                float nx = mx + 5f, ny = my - 4f;
                nose.AddBezier(nx - 5f, ny - 2f, nx + 4.5f, ny - 4f, nx + 5f, ny + 1.5f, nx, ny + 4f);
                nose.AddBezier(nx, ny + 4f, nx - 5f, ny + 2f, nx - 5f, ny - 2f, nx - 5f, ny - 2f);
                nose.CloseFigure();
                g.FillPath(DarkBrush, nose);
                g.FillEllipse(HighlightBrush, nx - 3f, ny - 2.5f, 3.4f, 2.2f);
            }

            if (pose.State == PetState.Happy || pose.State == PetState.Run || pose.State == PetState.Hover)
            {
                using (GraphicsPath m = new GraphicsPath())
                {
                    m.AddBezier(mx - 5f, my + 2f, mx - 2f, my + 9f, mx + 5f, my + 9f, mx + 7f, my + 2f);
                    g.FillPath(DarkBrush, m);
                }
                g.FillEllipse(TongueBrush, mx - 1f, my + 5f, 8f, 8f);
            }
            else if (wide)
            {
                g.FillEllipse(DarkBrush, mx - 1f, my + 1f, 7f, 8f);
            }
            else
            {
                // 照片里的小狗是淡淡的「w」形嘴
                using (GraphicsPath m = new GraphicsPath())
                {
                    m.AddBezier(mx - 7f, my + 1f, mx - 4f, my + 6f, mx - 1f, my + 5f, mx, my + 3f);
                    m.AddBezier(mx, my + 3f, mx + 1f, my + 5f, mx + 4f, my + 6f, mx + 7f, my + 1f);
                    g.DrawPath(MouthPen, m);
                }
            }

            int a = (int)(60 + 110 * Math.Min(1f, pose.Mood));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 246, 168, 160)))
            {
                g.FillEllipse(b, hx - 18f, hy + 6f, 11f, 7f);
                g.FillEllipse(b, hx + 8f, hy + 8f, 10f, 6f);
            }
        }

        /// <summary>又大又圆的黑眼睛 + 两点高光（照片里最抓人的地方）</summary>
        static void Eye(Graphics g, float x, float y, float r)
        {
            g.FillEllipse(DarkBrush, x - r, y - r, r * 2f, r * 2f);
            g.FillEllipse(HighlightBrush, x - r * 0.62f, y - r * 0.72f, r * 0.72f, r * 0.72f);
            g.FillEllipse(HighlightBrush, x + r * 0.12f, y + r * 0.18f, r * 0.36f, r * 0.36f);
        }

        static void Arc(Graphics g, float x, float y)
        {
            using (GraphicsPath e = new GraphicsPath())
            {
                e.AddArc(x - 5.5f, y - 2f, 11f, 9f, 200f, 140f);
                g.DrawPath(EyePen, e);
            }
        }

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
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 246, 122, 150)))
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
            else if (pose.State == PetState.Hover)
            {
                for (int i = 0; i < 3; i++)
                {
                    float t = (pose.T * 1.3f + i * 0.33f) % 1f;
                    int alpha = (int)(110 * (1f - t));
                    if (alpha < 10) continue;
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 235, 228, 210)))
                    {
                        g.FillEllipse(b, BodyCX - 16f + i * 13f, GroundY - 2f + t * 10f, 14f * (1f - t * 0.3f), 6f);
                    }
                }
            }
            else if (pose.State == PetState.Run || pose.State == PetState.Sneak)
            {
                for (int i = 0; i < 3; i++)
                {
                    float t = (pose.T * 2.2f + i * 0.33f) % 1f;
                    int a = (int)(110 * (1f - t));
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
                    using (Pen p = new Pen(Color.FromArgb(alpha, 160, 130, 70), 1.6f))
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

        // ================================================================ 正面肖像

        /// <summary>
        /// 正面坐姿肖像：照着用户给的小金毛照片画——圆头、鼓脸颊、大黑眼睛、小黑鼻、
        /// 两侧垂耳、前爪露出黑肉垫。画布同样是 132×120，坐标以 (66, 60) 为中心。
        /// </summary>
        public static void DrawPortrait(Graphics g, float t)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float breathe = (float)Math.Sin(t * 1.4) * 0.8f;
            float headCX = 66f, headCY = 48f + breathe, headR = 30f;
            float bodyCY = 99f + breathe;

            // 身体：圆滚滚的胸口
            Fluff(g, FurTopBrush, 66f, bodyCY, 35f, 17f, 9, 145f, 250f);
            g.FillEllipse(FurBrush, 31f, bodyCY - 18f, 70f, 36f);
            g.FillEllipse(FurLightestBrush, 44f, bodyCY - 8f, 44f, 24f);

            // 两只前爪（正面看，不露肉垫）
            for (int i = 0; i < 2; i++)
            {
                float px = i == 0 ? 49f : 83f;
                g.FillEllipse(FurLightestBrush, px - 12f, bodyCY + 2f, 24f, 17f);
                g.DrawEllipse(ThinPen, px - 12f, bodyCY + 2f, 24f, 17f);
                using (Pen toe = new Pen(Color.FromArgb(120, 190, 150, 92), 1.2f))
                {
                    g.DrawLine(toe, px - 4f, bodyCY + 6f, px - 4f, bodyCY + 13f);
                    g.DrawLine(toe, px + 4f, bodyCY + 6f, px + 4f, bodyCY + 13f);
                }
            }

            // 两侧垂耳（画在头之前，被头压住内侧，只露出外沿）
            PortraitEar(g, 27f, 44f, -24f);
            PortraitEar(g, 105f, 44f, 24f);

            // 头顶绒毛 + 头
            Fluff(g, FurTopBrush, headCX, headCY - 5f, headR * 0.88f, headR * 0.88f, 11, 185f, 170f);
            g.FillEllipse(HeadBrush, headCX - headR, headCY - headR, headR * 2f, headR * 2f);

            // 鼓鼓的脸颊（暖色，不要糊成白色）
            g.FillEllipse(FurTopBrush, 24f, 46f + breathe, 32f, 30f);
            g.FillEllipse(FurTopBrush, 76f, 46f + breathe, 32f, 30f);

            // 口鼻区域（浅色，但比脸颊亮一点）
            g.FillEllipse(FurLightestBrush, 46f, 56f + breathe, 40f, 26f);

            // 眼睛：又大又圆、间距宽
            Eye(g, 52f, 46f + breathe, 9.6f);
            Eye(g, 80f, 46f + breathe, 9.6f);

            // 小巧的黑鼻头
            using (GraphicsPath nose = new GraphicsPath())
            {
                float nx = 66f, ny = 57f + breathe;
                nose.AddBezier(nx - 7f, ny - 2f, nx - 6f, ny + 6f, nx, ny + 7f, nx + 6f, ny + 6f);
                nose.AddBezier(nx + 6f, ny + 6f, nx + 7f, ny - 2f, nx, ny - 3.5f, nx - 7f, ny - 2f);
                nose.CloseFigure();
                g.FillPath(DarkBrush, nose);
                g.FillEllipse(HighlightBrush, nx - 4f, ny - 1f, 4.6f, 2.8f);
            }

            // 淡淡的 w 形嘴
            using (GraphicsPath m = new GraphicsPath())
            {
                m.AddBezier(58f, 68f + breathe, 62f, 74f + breathe, 65f, 72f + breathe, 66f, 69f + breathe);
                m.AddBezier(66f, 69f + breathe, 67f, 72f + breathe, 70f, 74f + breathe, 74f, 68f + breathe);
                g.DrawPath(MouthPen, m);
            }

            // 腮红
            using (SolidBrush b = new SolidBrush(Color.FromArgb(85, 246, 168, 160)))
            {
                g.FillEllipse(b, 34f, 58f + breathe, 16f, 10f);
                g.FillEllipse(b, 82f, 58f + breathe, 16f, 10f);
            }
        }

        static void PortraitEar(Graphics g, float x, float y, float tilt)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(tilt);
            g.TranslateTransform(-x, -y);

            using (GraphicsPath ear = new GraphicsPath())
            {
                ear.AddBezier(x, y - 13f, x - 20f, y + 2f, x - 17f, y + 30f, x + 3f, y + 34f);
                ear.AddBezier(x + 3f, y + 34f, x + 19f, y + 25f, x + 17f, y - 3f, x, y - 13f);
                ear.CloseFigure();
                g.FillPath(FurDarkBrush, ear);
                using (GraphicsPath inner = new GraphicsPath())
                {
                    inner.AddBezier(x + 1f, y - 6f, x - 12f, y + 6f, x - 10f, y + 25f, x + 2f, y + 28f);
                    inner.AddBezier(x + 2f, y + 28f, x + 12f, y + 21f, x + 11f, y + 1f, x + 1f, y - 6f);
                    inner.CloseFigure();
                    g.FillPath(FurTopBrush, inner);
                }
                g.DrawPath(OutlinePen, ear);
            }
            g.Restore(st);
        }
    }
}
