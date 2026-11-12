using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BobDesktop
{
    public enum DialogCategory
    {
        Idle,
        Petting,
        Focus,
        Break,
        Toy,
        Nap,
        Window
    }

    public static class CatDialogBank
    {
        private static readonly Dictionary<DialogCategory, string[]> Phrases = new Dictionary<DialogCategory, string[]>
        {
            {
                DialogCategory.Idle, new[]
                {
                    "Meow!",
                    "Purr... warm keyboard...",
                    "I am loaf.",
                    "*yawn* so sleepy...",
                    "What are we working on?",
                    "Need more tuna.",
                    "I saw a bug earlier!",
                    "Patrol mode: active.",
                    "Can I sit on your mouse?",
                    "Thinking about cat stuff.",
                    "Your screen is nice and warm.",
                    "Just stretching my paws."
                }
            },
            {
                DialogCategory.Petting, new[]
                {
                    "Purrrrr... ❤️",
                    "That's the spot!",
                    "Mreeeow! Best scratches ever.",
                    "*happy tail swish*",
                    "Head scratch approved!",
                    "Belly rub accepted ❤️",
                    "Purr purr purr~"
                }
            },
            {
                DialogCategory.Focus, new[]
                {
                    "Focus time! Bob is your study buddy 🍅",
                    "Quiet study paws 🐾",
                    "Stay hydrated!",
                    "You're doing great! Keep going.",
                    "Deep focus mode engaged.",
                    "No distractions, just work & cats.",
                    "Bob is watching you be productive!"
                }
            },
            {
                DialogCategory.Break, new[]
                {
                    "Break time! Stretch your legs! ☕",
                    "Step away and rest your eyes!",
                    "Bob demands a break pet!",
                    "Time for tea and snacks!",
                    "Great session! Relax a bit.",
                    "*chirp* Break time achieved!"
                }
            },
            {
                DialogCategory.Toy, new[]
                {
                    "MUST CATCH RED DOT!",
                    "Yarn ball spotted!",
                    "Pounce incoming!",
                    "Red dot can't hide from me!",
                    "Bat bat bat!"
                }
            },
            {
                DialogCategory.Nap, new[]
                {
                    "Zzz... 🐟",
                    "Zzz... tuna dreams...",
                    "Five more minutes...",
                    "*snore* *ear twitch*",
                    "Power napping..."
                }
            },
            {
                DialogCategory.Window, new[]
                {
                    "I fits, therefore I sits.",
                    "Nice window you have here.",
                    "Top of the window, top of the world!",
                    "Supervising from above.",
                    "Perch secured."
                }
            }
        };

        private static string lastPhrase = "";

        public static string GetRandom(DialogCategory category, Random rand)
        {
            if (rand == null) rand = new Random();
            string[] pool;
            if (!Phrases.TryGetValue(category, out pool) || pool.Length == 0) return "Meow!";

            for (int attempt = 0; attempt < 5; attempt++)
            {
                string candidate = pool[rand.Next(pool.Length)];
                if (candidate != lastPhrase || pool.Length == 1)
                {
                    lastPhrase = candidate;
                    return candidate;
                }
            }
            return pool[0];
        }

        public static string GetContextual(CatBrain brain, PomodoroTimer pomodoro, Random rand)
        {
            if (pomodoro != null && pomodoro.IsInWork && !pomodoro.IsPaused)
                return GetRandom(DialogCategory.Focus, rand);
            if (pomodoro != null && pomodoro.IsInBreak)
                return GetRandom(DialogCategory.Break, rand);
            if (brain != null && brain.ToyVisible)
                return GetRandom(DialogCategory.Toy, rand);
            if (brain != null && brain.Perched)
                return GetRandom(DialogCategory.Window, rand);
            if (brain != null && brain.State == -1)
                return GetRandom(DialogCategory.Nap, rand);

            return GetRandom(DialogCategory.Idle, rand);
        }
    }

    internal sealed class DialogOverlay : Form
    {
        private IntPtr dc, original;
        private IntPtr currentBitmap;
        private bool disposed;
        private double hideAt;
        private string activeText;
        private int lastCatX, lastCatY, lastCatW, lastCatH;
        private int lastHeadOffset;
        private Rectangle lastArea;

        public bool IsActive { get { return Visible && !disposed; } }

        public DialogOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            Text = "Bob speech";
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p = base.CreateParams;
                p.ExStyle |= Native.ExLayered | Native.ExToolWindow | Native.ExNoActivate | Native.ExTransparent;
                return p;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
            if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; } // HTTRANSPARENT
            base.WndProc(ref m);
        }

        public void ShowDialog(string text, double catX, double catY, int catWidth, int catHeight, Rectangle area, double now, double duration = 4.0)
        {
            ShowDialog(text, catX, catY, catWidth, catHeight, (int)(catHeight * 0.30), area, now, duration);
        }

        public void ShowDialog(string text, double catX, double catY, int catWidth, int catHeight, int headOffset, Rectangle area, double now, double duration = 4.0)
        {
            if (string.IsNullOrEmpty(text)) { HideDialog(); return; }

            activeText = text;
            hideAt = now + duration;
            lastCatX = (int)catX;
            lastCatY = (int)catY;
            lastCatW = catWidth;
            lastCatH = catHeight;
            lastHeadOffset = headOffset > 0 ? headOffset : (int)(catHeight * 0.30);
            lastArea = area;

            RenderBubble();
        }

        public void UpdatePosition(double catX, double catY, int catWidth, int catHeight, Rectangle area)
        {
            UpdatePosition(catX, catY, catWidth, catHeight, lastHeadOffset, area);
        }

        public void UpdatePosition(double catX, double catY, int catWidth, int catHeight, int headOffset, Rectangle area)
        {
            if (!IsActive) return;
            if (Math.Abs(lastCatX - (int)catX) < 2 && Math.Abs(lastCatY - (int)catY) < 2 && Math.Abs(lastHeadOffset - headOffset) < 3) return;

            lastCatX = (int)catX;
            lastCatY = (int)catY;
            lastCatW = catWidth;
            lastCatH = catHeight;
            if (headOffset > 0) lastHeadOffset = headOffset;
            lastArea = area;

            PositionOnly();
        }

        public void Tick(double now)
        {
            if (IsActive && now >= hideAt)
            {
                HideDialog();
            }
        }

        public void HideDialog()
        {
            if (Visible) Hide();
            activeText = null;
            ReleaseBitmap();
        }

        private void PositionOnly()
        {
            if (currentBitmap == IntPtr.Zero || !Visible) return;

            int bubbleW = Width;
            int bubbleH = Height;

            int headTop = lastCatY + lastHeadOffset;
            bool placeAbove = (headTop - bubbleH - 2) >= lastArea.Top;
            int posX = lastCatX + (lastCatW - bubbleW) / 2;
            posX = Math.Max(lastArea.Left + 8, Math.Min(posX, lastArea.Right - bubbleW - 8));
            int posY = placeAbove ? headTop - bubbleH - 2 : lastCatY + lastCatH - 6;

            Location = new Point(posX, posY);
            Native.POINT position = new Native.POINT(posX, posY);
            Native.POINT origin = new Native.POINT(0, 0);
            Native.SIZE dimensions = new Native.SIZE(bubbleW, bubbleH);
            Native.BLEND blend = new Native.BLEND { Op = 0, Flags = 0, Alpha = 255, Format = 1 };

            Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref position, ref dimensions, dc, ref origin, 0, ref blend, 2);
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002);
        }

        private void RenderBubble()
        {
            if (string.IsNullOrEmpty(activeText)) return;

            using (Font font = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Bitmap measureBmp = new Bitmap(1, 1))
            using (Graphics mg = Graphics.FromImage(measureBmp))
            {
                SizeF textSize = mg.MeasureString(activeText, font, 240);
                int padX = 16, padY = 10, tailH = 8;
                int bubbleW = Math.Max(60, (int)Math.Ceiling(textSize.Width) + padX * 2);
                int bubbleH = (int)Math.Ceiling(textSize.Height) + padY * 2 + tailH;

                int headTop = lastCatY + lastHeadOffset;
                bool placeAbove = (headTop - bubbleH - 2) >= lastArea.Top;

                ReleaseBitmap();

                using (Bitmap surface = new Bitmap(bubbleW, bubbleH, PixelFormat.Format32bppPArgb))
                using (Graphics g = Graphics.FromImage(surface))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    RectangleF rect = placeAbove
                        ? new RectangleF(1.5f, 1.5f, bubbleW - 3f, bubbleH - tailH - 3f)
                        : new RectangleF(1.5f, tailH + 1.5f, bubbleW - 3f, bubbleH - tailH - 3f);

                    float radius = 7f;
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
                        path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);

                        if (placeAbove)
                        {
                            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                            float tailX = bubbleW / 2f;
                            path.AddLine(rect.Right - radius * 2, rect.Bottom, tailX + 6, rect.Bottom);
                            path.AddLine(tailX + 6, rect.Bottom, tailX, bubbleH - 1.5f);
                            path.AddLine(tailX, bubbleH - 1.5f, tailX - 6, rect.Bottom);
                            path.AddLine(tailX - 6, rect.Bottom, rect.X + radius, rect.Bottom);
                            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
                        }
                        else
                        {
                            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
                            float tailX = bubbleW / 2f;
                            path.AddLine(rect.X, rect.Y + radius, rect.X, rect.Y);
                            path.AddLine(rect.X + radius, rect.Y, tailX - 6, rect.Y);
                            path.AddLine(tailX - 6, rect.Y, tailX, 1.5f);
                            path.AddLine(tailX, 1.5f, tailX + 6, rect.Y);
                        }
                        path.CloseFigure();

                        using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(248, 255, 253, 245)))
                        {
                            g.FillPath(fillBrush, path);
                        }
                        using (Pen borderPen = new Pen(Color.FromArgb(210, 65, 38, 20), 1.8f))
                        {
                            g.DrawPath(borderPen, path);
                        }
                    }

                    RectangleF textRect = placeAbove
                        ? new RectangleF(padX, padY, textSize.Width + 2, textSize.Height + 2)
                        : new RectangleF(padX, tailH + padY, textSize.Width + 2, textSize.Height + 2);

                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(45, 28, 16)))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(activeText, font, textBrush, new RectangleF(1, placeAbove ? 1 : tailH, bubbleW - 2, bubbleH - tailH - 2), sf);
                    }

                    currentBitmap = surface.GetHbitmap(Color.FromArgb(0));
                }

                if (dc == IntPtr.Zero) dc = Native.CreateCompatibleDC(IntPtr.Zero);
                IntPtr prev = Native.SelectObject(dc, currentBitmap);
                if (original == IntPtr.Zero) original = prev;

                int posX = lastCatX + (lastCatW - bubbleW) / 2;
                posX = Math.Max(lastArea.Left + 8, Math.Min(posX, lastArea.Right - bubbleW - 8));
                int posY = placeAbove ? headTop - bubbleH - 2 : lastCatY + lastCatH - 6;

                Size = new Size(bubbleW, bubbleH);
                Location = new Point(posX, posY);

                if (!Visible) Show();

                Native.POINT position = new Native.POINT(posX, posY);
                Native.POINT origin = new Native.POINT(0, 0);
                Native.SIZE dimensions = new Native.SIZE(bubbleW, bubbleH);
                Native.BLEND blend = new Native.BLEND { Op = 0, Flags = 0, Alpha = 255, Format = 1 };

                Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref position, ref dimensions, dc, ref origin, 0, ref blend, 2);
                Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002);
            }
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
                if (dc != IntPtr.Zero) { Native.DeleteDC(dc); dc = IntPtr.Zero; }
            }
            base.Dispose(disposing);
        }
    }
}
