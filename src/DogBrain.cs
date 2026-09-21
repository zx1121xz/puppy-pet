using System;
using System.Drawing;

namespace PuppyPet
{
    /// <summary>小狗的状态</summary>
    public enum PetState
    {
        Idle,     // 站着发呆 / 呼吸
        Walk,     // 慢走
        Run,      // 跑
        Sit,      // 坐下
        Sleep,    // 睡觉
        Happy,    // 被摸头：开心蹦跳
        Scratch,  // 挠痒
        Sneak,    // 偷偷溜走
        Hover,    // 悬在半空 / 飞向空中目标
        Drag,     // 被拎起来
        Fall,     // 下落
        Shake     // 落地后抖毛
    }

    /// <summary>一帧的全部绘制参数（由大脑算好，画笔只负责画）</summary>
    public struct DogPose
    {
        public PetState State;
        public float Phase;     // 走路周期 0..1
        public float T;         // 当前状态已持续秒数
        public int Facing;      // 1 朝右 / -1 朝左
        public bool Airborne;   // 是否离地
        public float Squash;    // 1 正常；<1 压扁；>1 拉伸
        public float Mood;      // 0..1 开心程度
        public bool Blink;
        public float Lift;      // 影子的分离高度（0 贴地）
    }

    /// <summary>
    /// 行为大脑：纯逻辑，不碰窗口，可在测试里喂时间戳复现每一步。
    /// </summary>
    public class DogBrain
    {
        public const double LeaveDelay = 10.0;    // 鼠标移开后多久偷偷跑掉（秒）
        public const float WalkSpeed = 46f;
        public const float RunSpeed = 172f;
        public const float SneakSpeed = 104f;
        public const float Gravity = 1600f;
        public const float HopSpeed = 430f;
        public const float AirSpeed = 148f;      // 空中机动速度
        public const float HoverBob = 3.6f;      // 悬停时的上下浮动幅度

        public Rectangle Bounds;         // 活动范围（屏幕工作区坐标）
        public float Width = 132f;
        public float Height = 120f;

        public float X;
        public float Y;
        public float VX;
        public float VY;
        public int Facing = 1;
        public PetState State = PetState.Idle;
        public float StateTime;
        public float Phase;
        public bool Airborne;
        public float Squash = 1f;
        public float Mood;
        public bool Blink;
        public double Now;              // 最近一次 Update 的时间戳（悬停浮动用）
        public bool Hovering;
        public bool Paused;
        public float SpeedScale = 1f;
        public bool FullScreen = true;      // true: 满屏跑动；false: 只在屏幕底部活动

        readonly Random _rng;
        double _nextDecision;
        double _nextBlink;
        double _leaveAt = -1;         // 鼠标离开的时刻
        double _hoverAt = -1;         // 鼠标进入的时刻
        double _lastTouch = -1;       // 最近一次被摸/被拖
        double _happyUntil = -1;      // 点击后保持开心的截止时刻
        double _lastAir = 0;          // 最近一次待在空中的时刻（用于保证满屏跑动）
        float _targetX;
        float _targetY;
        bool _sneaking;

        public DogBrain(Rectangle bounds, int seed)
        {
            Bounds = bounds;
            _rng = new Random(seed);
            X = bounds.Left + bounds.Width * 0.5f;
            Y = GroundY;
            _nextDecision = 1.2;
            _nextBlink = 2.0;
            _targetX = X;
            _targetY = Y;
            _hoverAt = -1;
            _lastTouch = -1;
        }

        public float GroundY { get { return Bounds.Bottom - Height * 0.5f - 8f; } }

        public bool IsResting { get { return State == PetState.Sit || State == PetState.Sleep || State == PetState.Idle; } }
        public bool IsFloating { get { return !OnGround && (State == PetState.Hover || State == PetState.Happy || State == PetState.Scratch); } }
        public bool OnGround { get { return Y >= GroundY - 0.5f; } }
        /// <summary>实际绘制用的 Y：悬停时轻轻上下浮动</summary>
        public float VisualY
        {
            get { return IsFloating ? Y + (float)Math.Sin(Now * 2.6) * HoverBob : Y; }
        }
        PetState RestState() { return OnGround ? PetState.Idle : PetState.Hover; }
        public bool IsMoving { get { return State == PetState.Walk || State == PetState.Run || State == PetState.Sneak; } }

        // ---------------------------------------------------------------- 事件

        /// <summary>鼠标移入小狗身上</summary>
        public void OnHover(double now)
        {
            if (Hovering) return;
            Hovering = true;
            _hoverAt = now;
            _lastTouch = now;
            _leaveAt = -1;
            _sneaking = false;
            if (State != PetState.Drag && State != PetState.Fall)
            {
                SetState(PetState.Happy, now);
                Mood = Math.Max(Mood, 0.55f);
            }
        }

        /// <summary>鼠标离开小狗</summary>
        public void OnMouseLeave(double now)
        {
            if (!Hovering) return;
            Hovering = false;
            _leaveAt = now;            // 从这里开始计时，10 秒后偷偷跑掉
            if (State == PetState.Happy || State == PetState.Scratch)
            {
                SetState(RestState(), now);
            }
        }

        /// <summary>被拎起来</summary>
        public void BeginDrag(double now)
        {
            _lastTouch = now;
            _sneaking = false;
            _leaveAt = -1;
            Airborne = false;
            SetState(PetState.Drag, now);
        }

        public void DragTo(float x, float y)
        {
            X = Clamp(x, Bounds.Left + Width * 0.4f, Bounds.Right - Width * 0.4f);
            Y = Clamp(y, Bounds.Top + Height * 0.5f, Bounds.Bottom - Height * 0.5f);
            VX = 0f;
            VY = 0f;
        }

        /// <summary>松手：自由落体</summary>
        public void EndDrag(double now)
        {
            if (State != PetState.Drag) return;
            Airborne = true;
            VY = 0f;
            SetState(PetState.Fall, now);
        }

        /// <summary>被点了一下：这是真实的互动，会取消「偷偷跑掉」的倒计时</summary>
        public void Poke(double now)
        {
            _lastTouch = now;
            _sneaking = false;
            _leaveAt = -1;
            Mood = 1f;
            Hop(now, 1.8);
        }

        /// <summary>原地开心地跳一下（自己玩，不影响「偷偷跑掉」的倒计时）</summary>
        void Hop(double now, double hold)
        {
            _happyUntil = now + hold;
            if (State == PetState.Drag || State == PetState.Fall) return;
            if (OnGround)
            {
                Airborne = true;
                VY = -HopSpeed * 0.86f;
            }
            else
            {
                Airborne = false;
                _targetY = Math.Max(Bounds.Top + Height * 0.6f, Y - 26f);   // 空中往上蹦一下
            }
            SetState(PetState.Happy, now);
        }

        public void SetState(PetState s, double now)
        {
            if (State == s) return;
            State = s;
            StateTime = 0f;
            Phase = 0f;
            switch (s)
            {
                case PetState.Idle: _nextDecision = now + 1.4 + _rng.NextDouble() * 3.2; break;
                case PetState.Sit: _nextDecision = now + 2.6 + _rng.NextDouble() * 4.5; break;
                case PetState.Sleep: _nextDecision = now + 8.0 + _rng.NextDouble() * 14.0; break;
            }
        }

        // ---------------------------------------------------------------- 主循环

        public void Update(double now, double dt)
        {
            if (dt < 0) dt = 0;
            if (dt > 0.12) dt = 0.12;          // 卡顿保护，避免瞬移
            Now = now;
            if (Paused) return;

            StateTime += (float)dt;
            if (!OnGround) _lastAir = now;

            // 眨眼
            if (Blink)
            {
                if (now >= _nextBlink) { Blink = false; _nextBlink = now + 1.8 + _rng.NextDouble() * 3.4; }
            }
            else if (now >= _nextBlink)
            {
                Blink = true;
                _nextBlink = now + 0.11;
            }

            // 心情：被摸时迅速升高，之后缓慢回落
            if (Hovering) Mood = Math.Min(1f, Mood + (float)dt * 2.4f);
            else Mood = Math.Max(0f, Mood - (float)dt * 0.32f);

            // 悬停交互：摸头 <-> 挠痒 轮着来
            if (Hovering && State != PetState.Drag && State != PetState.Fall)
            {
                if (State != PetState.Happy && State != PetState.Scratch) SetState(PetState.Happy, now);
                else if (State == PetState.Happy && StateTime > 2.4f + (float)_rng.NextDouble() * 1.6f) SetState(PetState.Scratch, now);
                else if (State == PetState.Scratch && StateTime > 1.6f + (float)_rng.NextDouble() * 1.2f) SetState(PetState.Happy, now);
            }

            // 鼠标离开 10 秒 → 偷偷跑掉
            if (!Hovering && _leaveAt > 0 && State != PetState.Drag && State != PetState.Fall && now - _leaveAt >= LeaveDelay)
            {
                StartSneak(now);
            }

            // 走路周期
            if (IsMoving)
            {
                float spd = CurrentSpeed();
                Phase += (float)(dt * spd / 26.0);   // 26px 一个步幅
                if (Phase > 1f) Phase -= 1f;
            }
            else
            {
                Phase = 0f;
            }

            // 空中物理（自由落体）
            if (Airborne && State != PetState.Hover) AirPhysics(now, dt);

            switch (State)
            {
                case PetState.Idle:
                case PetState.Sit:
                case PetState.Sleep:
                    if (!Airborne && now >= _nextDecision) Decide(now);
                    break;

                case PetState.Walk:
                case PetState.Run:
                case PetState.Sneak:
                    if (!StepTowards(dt)) Arrive(now);
                    break;

                case PetState.Hover:
                    if (Airborne)
                    {
                        StepAirTowardsX(dt);
                        AirPhysics(now, dt);
                        if (VY < 0f && Y <= _targetY)      // 跳到目标高度：转入悬停
                        {
                            Airborne = false;
                            VY = 0f;
                        }
                        else if (VY >= 0f)                 // 抛物线到顶还没够高：就地悬停
                        {
                            Airborne = false;
                            VY = 0f;
                            if (_targetY < Y) _targetY = Y;
                        }
                    }
                    else if (!StepAirTowards(dt))
                    {
                        if (Math.Abs(_targetY - GroundY) < 1.5f)         // 目标是地面：落地休息
                        {
                            Y = GroundY;
                            Airborne = false;
                            VY = 0f;
                            Arrive(now);
                        }
                        else if (OnGround)
                        {
                            Arrive(now);
                        }
                        else if (StateTime > 1.6f + (float)_rng.NextDouble() * 3.0f)
                        {
                            Decide(now);                                 // 悬一会儿再决定去哪
                        }
                    }
                    else if (StateTime > 14f)
                    {
                        GoToGround(now);                                 // 悬太久就回地面
                    }
                    break;

                case PetState.Shake:
                    if (StateTime > 0.62f) SetState(PetState.Idle, now);
                    break;

                case PetState.Happy:
                case PetState.Scratch:
                    if (!Hovering && now >= _happyUntil) SetState(RestState(), now);
                    else if (OnGround && StateTime > 2.2f + (float)_rng.NextDouble() * 1.8f)
                    {
                        _happyUntil = now + 1.2;
                        Airborne = true;                    // 开心地小跳一下
                        VY = -HopSpeed * 0.62f;
                    }
                    break;

                case PetState.Fall:
                case PetState.Drag:
                    break;
            }

            // 跑动时轻轻上下颠
            if (IsMoving && !Airborne)
            {
                Squash = 1f + (float)Math.Sin(Phase * Math.PI * 2.0) * 0.035f;
            }
            else if (State == PetState.Sleep)
            {
                Squash = 1f + (float)Math.Sin(now * 1.6) * 0.02f;
            }
            else
            {
                Squash = 1f + (float)Math.Sin(now * 2.2) * 0.012f;
            }
        }

        void AirPhysics(double now, double dt)
        {
            VY += Gravity * (float)dt;
            Y += VY * (float)dt;
            if (Y >= GroundY)
            {
                Y = GroundY;
                Airborne = false;
                VY = 0f;
                // 被摸着的时候不要抖毛打断互动
                SetState(Hovering ? PetState.Happy : PetState.Shake, now);
            }
        }

        /// <summary>空中上升阶段：只横向靠近目标</summary>
        void StepAirTowardsX(double dt)
        {
            float dx = _targetX - X;
            if (Math.Abs(dx) < 4f) return;
            float step = Math.Min(AirSpeed * SpeedScale * (float)dt, Math.Abs(dx));
            X += dx > 0 ? step : -step;
            Facing = dx > 0 ? 1 : -1;
            X = Clamp(X, Bounds.Left + Width * 0.4f, Bounds.Right - Width * 0.4f);
        }

        /// <summary>空中：朝目标点平滑机动，返回 false 表示已到位</summary>
        bool StepAirTowards(double dt)
        {
            float dx = _targetX - X;
            float dy = _targetY - Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            if (dist < 7f) return false;
            float step = Math.Min(AirSpeed * SpeedScale * (float)dt, dist);
            X += dx / dist * step;
            Y += dy / dist * step;
            if (dx > 0.6f) Facing = 1;
            else if (dx < -0.6f) Facing = -1;
            ClampPosition();
            return (float)Math.Sqrt((_targetX - X) * (_targetX - X) + (_targetY - Y) * (_targetY - Y)) >= 7f;
        }

        void ClampPosition()
        {
            X = Clamp(X, Bounds.Left + Width * 0.4f, Bounds.Right - Width * 0.4f);
            Y = Clamp(Y, Bounds.Top + Height * 0.55f, GroundY);
        }

        /// <summary>起跳扑向空中目标（有抛物线的味道，不是凭空飘）</summary>
        public void LeapTo(float targetX, float targetY, double now)
        {
            _targetX = Clamp(targetX, Bounds.Left + Width * 0.4f, Bounds.Right - Width * 0.4f);
            _targetY = Clamp(targetY, Bounds.Top + Height * 0.6f, GroundY);
            if (OnGround)
            {
                Airborne = true;
                VY = -(float)Math.Sqrt(2.0 * Gravity * Math.Max(12f, GroundY - _targetY) * 1.03);
            }
            else
            {
                Airborne = false;
            }
            SetState(PetState.Hover, now);
        }

        /// <summary>飞回地面</summary>
        public void GoToGround(double now)
        {
            if (OnGround) { SetState(PetState.Idle, now); return; }
            _targetX = Clamp(X + (float)(_rng.NextDouble() * 160 - 80), Bounds.Left + Width * 0.4f, Bounds.Right - Width * 0.4f);
            _targetY = GroundY;
            Airborne = false;
            SetState(PetState.Hover, now);
        }

        /// <summary>随机一个空中目标点（离地面有高度）</summary>
        float RandomAirY()
        {
            float top = Bounds.Top + Height * 0.8f;
            float bottom = GroundY - 90f;
            if (bottom < top) return top;
            return top + (float)_rng.NextDouble() * (bottom - top);
        }

        float CurrentSpeed()
        {
            if (State == PetState.Run) return RunSpeed * SpeedScale;
            if (State == PetState.Sneak) return SneakSpeed * SpeedScale;
            return WalkSpeed * SpeedScale;
        }

        /// <summary>朝目标走一步；返回 false 表示已到达</summary>
        bool StepTowards(double dt)
        {
            float dx = _targetX - X;
            float dist = Math.Abs(dx);
            if (dist < 5f) return false;
            float spd = CurrentSpeed();
            float step = spd * (float)dt;
            if (step > dist) step = dist;
            X += (dx > 0 ? step : -step);
            Facing = dx > 0 ? 1 : -1;
            X = Clamp(X, Bounds.Left + Width * 0.4f, Bounds.Right - Width * 0.4f);
            return Math.Abs(_targetX - X) >= 5f;
        }

        void Arrive(double now)
        {
            if (_sneaking)
            {
                _sneaking = false;
                // 溜到一个新地方后，有时撒欢跑一圈
                if (_rng.NextDouble() < 0.45)
                {
                    _targetX = Clamp(X + (float)(_rng.NextDouble() * 900 - 450), Bounds.Left + Width * 0.4f, Bounds.Right - Width * 0.4f);
                    if (FullScreen && _rng.NextDouble() < 0.35) { LeapTo(_targetX, RandomAirY(), now); return; }
                    _targetY = GroundY;
                    Airborne = false;
                    SetState(OnGround ? PetState.Run : PetState.Hover, now);
                    return;
                }
                SetState(PetState.Sit, now);
                return;
            }
            SetState(OnGround ? (_rng.NextDouble() < 0.45 ? PetState.Sit : PetState.Idle) : PetState.Hover, now);
        }

        void Decide(double now)
        {
            double sinceTouch = _lastTouch < 0 ? 999 : now - _lastTouch;

            // 只在地面溜达太久？蹦到半空去玩（保证真的会满屏跑）
            if (FullScreen && OnGround && now - _lastAir > 14.0)
            {
                LeapTo(RandomX(), RandomAirY(), now);
                return;
            }

            // 很久没被理 → 自己跑远去玩
            if (sinceTouch > 22.0 && _rng.NextDouble() < 0.42)
            {
                _targetX = RandomX();
                if (FullScreen && _rng.NextDouble() < 0.55)
                {
                    LeapTo(_targetX, RandomAirY(), now);
                    return;
                }
                SetState(PetState.Run, now);
                return;
            }

            // 满屏模式：时不时蹦到半空玩耍
            if (FullScreen && !OnGround)
            {
                double rr = _rng.NextDouble();
                if (rr < 0.55) { LeapTo(RandomX(), RandomAirY(), now); return; }
                if (rr < 0.62) { Hop(now, 1.0); _targetY = RandomAirY(); SetState(PetState.Hover, now); return; }
                GoToGround(now);
                return;
            }

            double r = _rng.NextDouble();
            if (FullScreen && r < 0.34)
            {
                LeapTo(RandomX(), RandomAirY(), now);      // 一跃而上，蹦到空中某个位置
            }
            else if (r < 0.42)
            {
                _targetX = Clamp(X + (float)(_rng.NextDouble() * 520 - 260), Bounds.Left + Width * 0.4f, Bounds.Right - Width * 0.4f);
                SetState(PetState.Walk, now);
            }
            else if (r < 0.50)
            {
                _targetX = RandomX();
                SetState(PetState.Run, now);
            }
            else if (r < 0.64)
            {
                SetState(PetState.Sit, now);
            }
            else if (r < 0.76 && sinceTouch > 8.0)
            {
                SetState(PetState.Sleep, now);
            }
            else if (r < 0.88)
            {
                Hop(now, 1.2);                   // 自己玩：原地开心跳一下
            }
            else
            {
                SetState(PetState.Idle, now);
            }
        }

        /// <summary>偷偷溜走：压低身子快步跑到远处</summary>
        public void StartSneak(double now)
        {
            _leaveAt = -1;
            _sneaking = true;
            float dir = _rng.NextDouble() < 0.5 ? -1f : 1f;
            if (X + dir * 300f < Bounds.Left + Width || X + dir * 300f > Bounds.Right - Width) dir = -dir;
            _targetX = Clamp(X + dir * (320f + (float)_rng.NextDouble() * 620f),
                             Bounds.Left + Width * 0.4f, Bounds.Right - Width * 0.4f);
            if (OnGround)
            {
                _targetY = GroundY;
                SetState(PetState.Sneak, now);
            }
            else
            {
                // 半空中被发现要走？直接飞走
                _targetY = FullScreen && _rng.NextDouble() < 0.5 ? RandomAirY() : GroundY;
                Airborne = false;
                SetState(PetState.Hover, now);
            }
        }

        float RandomX()
        {
            return Bounds.Left + Width * 0.4f + (float)_rng.NextDouble() * (Bounds.Width - Width * 0.8f);
        }

        /// <summary>当前帧的绘制参数</summary>
        public DogPose GetPose()
        {
            DogPose p = new DogPose();
            p.State = State;
            p.Phase = Phase;
            p.T = StateTime;
            p.Facing = Facing;
            p.Airborne = Airborne;
            p.Squash = Squash;
            p.Mood = Mood;
            p.Blink = Blink;
            p.Lift = Math.Max(0f, GroundY - Y);
            return p;
        }

        /// <summary>命中测试：鼠标是否落在小狗身上</summary>
        public bool HitTest(float px, float py)
        {
            float w = Width * 0.40f;
            float h = Height * 0.38f;
            if (State == PetState.Sleep) h *= 0.72f;
            if (State == PetState.Drag || State == PetState.Fall) h *= 1.15f;
            float cy = Y + Height * 0.04f;
            float dx = (px - X) / w;
            float dy = (py - cy) / h;
            return dx * dx + dy * dy <= 1f;
        }

        public static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
