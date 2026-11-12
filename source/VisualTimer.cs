using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BobDesktop
{
    public enum VisualTimerTheme
    {
        CozyBob,       // Warm cocoa & cream with amber/terracotta accents (Default)
        ModernGlass,   // Dark translucent obsidian glass with neon accents
        MinimalPill,   // Ultra-compact single-line capsule
        LightCeramic   // Soft porcelain ivory with rich espresso typography
    }

    public enum VisualTimerMode
    {
        Detached,      // Free-floating stationary HUD anywhere on desktop (Default)
        CompactBar,    // Minimal progress bar anchored right under Bob's feet
        Follow         // Legacy alias falling back to Detached
    }

    internal class VisualTimerThemeColors
    {
        public Color Background;
        public Color Border;
        public Color InnerHighlight;
        public Color TextPrimary;
        public Color TextMuted;
        public Color FocusAccent;
        public Color BreakAccent;
        public Color ProgressTrack;
        public Color ButtonBg;
        public Color ButtonHover;
        public Color ButtonText;

        public static VisualTimerThemeColors Get(VisualTimerTheme theme, byte opacity)
        {
            float op = opacity / 255.0f;
            switch (theme)
            {
                case VisualTimerTheme.ModernGlass:
                    return new VisualTimerThemeColors
                    {
                        Background = Color.FromArgb((int)(opacity * 0.90f), 14, 16, 22),
                        Border = Color.FromArgb((int)(opacity * 0.45f), 255, 255, 255),
                        InnerHighlight = Color.FromArgb(30, 255, 255, 255),
                        TextPrimary = Color.FromArgb(255, 255, 255),
                        TextMuted = Color.FromArgb(150, 162, 178),
                        FocusAccent = Color.FromArgb(255, 78, 78),
                        BreakAccent = Color.FromArgb(16, 210, 150),
                        ProgressTrack = Color.FromArgb(40, 255, 255, 255),
                        ButtonBg = Color.FromArgb(38, 255, 255, 255),
                        ButtonHover = Color.FromArgb(85, 255, 255, 255),
                        ButtonText = Color.FromArgb(240, 245, 255)
                    };

                case VisualTimerTheme.MinimalPill:
                    return new VisualTimerThemeColors
                    {
                        Background = Color.FromArgb((int)(opacity * 0.94f), 24, 20, 18),
                        Border = Color.FromArgb((int)(opacity * 0.50f), 220, 180, 140),
                        InnerHighlight = Color.FromArgb(25, 255, 255, 255),
                        TextPrimary = Color.FromArgb(250, 244, 236),
                        TextMuted = Color.FromArgb(185, 170, 155),
                        FocusAccent = Color.FromArgb(255, 110, 75),
                        BreakAccent = Color.FromArgb(80, 200, 135),
                        ProgressTrack = Color.FromArgb(50, 255, 255, 255),
                        ButtonBg = Color.FromArgb(40, 255, 255, 255),
                        ButtonHover = Color.FromArgb(85, 255, 255, 255),
                        ButtonText = Color.FromArgb(255, 250, 245)
                    };

                case VisualTimerTheme.LightCeramic:
                    return new VisualTimerThemeColors
                    {
                        Background = Color.FromArgb((int)(opacity * 0.95f), 254, 252, 248),
                        Border = Color.FromArgb((int)(opacity * 0.50f), 180, 165, 150),
                        InnerHighlight = Color.FromArgb(120, 255, 255, 255),
                        TextPrimary = Color.FromArgb(36, 28, 22),
                        TextMuted = Color.FromArgb(125, 115, 105),
                        FocusAccent = Color.FromArgb(220, 55, 30),
                        BreakAccent = Color.FromArgb(35, 145, 90),
                        ProgressTrack = Color.FromArgb(40, 0, 0, 0),
                        ButtonBg = Color.FromArgb(25, 0, 0, 0),
                        ButtonHover = Color.FromArgb(55, 0, 0, 0),
                        ButtonText = Color.FromArgb(40, 30, 25)
                    };

                case VisualTimerTheme.CozyBob:
                default:
                    return new VisualTimerThemeColors
                    {
                        Background = Color.FromArgb((int)(opacity * 0.94f), 35, 24, 18),
                        Border = Color.FromArgb((int)(opacity * 0.50f), 232, 186, 134),
                        InnerHighlight = Color.FromArgb(30, 255, 255, 255),
                        TextPrimary = Color.FromArgb(253, 248, 242),
                        TextMuted = Color.FromArgb(196, 178, 162),
                        FocusAccent = Color.FromArgb(255, 112, 78),
                        BreakAccent = Color.FromArgb(86, 202, 138),
                        ProgressTrack = Color.FromArgb(50, 255, 255, 255),
                        ButtonBg = Color.FromArgb(45, 255, 255, 255),
                        ButtonHover = Color.FromArgb(95, 255, 255, 255),
                        ButtonText = Color.FromArgb(255, 250, 245)
                    };
            }
        }
    }

    internal sealed class VisualTimerOverlay : Form
    {
        private readonly PomodoroTimer pomodoro;
        private readonly Preferences prefs;
        private IntPtr dc = IntPtr.Zero;
        private IntPtr original = IntPtr.Zero;
        private IntPtr currentBitmap = IntPtr.Zero;
        private bool disposed;

        // Interactive dragging
        private bool isDragging;
        private Point dragStartMouse;
        private Point dragStartLocation;

        // Hover tracking
        private string hoveredButton = null;

        // Button hit rectangles (in local coordinates)
        private Rectangle rectBtnPlayPause;
        private Rectangle rectBtnSkip;
        private Rectangle rectBtnPlus5;
        private Rectangle rectBtnReset;
        private Rectangle rectBtnClose;

        // Render cache to avoid redrawing when nothing changed
        private string lastRenderKey;

        public VisualTimerOverlay(PomodoroTimer pomo, Preferences userPrefs)
        {
            pomodoro = pomo;
            prefs = userPrefs;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            Text = "Bob Visual Timer";
            DoubleBuffered = true;

            MouseDown += OnTimerMouseDown;
            MouseMove += OnTimerMouseMove;
            MouseUp += OnTimerMouseUp;
            MouseLeave += OnTimerMouseLeave;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p = base.CreateParams;
                p.ExStyle |= Native.ExLayered | Native.ExToolWindow | Native.ExNoActivate;
                if (prefs != null && prefs.PomodoroTimerClickThrough)
                {
                    p.ExStyle |= Native.ExTransparent;
                }
                return p;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
            if (m.Msg == 0x84 && prefs != null && prefs.PomodoroTimerClickThrough)
            {
                m.Result = new IntPtr(-1); // HTTRANSPARENT
                return;
            }
            base.WndProc(ref m);
        }

        public void RecreateHandleStyle()
        {
            RecreateHandle();
            lastRenderKey = null;
        }

        public void UpdateTimer(double now, double catX, double catY, int catW, int catH, Rectangle screenArea)
        {
            if (disposed) return;

            bool enabled = prefs.PomodoroVisualTimerEnabled;
            if (enabled && prefs.PomodoroTimerAutoHideWhenIdle && pomodoro.Phase == PomodoroPhase.Idle)
            {
                enabled = false;
            }

            if (!enabled)
            {
                if (Visible) Hide();
                return;
            }

            VisualTimerMode mode = ParseMode(prefs.PomodoroTimerMode);
            VisualTimerTheme theme = ParseTheme(prefs.PomodoroTimerTheme);
            double scale = Math.Max(0.7, Math.Min(1.6, prefs.PomodoroTimerScale));

            // Determine dimensions with generous non-overlapping layout
            int hudW, hudH;
            if (mode == VisualTimerMode.CompactBar)
            {
                hudW = (int)(144 * scale);
                hudH = (int)(24 * scale);
            }
            else if (theme == VisualTimerTheme.MinimalPill)
            {
                hudW = (int)(196 * scale);
                hudH = (int)(38 * scale);
            }
            else
            {
                hudW = (int)(256 * scale);
                hudH = (int)(94 * scale);
            }

            // Calculate position: Stationary HUD by default (never follows or moves with the cat!)
            int targetX, targetY;
            if (mode == VisualTimerMode.CompactBar)
            {
                targetX = (int)catX + (catW - hudW) / 2;
                targetX = Math.Max(screenArea.Left + 4, Math.Min(targetX, screenArea.Right - hudW - 4));
                targetY = (int)catY + catH - (int)(18 * scale);
            }
            else // Detached: completely stationary where placed
            {
                if (prefs.PomodoroTimerCustomX >= 0 && prefs.PomodoroTimerCustomY >= 0)
                {
                    targetX = Math.Max(screenArea.Left + 4, Math.Min(prefs.PomodoroTimerCustomX, screenArea.Right - hudW - 4));
                    targetY = Math.Max(screenArea.Top + 4, Math.Min(prefs.PomodoroTimerCustomY, screenArea.Bottom - hudH - 4));
                }
                else
                {
                    // Default stationary spot: top-right corner of screen
                    targetX = screenArea.Right - hudW - 32;
                    targetY = screenArea.Top + 32;
                    prefs.PomodoroTimerCustomX = targetX;
                    prefs.PomodoroTimerCustomY = targetY;
                }
            }

            if (!isDragging)
            {
                if (Location.X != targetX || Location.Y != targetY || Size.Width != hudW || Size.Height != hudH)
                {
                    Location = new Point(targetX, targetY);
                    Size = new Size(hudW, hudH);
                }
            }

            // Check if redraw needed
            string renderKey = string.Format("{0}:{1}:{2}:{3:F2}:{4}:{5}:{6}:{7}:{8}",
                pomodoro.Phase, pomodoro.GetTimeFormatted(), pomodoro.IsPaused,
                pomodoro.Progress, theme, mode, scale, prefs.PomodoroTimerOpacity, hoveredButton);

            if (renderKey != lastRenderKey || !Visible)
            {
                RenderHUD(hudW, hudH, theme, mode, scale);
                lastRenderKey = renderKey;
            }

            if (!Visible)
            {
                Show();
                Native.SetWindowPos(Handle, new IntPtr(-1), Location.X, Location.Y, hudW, hudH, 0x0010 | 0x0001);
            }
        }

        private void RenderHUD(int w, int h, VisualTimerTheme theme, VisualTimerMode mode, double scale)
        {
            if (w <= 0 || h <= 0) return;

            VisualTimerThemeColors colors = VisualTimerThemeColors.Get(theme, prefs.PomodoroTimerOpacity);
            ReleaseBitmap();

            using (Bitmap surface = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(surface))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                if (mode == VisualTimerMode.CompactBar)
                {
                    RenderCompactBar(g, w, h, colors, scale);
                }
                else if (theme == VisualTimerTheme.MinimalPill)
                {
                    RenderMinimalPill(g, w, h, colors, scale);
                }
                else
                {
                    RenderStandardHUD(g, w, h, colors, theme, mode, scale);
                }

                currentBitmap = surface.GetHbitmap(Color.FromArgb(0));
            }

            if (dc == IntPtr.Zero) dc = Native.CreateCompatibleDC(IntPtr.Zero);
            IntPtr prev = Native.SelectObject(dc, currentBitmap);
            if (original == IntPtr.Zero) original = prev;

            Native.POINT position = new Native.POINT(Location.X, Location.Y);
            Native.POINT origin = new Native.POINT(0, 0);
            Native.SIZE dimensions = new Native.SIZE(w, h);
            Native.BLEND blend = new Native.BLEND { Op = 0, Flags = 0, Alpha = 255, Format = 1 };

            Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref position, ref dimensions, dc, ref origin, 0, ref blend, 2);
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002);
        }

        private void RenderStandardHUD(Graphics g, int w, int h, VisualTimerThemeColors c, VisualTimerTheme theme, VisualTimerMode mode, double scale)
        {
            float cornerRadius = (float)(11 * scale);
            RectangleF cardRect = new RectangleF(1.5f, 1.5f, w - 3f, h - 3f);

            // Background card with rounded path
            using (GraphicsPath path = CreateRoundedRectanglePath(cardRect, cornerRadius))
            {
                using (SolidBrush bgBrush = new SolidBrush(c.Background))
                {
                    g.FillPath(bgBrush, path);
                }
                using (Pen borderPen = new Pen(c.Border, 1.2f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // Top inner highlight line
            using (Pen hiPen = new Pen(c.InnerHighlight, 1f))
            {
                g.DrawLine(hiPen, cardRect.Left + cornerRadius, cardRect.Top + 1f, cardRect.Right - cornerRadius, cardRect.Top + 1f);
            }

            Color accentColor = pomodoro.IsInWork ? c.FocusAccent : pomodoro.IsInBreak ? c.BreakAccent : c.TextMuted;

            // ==========================================
            // TOP ROW: Phase Badge Pill | Cycles | Controls
            // ==========================================
            float topY = (float)(9 * scale);
            float badgeX = (float)(12 * scale);
            float badgeH = (float)(19 * scale);
            string phaseText = pomodoro.Phase == PomodoroPhase.Work ? "FOCUS" :
                               pomodoro.Phase == PomodoroPhase.ShortBreak ? "SHORT BREAK" :
                               pomodoro.Phase == PomodoroPhase.LongBreak ? "LONG BREAK" : "READY";

            using (Font badgeFont = new Font("Segoe UI", (float)(7.2f * scale), FontStyle.Bold))
            {
                SizeF phaseSize = g.MeasureString(phaseText, badgeFont);
                float badgeW = phaseSize.Width + (float)(18 * scale);
                RectangleF badgeRect = new RectangleF(badgeX, topY, badgeW, badgeH);

                using (GraphicsPath badgePath = CreateRoundedRectanglePath(badgeRect, badgeH / 2f))
                {
                    using (SolidBrush bBg = new SolidBrush(Color.FromArgb(45, accentColor.R, accentColor.G, accentColor.B)))
                    {
                        g.FillPath(bBg, badgePath);
                    }
                    using (Pen bPen = new Pen(Color.FromArgb(75, accentColor.R, accentColor.G, accentColor.B), 1f))
                    {
                        g.DrawPath(bPen, badgePath);
                    }
                }

                // Dot in badge
                float dotD = (float)(5.5f * scale);
                float dotY = topY + (badgeH - dotD) / 2f;
                using (SolidBrush dotBrush = new SolidBrush(accentColor))
                {
                    g.FillEllipse(dotBrush, badgeX + (float)(6 * scale), dotY, dotD, dotD);
                }

                using (SolidBrush textBrush = new SolidBrush(accentColor))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(phaseText, badgeFont, textBrush, new RectangleF(badgeX + (float)(13 * scale), topY, badgeW - (float)(13 * scale), badgeH), sf);
                }

                // Window control buttons at far top-right
                float btnSmallSize = (float)(18 * scale);
                float btnCloseX = w - (float)(12 * scale) - btnSmallSize;
                float btnResetX = btnCloseX - btnSmallSize - (float)(5 * scale);

                rectBtnClose = new Rectangle((int)btnCloseX, (int)topY, (int)btnSmallSize, (int)btnSmallSize);
                rectBtnReset = new Rectangle((int)btnResetX, (int)topY, (int)btnSmallSize, (int)btnSmallSize);

                DrawVectorButton(g, rectBtnReset, "reset", hoveredButton == "Reset", c, scale);
                DrawVectorButton(g, rectBtnClose, "close", hoveredButton == "Close", c, scale);

                // Cycle dots placed safely to the left of the reset button (no overlapping!)
                float cycleDotD = (float)(6 * scale);
                int totalCycles = Math.Max(2, Math.Min(8, pomodoro.CyclesBeforeLongBreak));
                float dotsTotalW = totalCycles * (cycleDotD + (float)(4 * scale));
                float dotsStartX = btnResetX - (float)(10 * scale) - dotsTotalW;

                if (dotsStartX > badgeX + badgeW + 8 * scale)
                {
                    for (int i = 0; i < totalCycles; i++)
                    {
                        float cx = dotsStartX + i * (cycleDotD + (float)(4 * scale));
                        float cy = topY + (badgeH - cycleDotD) / 2f;
                        bool active = i < pomodoro.CurrentCycle && pomodoro.Phase != PomodoroPhase.Idle;

                        using (SolidBrush dotB = new SolidBrush(active ? accentColor : Color.FromArgb(40, c.TextMuted)))
                        {
                            g.FillEllipse(dotB, cx, cy, cycleDotD, cycleDotD);
                        }
                    }
                }
            }

            // ==========================================
            // MIDDLE ROW: Countdown Timer & Action Buttons
            // ==========================================
            float midY = (float)(32 * scale);
            string timeStr = pomodoro.Phase == PomodoroPhase.Idle
                ? string.Format("{0:D2}:00", pomodoro.WorkMinutes)
                : pomodoro.GetTimeFormatted();

            using (Font timeFont = new Font("Segoe UI", (float)(21 * scale), FontStyle.Bold))
            using (SolidBrush timeBrush = new SolidBrush(c.TextPrimary))
            {
                g.DrawString(timeStr, timeFont, timeBrush, (float)(12 * scale), midY);
            }

            // Action Buttons row at right: [Play/Pause] [Skip] [+5m]
            float btnY = midY + (float)(3 * scale);
            float btnH = (float)(25 * scale);
            float btnPlayW = (float)(28 * scale);
            float btnSkipW = (float)(28 * scale);
            float btnPlusW = (float)(36 * scale);

            float rightMargin = (float)(12 * scale);
            float plusX = w - rightMargin - btnPlusW;
            float skipX = plusX - (float)(6 * scale) - btnSkipW;
            float playX = skipX - (float)(6 * scale) - btnPlayW;

            rectBtnPlayPause = new Rectangle((int)playX, (int)btnY, (int)btnPlayW, (int)btnH);
            rectBtnSkip = new Rectangle((int)skipX, (int)btnY, (int)btnSkipW, (int)btnH);
            rectBtnPlus5 = new Rectangle((int)plusX, (int)btnY, (int)btnPlusW, (int)btnH);

            DrawVectorActionButton(g, rectBtnPlayPause, pomodoro.IsRunning ? "pause" : "play", hoveredButton == "PlayPause", c, accentColor, scale);
            DrawVectorActionButton(g, rectBtnSkip, "skip", hoveredButton == "Skip", c, c.TextMuted, scale);
            DrawTextActionButton(g, rectBtnPlus5, "+5m", hoveredButton == "Plus5", c, c.TextMuted, scale);

            // Paused indicator: cleanly positioned between timer text and play button without overlapping!
            if (pomodoro.IsPaused && pomodoro.Phase != PomodoroPhase.Idle)
            {
                float pauseW = (float)(44 * scale);
                float pauseH = (float)(16 * scale);
                float pauseX = playX - pauseW - (float)(6 * scale);
                float pauseY = midY + (float)(8 * scale);

                RectangleF pRect = new RectangleF(pauseX, pauseY, pauseW, pauseH);
                using (GraphicsPath pPath = CreateRoundedRectanglePath(pRect, pauseH / 2f))
                {
                    using (SolidBrush pBg = new SolidBrush(Color.FromArgb(50, 245, 175, 40)))
                    {
                        g.FillPath(pBg, pPath);
                    }
                    using (Pen pPen = new Pen(Color.FromArgb(120, 245, 175, 40), 1f))
                    {
                        g.DrawPath(pPen, pPath);
                    }
                }
                using (Font pf = new Font("Segoe UI", (float)(6.8f * scale), FontStyle.Bold))
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(245, 180, 50)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("PAUSED", pf, pb, pRect, sf);
                }
            }

            // ==========================================
            // BOTTOM ROW: Progress Bar & Status Text
            // ==========================================
            float barY = (float)(65 * scale);
            float barX = (float)(12 * scale);
            float barW = w - (float)(24 * scale);
            float barH = (float)(4.5f * scale);

            // Progress Track
            RectangleF trackRect = new RectangleF(barX, barY, barW, barH);
            using (GraphicsPath trackPath = CreateRoundedRectanglePath(trackRect, barH / 2f))
            {
                using (SolidBrush trkBrush = new SolidBrush(c.ProgressTrack))
                {
                    g.FillPath(trkBrush, trackPath);
                }
            }

            // Progress Fill
            double progress = pomodoro.Progress;
            if (progress > 0.005)
            {
                float fillW = Math.Max(barH, (float)(barW * Math.Min(1.0, progress)));
                RectangleF fillRect = new RectangleF(barX, barY, fillW, barH);
                using (GraphicsPath fillPath = CreateRoundedRectanglePath(fillRect, barH / 2f))
                {
                    using (SolidBrush fillBrush = new SolidBrush(accentColor))
                    {
                        g.FillPath(fillBrush, fillPath);
                    }
                }
            }

            // Micro-copy subtext (cleanly formatted, no wrapping or overlap)
            float statusY = barY + barH + (float)(3 * scale);
            string subText;
            if (pomodoro.Phase == PomodoroPhase.Work)
            {
                subText = pomodoro.IsPaused ? "Focus paused — click play to resume" : "Focus session in progress • Bob stays calm";
            }
            else if (pomodoro.Phase == PomodoroPhase.ShortBreak || pomodoro.Phase == PomodoroPhase.LongBreak)
            {
                subText = "Break time — stretch, rest your eyes & drink water";
            }
            else
            {
                subText = "Ready to focus • drag anywhere to move HUD";
            }

            using (Font subFont = new Font("Segoe UI", (float)(6.8f * scale), FontStyle.Regular))
            using (SolidBrush subBrush = new SolidBrush(c.TextMuted))
            {
                g.DrawString(subText, subFont, subBrush, barX, statusY);
            }
        }

        private void RenderMinimalPill(Graphics g, int w, int h, VisualTimerThemeColors c, double scale)
        {
            float cornerRadius = h / 2f;
            RectangleF pillRect = new RectangleF(1.5f, 1.5f, w - 3f, h - 3f);

            using (GraphicsPath path = CreateRoundedRectanglePath(pillRect, cornerRadius))
            {
                using (SolidBrush bgBrush = new SolidBrush(c.Background)) g.FillPath(bgBrush, path);
                using (Pen borderPen = new Pen(c.Border, 1.2f)) g.DrawPath(borderPen, path);
            }

            Color accentColor = pomodoro.IsInWork ? c.FocusAccent : pomodoro.IsInBreak ? c.BreakAccent : c.TextMuted;

            // Phase indicator dot
            float dotD = (float)(8 * scale);
            float dotX = (float)(10 * scale);
            float dotY = (h - dotD) / 2f;
            using (SolidBrush dotBrush = new SolidBrush(accentColor))
            {
                g.FillEllipse(dotBrush, dotX, dotY, dotD, dotD);
            }

            // Time string
            string timeStr = pomodoro.Phase == PomodoroPhase.Idle
                ? string.Format("{0:D2}:00", pomodoro.WorkMinutes)
                : pomodoro.GetTimeFormatted();

            float timeX = dotX + dotD + (float)(6 * scale);
            using (Font font = new Font("Segoe UI", (float)(12 * scale), FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(c.TextPrimary))
            {
                g.DrawString(timeStr, font, tb, timeX, (float)(8 * scale));
            }

            // Buttons
            float btnSize = (float)(22 * scale);
            float btnY = (h - btnSize) / 2f;
            float btnSkipX = w - (float)(10 * scale) - btnSize;
            float btnPlayX = btnSkipX - (float)(5 * scale) - btnSize;

            rectBtnPlayPause = new Rectangle((int)btnPlayX, (int)btnY, (int)btnSize, (int)btnSize);
            rectBtnSkip = new Rectangle((int)btnSkipX, (int)btnY, (int)btnSize, (int)btnSize);
            rectBtnPlus5 = Rectangle.Empty;
            rectBtnReset = Rectangle.Empty;
            rectBtnClose = Rectangle.Empty;

            DrawVectorActionButton(g, rectBtnPlayPause, pomodoro.IsRunning ? "pause" : "play", hoveredButton == "PlayPause", c, accentColor, scale);
            DrawVectorActionButton(g, rectBtnSkip, "skip", hoveredButton == "Skip", c, c.TextMuted, scale);

            // Bottom 2px progress bar
            if (pomodoro.Progress > 0)
            {
                float progH = 2.5f;
                float progW = (float)((w - 20) * pomodoro.Progress);
                using (SolidBrush pb = new SolidBrush(accentColor))
                {
                    g.FillRectangle(pb, 10, h - 3, progW, progH);
                }
            }
        }

        private void RenderCompactBar(Graphics g, int w, int h, VisualTimerThemeColors c, double scale)
        {
            float cornerRadius = h / 2f;
            RectangleF barRect = new RectangleF(1.5f, 1.5f, w - 3f, h - 3f);

            using (GraphicsPath path = CreateRoundedRectanglePath(barRect, cornerRadius))
            {
                using (SolidBrush bgBrush = new SolidBrush(c.Background)) g.FillPath(bgBrush, path);
                using (Pen borderPen = new Pen(c.Border, 1.2f)) g.DrawPath(borderPen, path);
            }

            Color accentColor = pomodoro.IsInWork ? c.FocusAccent : pomodoro.IsInBreak ? c.BreakAccent : c.TextMuted;

            // Progress fill inside bar
            if (pomodoro.Progress > 0)
            {
                float fillW = Math.Max(h, (float)(w * Math.Min(1.0, pomodoro.Progress)));
                RectangleF fillRect = new RectangleF(1.5f, 1.5f, fillW - 3f, h - 3f);
                using (GraphicsPath fillPath = CreateRoundedRectanglePath(fillRect, cornerRadius))
                using (SolidBrush fillB = new SolidBrush(Color.FromArgb(90, accentColor.R, accentColor.G, accentColor.B)))
                {
                    g.FillPath(fillB, fillPath);
                }
            }

            // Centered compact label
            string label = pomodoro.Phase == PomodoroPhase.Idle
                ? string.Format("{0}m Focus", pomodoro.WorkMinutes)
                : string.Format("{0} {1}", pomodoro.IsInWork ? "Focus" : "Break", pomodoro.GetTimeFormatted());

            using (Font f = new Font("Segoe UI", (float)(8f * scale), FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(c.TextPrimary))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(label, f, tb, barRect, sf);
            }

            rectBtnPlayPause = new Rectangle(0, 0, w, h);
            rectBtnSkip = Rectangle.Empty;
            rectBtnPlus5 = Rectangle.Empty;
            rectBtnReset = Rectangle.Empty;
            rectBtnClose = Rectangle.Empty;
        }

        // ==========================================
        // VECTOR-DRAWN BUTTONS & ICONS (No missing glyphs / emoji boxes!)
        // ==========================================
        private void DrawVectorActionButton(Graphics g, Rectangle r, string action, bool isHovered, VisualTimerThemeColors c, Color accent, double scale)
        {
            float radius = (float)(6 * scale);
            RectangleF rf = new RectangleF(r.X, r.Y, r.Width, r.Height);

            using (GraphicsPath path = CreateRoundedRectanglePath(rf, radius))
            {
                using (SolidBrush bg = new SolidBrush(isHovered ? c.ButtonHover : c.ButtonBg))
                {
                    g.FillPath(bg, path);
                }
                if (isHovered)
                {
                    using (Pen hp = new Pen(Color.FromArgb(140, accent.R, accent.G, accent.B), 1.2f))
                    {
                        g.DrawPath(hp, path);
                    }
                }
            }

            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            Color iconColor = isHovered ? c.TextPrimary : c.ButtonText;

            using (SolidBrush iconBrush = new SolidBrush(iconColor))
            {
                if (action == "play")
                {
                    // Perfectly centered vector play triangle
                    PointF[] tri = new PointF[] {
                        new PointF(cx - 3.5f * (float)scale, cy - 5.5f * (float)scale),
                        new PointF(cx - 3.5f * (float)scale, cy + 5.5f * (float)scale),
                        new PointF(cx + 5f * (float)scale, cy)
                    };
                    g.FillPolygon(iconBrush, tri);
                }
                else if (action == "pause")
                {
                    // Perfectly centered vector pause bars
                    float barW = 2.6f * (float)scale;
                    float barH = 11f * (float)scale;
                    float gap = 2.2f * (float)scale;
                    g.FillRectangle(iconBrush, cx - barW - gap, cy - barH / 2f, barW, barH);
                    g.FillRectangle(iconBrush, cx + gap, cy - barH / 2f, barW, barH);
                }
                else if (action == "skip")
                {
                    // Clean vector skip (triangle + stop bar)
                    PointF[] tri = new PointF[] {
                        new PointF(cx - 5f * (float)scale, cy - 5f * (float)scale),
                        new PointF(cx - 5f * (float)scale, cy + 5f * (float)scale),
                        new PointF(cx + 1f * (float)scale, cy)
                    };
                    g.FillPolygon(iconBrush, tri);
                    g.FillRectangle(iconBrush, cx + 2.5f * (float)scale, cy - 5f * (float)scale, 2.2f * (float)scale, 10f * (float)scale);
                }
            }
        }

        private void DrawTextActionButton(Graphics g, Rectangle r, string text, bool isHovered, VisualTimerThemeColors c, Color accent, double scale)
        {
            float radius = (float)(6 * scale);
            RectangleF rf = new RectangleF(r.X, r.Y, r.Width, r.Height);

            using (GraphicsPath path = CreateRoundedRectanglePath(rf, radius))
            {
                using (SolidBrush bg = new SolidBrush(isHovered ? c.ButtonHover : c.ButtonBg))
                {
                    g.FillPath(bg, path);
                }
                if (isHovered)
                {
                    using (Pen hp = new Pen(Color.FromArgb(140, accent.R, accent.G, accent.B), 1.2f))
                    {
                        g.DrawPath(hp, path);
                    }
                }
            }

            using (Font font = new Font("Segoe UI", (float)(8f * scale), FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(isHovered ? c.TextPrimary : c.ButtonText))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(text, font, tb, rf, sf);
            }
        }

        private void DrawVectorButton(Graphics g, Rectangle r, string type, bool isHovered, VisualTimerThemeColors c, double scale)
        {
            RectangleF rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
            if (isHovered)
            {
                using (GraphicsPath path = CreateRoundedRectanglePath(rf, (float)(4 * scale)))
                using (SolidBrush hb = new SolidBrush(c.ButtonHover))
                {
                    g.FillPath(hb, path);
                }
            }

            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            Color iconColor = isHovered ? c.TextPrimary : c.TextMuted;

            if (type == "close")
            {
                // Vector 'X' icon
                float d = 3.5f * (float)scale;
                using (Pen p = new Pen(iconColor, 1.6f * (float)scale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(p, cx - d, cy - d, cx + d, cy + d);
                    g.DrawLine(p, cx - d, cy + d, cx + d, cy - d);
                }
            }
            else if (type == "reset")
            {
                // Vector circular reset / reload arrow
                float rad = 4f * (float)scale;
                using (Pen p = new Pen(iconColor, 1.4f * (float)scale))
                {
                    g.DrawArc(p, cx - rad, cy - rad, rad * 2f, rad * 2f, 45, 270);
                }
                using (SolidBrush b = new SolidBrush(iconColor))
                {
                    PointF[] arrow = new PointF[] {
                        new PointF(cx + rad - 1.5f * (float)scale, cy - rad - 2.5f * (float)scale),
                        new PointF(cx + rad + 2.5f * (float)scale, cy - rad + 0.5f * (float)scale),
                        new PointF(cx + rad - 1.5f * (float)scale, cy - rad + 3.5f * (float)scale)
                    };
                    g.FillPolygon(b, arrow);
                }
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2f;
            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void OnTimerMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ShowContextMenu(Cursor.Position);
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            // Check button hits
            if (rectBtnClose.Contains(e.Location))
            {
                prefs.PomodoroVisualTimerEnabled = false;
                prefs.Save();
                Hide();
                return;
            }

            if (rectBtnReset.Contains(e.Location))
            {
                // Snap position to top-right corner
                Rectangle screenArea = Screen.FromPoint(Cursor.Position).WorkingArea;
                prefs.PomodoroTimerCustomX = screenArea.Right - Width - 32;
                prefs.PomodoroTimerCustomY = screenArea.Top + 32;
                Location = new Point(prefs.PomodoroTimerCustomX, prefs.PomodoroTimerCustomY);
                prefs.Save();
                lastRenderKey = null;
                return;
            }

            if (rectBtnPlayPause.Contains(e.Location))
            {
                pomodoro.TogglePause();
                lastRenderKey = null;
                return;
            }

            if (rectBtnSkip.Contains(e.Location))
            {
                pomodoro.Skip();
                lastRenderKey = null;
                return;
            }

            if (rectBtnPlus5.Contains(e.Location))
            {
                pomodoro.AddMinutes(5);
                lastRenderKey = null;
                return;
            }

            // Clicked on card body: begin smooth dragging HUD
            isDragging = true;
            dragStartMouse = Cursor.Position;
            dragStartLocation = Location;
            Capture = true;
        }

        private void OnTimerMouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point cur = Cursor.Position;
                int nx = dragStartLocation.X + (cur.X - dragStartMouse.X);
                int ny = dragStartLocation.Y + (cur.Y - dragStartMouse.Y);

                Rectangle screenArea = Screen.FromPoint(cur).WorkingArea;
                nx = Math.Max(screenArea.Left, Math.Min(nx, screenArea.Right - Width));
                ny = Math.Max(screenArea.Top, Math.Min(ny, screenArea.Bottom - Height));

                Location = new Point(nx, ny);
                prefs.PomodoroTimerCustomX = nx;
                prefs.PomodoroTimerCustomY = ny;
                prefs.PomodoroTimerMode = "Detached";
                prefs.Save();
                return;
            }

            // Hover state updates
            string newHover = null;
            if (rectBtnClose.Contains(e.Location)) newHover = "Close";
            else if (rectBtnReset.Contains(e.Location)) newHover = "Reset";
            else if (rectBtnPlayPause.Contains(e.Location)) newHover = "PlayPause";
            else if (rectBtnSkip.Contains(e.Location)) newHover = "Skip";
            else if (rectBtnPlus5.Contains(e.Location)) newHover = "Plus5";

            if (newHover != hoveredButton)
            {
                hoveredButton = newHover;
                Cursor = newHover != null ? Cursors.Hand : Cursors.Default;
                lastRenderKey = null; // force redraw with new hover state
            }
        }

        private void OnTimerMouseUp(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                Capture = false;
            }
        }

        private void OnTimerMouseLeave(object sender, EventArgs e)
        {
            if (hoveredButton != null)
            {
                hoveredButton = null;
                Cursor = Cursors.Default;
                lastRenderKey = null;
            }
        }

        public void ShowContextMenu(Point screenPt)
        {
            ContextMenuStrip ctx = new ContextMenuStrip();
            ctx.Font = new Font("Segoe UI", 9f);

            // Phase actions
            if (pomodoro.Phase == PomodoroPhase.Idle)
            {
                ctx.Items.Add(string.Format("Start Focus ({0}m)", pomodoro.WorkMinutes), null, delegate { pomodoro.StartWork(); });
            }
            else
            {
                ctx.Items.Add(pomodoro.IsPaused ? "Resume Focus" : "Pause Focus", null, delegate { pomodoro.TogglePause(); });
                ctx.Items.Add("Skip to Next Session", null, delegate { pomodoro.Skip(); });
                ctx.Items.Add("Add 5 Minutes (+5m)", null, delegate { pomodoro.AddMinutes(5); });
                ctx.Items.Add("Reset Timer", null, delegate { pomodoro.Reset(); });
            }

            ctx.Items.Add(new ToolStripSeparator());

            // Themes
            ToolStripMenuItem themeMenu = new ToolStripMenuItem("Theme");
            foreach (VisualTimerTheme th in new[] { VisualTimerTheme.CozyBob, VisualTimerTheme.ModernGlass, VisualTimerTheme.MinimalPill, VisualTimerTheme.LightCeramic })
            {
                VisualTimerTheme target = th;
                string label = th == VisualTimerTheme.CozyBob ? "Cozy Bob (Warm Amber)" :
                               th == VisualTimerTheme.ModernGlass ? "Modern Glass (Obsidian)" :
                               th == VisualTimerTheme.MinimalPill ? "Minimal Pill (Compact)" : "Light Ceramic (Ivory)";
                ToolStripMenuItem item = new ToolStripMenuItem(label) { Checked = prefs.PomodoroTimerTheme == target.ToString() };
                item.Click += delegate {
                    prefs.PomodoroTimerTheme = target.ToString();
                    prefs.Save();
                    lastRenderKey = null;
                };
                themeMenu.DropDownItems.Add(item);
            }
            ctx.Items.Add(themeMenu);

            // Modes / Placement
            ToolStripMenuItem modeMenu = new ToolStripMenuItem("Placement & Docking");
            ToolStripMenuItem detachItem = new ToolStripMenuItem("Stationary Desktop HUD (Default)") { Checked = prefs.PomodoroTimerMode != "CompactBar" };
            detachItem.Click += delegate { prefs.PomodoroTimerMode = "Detached"; prefs.Save(); lastRenderKey = null; };
            ToolStripMenuItem barItem = new ToolStripMenuItem("Compact Bar Under Bob") { Checked = prefs.PomodoroTimerMode == "CompactBar" };
            barItem.Click += delegate { prefs.PomodoroTimerMode = "CompactBar"; prefs.Save(); lastRenderKey = null; };
            ToolStripMenuItem resetItem = new ToolStripMenuItem("Reset Position to Top-Right");
            resetItem.Click += delegate {
                Rectangle screenArea = Screen.FromPoint(Cursor.Position).WorkingArea;
                prefs.PomodoroTimerCustomX = screenArea.Right - Width - 32;
                prefs.PomodoroTimerCustomY = screenArea.Top + 32;
                Location = new Point(prefs.PomodoroTimerCustomX, prefs.PomodoroTimerCustomY);
                prefs.Save();
                lastRenderKey = null;
            };
            modeMenu.DropDownItems.AddRange(new ToolStripItem[] { detachItem, barItem, new ToolStripSeparator(), resetItem });
            ctx.Items.Add(modeMenu);

            // Scale
            ToolStripMenuItem sizeMenu = new ToolStripMenuItem("Size");
            AddScaleItem(sizeMenu, "Compact (85%)", 0.85);
            AddScaleItem(sizeMenu, "Normal (100%)", 1.0);
            AddScaleItem(sizeMenu, "Large (125%)", 1.25);
            ctx.Items.Add(sizeMenu);

            // Opacity
            ToolStripMenuItem opacityMenu = new ToolStripMenuItem("Opacity");
            AddOpacityItem(opacityMenu, "100%", 255);
            AddOpacityItem(opacityMenu, "90% (Recommended)", 230);
            AddOpacityItem(opacityMenu, "75%", 190);
            AddOpacityItem(opacityMenu, "50% (Subtle)", 128);
            ctx.Items.Add(opacityMenu);

            ToolStripMenuItem clickThrough = new ToolStripMenuItem("Let clicks pass through HUD") { Checked = prefs.PomodoroTimerClickThrough };
            clickThrough.Click += delegate {
                prefs.PomodoroTimerClickThrough = !prefs.PomodoroTimerClickThrough;
                prefs.Save();
                RecreateHandleStyle();
            };
            ctx.Items.Add(clickThrough);

            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add("Hide Visual Timer", null, delegate {
                prefs.PomodoroVisualTimerEnabled = false;
                prefs.Save();
                Hide();
            });

            ctx.Show(screenPt);
        }

        private void AddScaleItem(ToolStripMenuItem parent, string label, double scale)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label) { Checked = Math.Abs(prefs.PomodoroTimerScale - scale) < 0.05 };
            item.Click += delegate {
                prefs.PomodoroTimerScale = scale;
                prefs.Save();
                lastRenderKey = null;
            };
            parent.DropDownItems.Add(item);
        }

        private void AddOpacityItem(ToolStripMenuItem parent, string label, byte opacity)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label) { Checked = Math.Abs((int)prefs.PomodoroTimerOpacity - (int)opacity) < 15 };
            item.Click += delegate {
                prefs.PomodoroTimerOpacity = opacity;
                prefs.Save();
                lastRenderKey = null;
            };
            parent.DropDownItems.Add(item);
        }

        public static VisualTimerMode ParseMode(string str)
        {
            if (string.Equals(str, "CompactBar", StringComparison.OrdinalIgnoreCase)) return VisualTimerMode.CompactBar;
            return VisualTimerMode.Detached;
        }

        public static VisualTimerTheme ParseTheme(string str)
        {
            if (string.Equals(str, "ModernGlass", StringComparison.OrdinalIgnoreCase)) return VisualTimerTheme.ModernGlass;
            if (string.Equals(str, "MinimalPill", StringComparison.OrdinalIgnoreCase)) return VisualTimerTheme.MinimalPill;
            if (string.Equals(str, "LightCeramic", StringComparison.OrdinalIgnoreCase)) return VisualTimerTheme.LightCeramic;
            return VisualTimerTheme.CozyBob;
        }

        private void ReleaseBitmap()
        {
            if (currentBitmap != IntPtr.Zero)
            {
                if (dc != IntPtr.Zero && original != IntPtr.Zero) Native.SelectObject(dc, original);
                Native.DeleteObject(currentBitmap);
                currentBitmap = IntPtr.Zero;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                ReleaseBitmap();
                if (dc != IntPtr.Zero) Native.DeleteDC(dc);
            }
            base.Dispose(disposing);
        }
    }
}
