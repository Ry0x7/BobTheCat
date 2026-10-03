using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace BobDesktop
{
    public enum Behavior { Follow, Wander, Stay }

    public class Preferences
    {
        [XmlIgnore] public bool Transient;
        public Behavior Mode = Behavior.Wander;
        public double Scale = 1.0;
        public int SleepAfterSeconds = 60;
        public bool ClickThrough = false;
        public bool SoundsEnabled = true;
        public bool WindowInteractions = true;
        public bool ToysEnabled = true, EnergeticPlay = true, CursorCuriosity = true, LaserFollowsCursor = false;
        public bool HoverPettingEnabled = true;
        public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BobDesktopPet"); } }
        public static Preferences Load()
        {
            try { string path=Path.Combine(Folder,"settings.xml"); if(!File.Exists(path)) path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BlepDesktopPet","settings.xml"); using (var input = File.OpenRead(path)) return (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(input); }
            catch { return new Preferences(); }
        }
        public void Save()
        {
            if (Transient) return;
            try { Directory.CreateDirectory(Folder); using (var output = File.Create(Path.Combine(Folder, "settings.xml"))) new XmlSerializer(typeof(Preferences)).Serialize(output, this); }
            catch (Exception error) { Program.Log(error.ToString()); }
        }
    }

    public class WindowLandmark
    {
        public IntPtr Handle;
        public Rectangle Bounds;
        public PointF Position;
        public static WindowLandmark Create(IntPtr handle, Rectangle bounds, Rectangle area, Size pet)
        {
            double x = CatBrain.Clamp(bounds.Right - pet.Width - 18, area.Left, area.Right - pet.Width);
            double footOffset = pet.Height * 202.0 / 208;
            double y = bounds.Top - footOffset;
            // A maximized window has no space above it; settle beside its lower corner instead.
            if (y < area.Top) y = bounds.Bottom - pet.Height - 16;
            return new WindowLandmark { Handle=handle, Bounds=bounds, Position=new PointF((float)x,(float)CatBrain.Clamp(y,area.Top,area.Bottom-pet.Height)) };
        }
    }

    internal static class Native
    {
        public const int ExLayered = 0x80000, ExToolWindow = 0x80, ExNoActivate = 0x8000000, ExTransparent = 0x20;
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int W, H; public SIZE(int w, int h) { W = w; H = h; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLEND { public byte Op, Flags, Alpha, Format; }
        [StructLayout(LayoutKind.Sequential)] public struct LASTINPUT { public uint Size, Time; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr destDc, ref POINT dest, ref SIZE size, IntPtr srcDc, ref POINT src, int key, ref BLEND blend, int flags);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref LASTINPUT input);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr process, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("dwmapi.dll", EntryPoint="DwmGetWindowAttribute")] public static extern int GetFrameBounds(IntPtr hwnd, int attribute, out RECT rect, int size);
        [DllImport("dwmapi.dll", EntryPoint="DwmGetWindowAttribute")] public static extern int GetCloaked(IntPtr hwnd, int attribute, out int cloaked, int size);
        public static double IdleSeconds()
        {
            LASTINPUT value = new LASTINPUT(); value.Size = (uint)Marshal.SizeOf(typeof(LASTINPUT));
            if (!GetLastInputInfo(ref value)) return 0;
            return unchecked((uint)Environment.TickCount - value.Time) / 1000.0;
        }
    }

    internal sealed class SoundBank : IDisposable
    {
        private readonly Dictionary<string, SoundPlayer> players = new Dictionary<string, SoundPlayer>();
        private readonly List<Stream> streams = new List<Stream>();
        private double lastPlayed = -100;
        private readonly Random soundRandom = new Random();
        private int lastMeow = -1;
        private readonly string[] meows = { "meow", "meow2", "meow3", "meow4" };
        public bool Enabled;
        public SoundBank()
        {
            foreach (string name in new[] { "meow", "meow2", "meow3", "meow4", "chirp" })
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Bob." + name + ".wav");
                if (stream == null) throw new Exception("Missing sound: " + name);
                streams.Add(stream);
                SoundPlayer player = new SoundPlayer(stream); player.Load(); players.Add(name,player);
            }
        }
        internal string ChooseMeow()
        {
            int next = soundRandom.Next(lastMeow < 0 ? meows.Length : meows.Length-1);
            if (lastMeow >= 0 && next >= lastMeow) next++;
            lastMeow = next;
            return meows[next];
        }
        public bool Play(string name, double now, bool interaction)
        {
            if (!Enabled || now-lastPlayed < (interaction ? .35 : 12)) return false;
            if (name == "meow") name = ChooseMeow();
            try { players[name].Play(); lastPlayed=now; return true; }
            catch (Exception error) { Program.Log(error.ToString()); return false; }
        }
        public void Stop() { foreach (SoundPlayer player in players.Values) player.Stop(); }
        public void Dispose() { Stop(); foreach (SoundPlayer player in players.Values) player.Dispose(); foreach (Stream stream in streams) stream.Dispose(); }
    }

    internal sealed class FixtureForm : Form
    {
        protected override bool ShowWithoutActivation { get { return true; } }
    }

    internal sealed class Sprite : IDisposable
    {
        public readonly Bitmap Image;
        public readonly IntPtr Handle;
        public Sprite(Bitmap image) { Image = image; Handle = image.GetHbitmap(Color.FromArgb(0)); }
        public void Dispose() { Native.DeleteObject(Handle); Image.Dispose(); }
    }

    internal sealed class PetForm : Form
    {
        private readonly Preferences prefs;
        private readonly CatBrain brain = new CatBrain();
        private readonly Bitmap atlas;
        private readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly SoundBank sounds = new SoundBank();
        private readonly ToyOverlay toy = new ToyOverlay();
        private readonly HoverPetting petting = new HoverPetting();
        private readonly ToolStripMenuItem status = new ToolStripMenuItem("Bob is waking up");
        private readonly Dictionary<Behavior, ToolStripMenuItem> modeItems = new Dictionary<Behavior, ToolStripMenuItem>();
        private readonly ToolStripMenuItem pauseItem = new ToolStripMenuItem("Pause");
        private readonly ToolStripMenuItem clickThroughItem = new ToolStripMenuItem("Let clicks pass through Bob");
        private IntPtr memoryDc, oldObject;
        private Sprite current;
        private Icon trayIcon;
        private Point lastCursor, downCursor, dragOrigin;
        private bool dragging, pressed, disposed;
        private double lastCursorMove, previousTick, stateStarted, actionUntil, lastStatusUpdate;
        private int lastState = int.MinValue, actionState = -2;
        private readonly string smokeReport;
        private int renders, frameChanges, renderErrors;
        private string frameKey;
        private uint gdiBefore;
        private double lastWindowsRefresh = -10;
        private bool wasPerched;
        private double behaviorTime;
        private FixtureForm fixture;
        private static readonly int[] Counts = { 6, 8, 8, 4, 5, 8, 6, 6, 6, 8, 8 };
        private static readonly int[][] Durations = {
            new[] {280,110,110,140,140,320}, new[] {120,120,120,120,120,120,120,220}, new[] {120,120,120,120,120,120,120,220},
            new[] {140,140,140,280}, new[] {140,140,140,140,280}, new[] {140,140,140,140,140,140,140,240},
            new[] {150,150,150,150,150,260}, new[] {120,120,120,120,120,220}, new[] {150,150,150,150,150,280}
        };

        public PetForm(string smoke)
        {
            smokeReport = smoke;
            prefs = smoke == null ? Preferences.Load() : new Preferences();
            prefs.Transient = smoke != null;
            if (prefs.Scale != 0.75 && prefs.Scale != 1 && prefs.Scale != 1.5) prefs.Scale = 1;
            if (prefs.SleepAfterSeconds < 15 || prefs.SleepAfterSeconds > 600) prefs.SleepAfterSeconds = 60;
            brain.Mode = prefs.Mode;
            brain.WindowInteractions = prefs.WindowInteractions;
            brain.ToysEnabled=prefs.ToysEnabled;brain.EnergeticPlay=prefs.EnergeticPlay;brain.CursorCuriosity=prefs.CursorCuriosity;brain.LaserFollowsCursor=prefs.LaserFollowsCursor;
            sounds.Enabled = smoke == null && prefs.SoundsEnabled;
            atlas = Program.LoadAtlas();
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Text = "Bob The Cat";
            ClientSize = PetSize;
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            brain.X = area.Right - PetSize.Width - 36;
            brain.Y = area.Bottom - PetSize.Height - 12;
            Location = new Point((int)brain.X, (int)brain.Y);
            lastCursor = Cursor.Position;
            MakeMenu();
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Bob.ico")) trayIcon = new Icon(stream);
            Icon = trayIcon;
            tray.Icon = trayIcon;
            tray.Text = "Bob - right-click for controls";
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { BringBack(); };
            timer.Interval = 33;
            timer.Tick += delegate { TickPet(); };
            MouseDown += OnPetMouseDown;
            MouseMove += OnPetMouseMove;
            MouseUp += OnPetMouseUp;
            MouseDoubleClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) React(4); };
        }
        private Size PetSize { get { return new Size((int)(192 * prefs.Scale), (int)(208 * prefs.Scale)); } }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { CreateParams p = base.CreateParams; p.ExStyle |= Native.ExLayered | Native.ExToolWindow | Native.ExNoActivate; if (prefs != null && prefs.ClickThrough) p.ExStyle |= Native.ExTransparent; return p; }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
            if (m.Msg == 0x84 && current != null && !pressed)
            {
                long value = m.LParam.ToInt64();
                Point local = PointToClient(new Point((short)(value & 0xffff), (short)((value >> 16) & 0xffff)));
                if (local.X < 0 || local.Y < 0 || local.X >= current.Image.Width || local.Y >= current.Image.Height || current.Image.GetPixel(local.X, local.Y).A < 24)
                { m.Result = new IntPtr(-1); return; }
            }
            base.WndProc(ref m);
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            gdiBefore = Native.GetGuiResources(Process.GetCurrentProcess().Handle, 0);
            memoryDc = Native.CreateCompatibleDC(IntPtr.Zero);
            if (memoryDc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            tray.Visible = true;
            if (smokeReport != null)
            {
                Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
                fixture = new FixtureForm { Text="Bob window-interaction check", StartPosition=FormStartPosition.Manual, Location=new Point(area.Left+100,area.Top+260), Size=new Size(460,340) };
                fixture.Show();
            }
            Render(0, 0);
            timer.Start();
            if (smokeReport == null)
            {
                tray.BalloonTipTitle = "Bob is here";
                tray.BalloonTipText = "Drag to move. Click to wave, double-click to jump. Right-click Bob or the tray cat for controls.";
                tray.ShowBalloonTip(4000);
            }
        }
        private void MakeMenu()
        {
            status.Enabled = false; menu.Items.Add(status); menu.Items.Add(new ToolStripSeparator());
            AddMode("Follow my cursor", Behavior.Follow);
            AddMode("Autonomous cat life", Behavior.Wander);
            AddMode("Stay here", Behavior.Stay);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Take a nap", null, delegate { brain.ForcedSleep = true; actionState = -2; });
            menu.Items.Add("Wake up", null, delegate { brain.Wake(); React(3); });
            menu.Items.Add("Play / jump", null, delegate { React(4); });
            menu.Items.Add("Pet Bob / meow", null, delegate { React(3,"meow"); });
            menu.Items.Add("Scratch Bob's head",null,delegate { DoAction(CatAction.HeadScratch); });
            menu.Items.Add("Belly rub",null,delegate { DoAction(CatAction.BellyRub); });
            ToolStripMenuItem actions=new ToolStripMenuItem("Cat actions");
            foreach(CatAction action in new[] { CatAction.Loaf,CatAction.Stretch,CatAction.Scratch,CatAction.Groom,CatAction.Roll,CatAction.Hop,CatAction.Zoomies,CatAction.Cling,CatAction.Fall })
            {
                CatAction chosen=action;
                string label=action==CatAction.Cling?"Grab a window":action==CatAction.Fall?"Let go / fall":action==CatAction.Zoomies?"Run around":action.ToString();
                actions.DropDownItems.Add(label,null,delegate { DoAction(chosen); });
            }
            menu.Items.Add(actions);
            ToolStripMenuItem toys=new ToolStripMenuItem("Toys");
            toys.DropDownItems.Add("Spawn a ball of yarn",null,delegate { DoAction(CatAction.Yarn); });
            toys.DropDownItems.Add("Spawn a laser dot",null,delegate { DoAction(CatAction.Laser); });
            toys.DropDownItems.Add("Put toys away",null,delegate { brain.RemoveToy();toy.Hide(); });
            ToolStripMenuItem laserCursor=new ToolStripMenuItem("Laser follows my cursor") { Checked=prefs.LaserFollowsCursor };
            laserCursor.Click+=delegate { prefs.LaserFollowsCursor=!prefs.LaserFollowsCursor;brain.LaserFollowsCursor=prefs.LaserFollowsCursor;laserCursor.Checked=prefs.LaserFollowsCursor;prefs.Save(); };toys.DropDownItems.Add(laserCursor);
            menu.Items.Add(toys);
            pauseItem.Click += delegate { brain.Paused = !brain.Paused; pauseItem.Text = brain.Paused ? "Resume" : "Pause"; };
            menu.Items.Add(pauseItem);
            ToolStripMenuItem sizes = new ToolStripMenuItem("Size");
            AddSize(sizes, "Small", 0.75); AddSize(sizes, "Normal", 1); AddSize(sizes, "Large", 1.5); menu.Items.Add(sizes);
            ToolStripMenuItem naps = new ToolStripMenuItem("Nap after inactivity");
            foreach (int seconds in new[] {30, 60, 180})
            {
                int delay = seconds;
                ToolStripMenuItem item = new ToolStripMenuItem(seconds == 30 ? "30 seconds" : seconds == 60 ? "1 minute" : "3 minutes");
                item.Click += delegate { prefs.SleepAfterSeconds = delay; prefs.Save(); foreach (ToolStripMenuItem other in naps.DropDownItems) other.Checked = other == item; };
                item.Checked = prefs.SleepAfterSeconds == seconds;
                naps.DropDownItems.Add(item);
            }
            menu.Items.Add(naps);
            clickThroughItem.Checked = prefs.ClickThrough;
            clickThroughItem.Click += delegate {
                prefs.ClickThrough = !prefs.ClickThrough; clickThroughItem.Checked = prefs.ClickThrough;
                int style = Native.GetWindowLong(Handle, -20);
                Native.SetWindowLong(Handle, -20, prefs.ClickThrough ? style | Native.ExTransparent : style & ~Native.ExTransparent);
                prefs.Save();
            };
            menu.Items.Add(clickThroughItem);
            ToolStripMenuItem soundItem = new ToolStripMenuItem("Sound effects"); soundItem.Checked=prefs.SoundsEnabled;
            soundItem.Click += delegate { prefs.SoundsEnabled=!prefs.SoundsEnabled; soundItem.Checked=prefs.SoundsEnabled; sounds.Enabled=smokeReport==null&&prefs.SoundsEnabled; if (!sounds.Enabled) sounds.Stop(); prefs.Save(); };
            menu.Items.Add(soundItem);
            ToolStripMenuItem windowsItem = new ToolStripMenuItem("Play around open windows"); windowsItem.Checked=prefs.WindowInteractions;
            windowsItem.Click += delegate { prefs.WindowInteractions=!prefs.WindowInteractions; windowsItem.Checked=prefs.WindowInteractions; brain.WindowInteractions=prefs.WindowInteractions; prefs.Save(); };
            menu.Items.Add(windowsItem);
            ToolStripMenuItem toyItem=new ToolStripMenuItem("Allow toys") { Checked=prefs.ToysEnabled };
            toyItem.Click+=delegate { prefs.ToysEnabled=!prefs.ToysEnabled;brain.ToysEnabled=prefs.ToysEnabled;toyItem.Checked=prefs.ToysEnabled;if(!prefs.ToysEnabled){brain.RemoveToy();toy.Hide();}prefs.Save(); };menu.Items.Add(toyItem);
            ToolStripMenuItem energyItem=new ToolStripMenuItem("Energetic play") { Checked=prefs.EnergeticPlay };
            energyItem.Click+=delegate { prefs.EnergeticPlay=!prefs.EnergeticPlay;brain.EnergeticPlay=prefs.EnergeticPlay;energyItem.Checked=prefs.EnergeticPlay;if(!prefs.EnergeticPlay)brain.Wake();prefs.Save(); };menu.Items.Add(energyItem);
            ToolStripMenuItem curiousItem=new ToolStripMenuItem("Notice my nearby cursor") { Checked=prefs.CursorCuriosity };
            curiousItem.Click+=delegate { prefs.CursorCuriosity=!prefs.CursorCuriosity;brain.CursorCuriosity=prefs.CursorCuriosity;curiousItem.Checked=prefs.CursorCuriosity;prefs.Save(); };menu.Items.Add(curiousItem);
            ToolStripMenuItem hoverItem=new ToolStripMenuItem("Pet with cursor hover") { Checked=prefs.HoverPettingEnabled };
            hoverItem.Click+=delegate { prefs.HoverPettingEnabled=!prefs.HoverPettingEnabled;hoverItem.Checked=prefs.HoverPettingEnabled;petting.Reset();prefs.Save(); };menu.Items.Add(hoverItem);
            menu.Items.Add("Bring Bob here", null, delegate { BringBack(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit Bob", null, delegate { Close(); });
        }
        private void AddMode(string label, Behavior mode)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label); item.Checked = prefs.Mode == mode;
            item.Click += delegate { prefs.Mode = brain.Mode = mode; brain.Wake(); foreach (var entry in modeItems) entry.Value.Checked = entry.Key == mode; prefs.Save(); };
            modeItems.Add(mode, item); menu.Items.Add(item);
        }
        private void AddSize(ToolStripMenuItem parent, string label, double scale)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label); item.Checked = prefs.Scale == scale;
            item.Click += delegate {
                prefs.Scale = scale;
                ReleaseFrames(); current = null; frameKey = null;
                ClientSize = PetSize;
                Rectangle area = Screen.FromPoint(new Point((int)brain.X, (int)brain.Y)).WorkingArea;
                brain.X = CatBrain.Clamp(brain.X, area.Left, area.Right - PetSize.Width); brain.Y = CatBrain.Clamp(brain.Y, area.Top, area.Bottom - PetSize.Height);
                foreach (ToolStripMenuItem other in parent.DropDownItems) other.Checked = other == item;
                prefs.Save(); Render(0, 0);
            };
            parent.DropDownItems.Add(item);
        }
        private void BringBack()
        {
            Point mouse = Cursor.Position; Rectangle area = Screen.FromPoint(mouse).WorkingArea;
            brain.X = CatBrain.Clamp(mouse.X + 70, area.Left, area.Right - PetSize.Width);
            brain.Y = CatBrain.Clamp(mouse.Y - PetSize.Height / 2, area.Top, area.Bottom - PetSize.Height);
            brain.Paused = false; pauseItem.Text = "Pause"; brain.Wake(); React(3);
        }
        private void React(int row) { React(row,row==4?"chirp":"meow"); }
        private void DoAction(CatAction action)
        {
            Rectangle area=Screen.FromPoint(new Point((int)brain.X+PetSize.Width/2,(int)brain.Y+PetSize.Height/2)).WorkingArea;
            brain.Windows=FindWindows(area);
            if(!brain.StartAction(action,behaviorTime,area,PetSize))
            {
                status.Text=action==CatAction.Cling?"No suitable open window nearby":"Turn on Allow toys first";
                return;
            }
            actionState=-2;pauseItem.Text="Pause";lastState=int.MinValue;
            sounds.Play(action==CatAction.HeadScratch || action==CatAction.BellyRub?"meow":"chirp",clock.Elapsed.TotalSeconds,true);
        }
        private void React(int row, string sound)
        {
            brain.Wake(); brain.Paused = false; pauseItem.Text = "Pause";
            actionState = row; actionUntil = behaviorTime + (row == 4 ? 0.84 : 0.70);
            brain.HoldUntil = actionUntil + 0.4;
            lastState = int.MinValue;
            sounds.Play(sound,clock.Elapsed.TotalSeconds,true);
        }
        private void OnPetMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { menu.Show(Cursor.Position); return; }
            if (e.Button != MouseButtons.Left) return;
            brain.Wake(); pressed = true; dragging = false; Capture = true;
            downCursor = Cursor.Position; dragOrigin = new Point((int)brain.X, (int)brain.Y);
        }
        private void OnPetMouseMove(object sender, MouseEventArgs e)
        {
            if (!pressed) return;
            Point cursor = Cursor.Position;
            if (Math.Abs(cursor.X - downCursor.X) + Math.Abs(cursor.Y - downCursor.Y) > 5) dragging = true;
            if (dragging)
            {
                Rectangle area = Screen.FromPoint(cursor).WorkingArea;
                brain.X = CatBrain.Clamp(dragOrigin.X + cursor.X - downCursor.X, area.Left, area.Right - PetSize.Width);
                brain.Y = CatBrain.Clamp(dragOrigin.Y + cursor.Y - downCursor.Y, area.Top, area.Bottom - PetSize.Height);
                brain.HoldUntil = behaviorTime + 8;
                Render(0, 0);
            }
        }
        private void OnPetMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            bool moved = dragging; pressed = dragging = false; Capture = false;
            if (!moved && e.Clicks < 2) React(3,"meow");
        }
        private void TickPet()
        {
            try
            {
                double now = clock.Elapsed.TotalSeconds;
                double dt = Math.Min(0.1, Math.Max(0.001, now - previousTick)); previousTick = now;
                Point cursor = Cursor.Position;
                if (Math.Abs(cursor.X - lastCursor.X) + Math.Abs(cursor.Y - lastCursor.Y) > 3) { lastCursorMove = now; lastCursor = cursor; }
                if (smokeReport != null && now > 10) { WriteSmoke(); Close(); return; }
                if (pressed || menu.Visible || brain.Paused) return;
                behaviorTime+=dt;
                Point local=new Point(cursor.X-Left,cursor.Y-Top);
                bool onFur=current!=null && local.X>=0 && local.Y>=0 && local.X<current.Image.Width && local.Y<current.Image.Height && current.Image.GetPixel(local.X,local.Y).A>48;
                if(prefs.HoverPettingEnabled && !brain.Moving && brain.Action!=CatAction.Cling && brain.Action!=CatAction.Fall && brain.Action!=CatAction.Hop)
                {
                    PetRegion region=petting.Update(cursor,new Rectangle(Left,Top,PetSize.Width,PetSize.Height),prefs.Scale,onFur,behaviorTime);
                    if(region!=PetRegion.None)DoAction(region==PetRegion.Head?CatAction.HeadScratch:CatAction.BellyRub);
                }
                else petting.Reset();
                Rectangle area = brain.Mode == Behavior.Follow ? Screen.FromPoint(cursor).WorkingArea : Screen.FromPoint(new Point((int)brain.X + PetSize.Width/2, (int)brain.Y + PetSize.Height/2)).WorkingArea;
                if (prefs.WindowInteractions && now-lastWindowsRefresh > 2)
                {
                    brain.Windows=FindWindows(area); lastWindowsRefresh=now;
                }
                brain.Tick(dt, behaviorTime, cursor, area, PetSize, Native.IdleSeconds(), onFur?3.1:now-lastCursorMove, prefs.SleepAfterSeconds);
                int state = actionState != -2 && behaviorTime < actionUntil ? actionState : brain.State;
                if (smokeReport != null) state = now < 1.2 ? 0 : now < 2.4 ? 1 : now < 3.2 ? 3 : now < 4.2 ? 4 : now < 5.2 ? -1 : now<6?20:now<7?21:now<8?23:now<9?25:26;
                if (state != lastState)
                {
                    if (state==4) sounds.Play("chirp",now,false);
                    else if (state==-1) sounds.Play("meow",now,false);
                    stateStarted = behaviorTime; lastState = state;
                }
                if (brain.Perched && !wasPerched) sounds.Play("meow",now,false);
                wasPerched=brain.Perched;
                int row = state, frame = 0;
                if (state == -1) { row = 0; frame = ((int)((behaviorTime - stateStarted) / 1.2) % 2) + 2; }
                else if (state >= 100) { row = 9 + (state - 100) / 8; frame = (state - 100) % 8; }
                else frame = FrameAt(row, (behaviorTime - stateStarted) * (brain.Action==CatAction.Zoomies?1800:1000));
                Render(row, frame);
                toy.DrawToy(brain.Toy,((int)(now*8))%6,brain.ToyX,brain.ToyY,prefs.Scale);
                if (now - lastStatusUpdate > 1)
                {
                    status.Text = brain.Curious?"Bob noticed your cursor":"Bob: "+brain.ActivityName;
                    lastStatusUpdate = now;
                }
            }
            catch (Exception error) { Program.Log(error.ToString()); renderErrors++; if (renderErrors > 3) Close(); }
        }
        private List<WindowLandmark> FindWindows(Rectangle area)
        {
            List<WindowLandmark> windows = new List<WindowLandmark>();
            Native.EnumWindows(delegate(IntPtr window, IntPtr parameter)
            {
                if (windows.Count>=24) return false;
                if (window==Handle || !Native.IsWindowVisible(window) || Native.IsIconic(window) || Native.GetWindow(window,4)!=IntPtr.Zero) return true;
                if ((Native.GetWindowLong(window,-20)&Native.ExToolWindow)!=0 || (Native.GetWindowLong(window,-16)&0x00C00000)==0) return true;
                int cloaked;
                if (Native.GetCloaked(window,14,out cloaked,4)==0 && cloaked!=0) return true;
                Native.RECT rect;
                if (Native.GetFrameBounds(window,9,out rect,Marshal.SizeOf(typeof(Native.RECT)))!=0 && !Native.GetWindowRect(window,out rect)) return true;
                Rectangle bounds=Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
                if (bounds.Width<220 || bounds.Height<140 || !area.IntersectsWith(bounds)) return true;
                windows.Add(WindowLandmark.Create(window,bounds,area,PetSize));
                return true;
            },IntPtr.Zero);
            return windows;
        }
        internal static int FrameAt(int row, double milliseconds)
        {
            if(row>=20 && row<=29) return (int)(milliseconds/160)%6;
            int[] values = Durations[row]; int total = 0; foreach (int value in values) total += value;
            double time = milliseconds % total;
            for (int i = 0; i < values.Length; i++) { if (time < values[i]) return i; time -= values[i]; }
            return 0;
        }
        private Sprite GetSprite(int row, int frame)
        {
            string key = row + ":" + frame;
            Sprite sprite;
            if (cache.TryGetValue(key, out sprite)) return sprite;
            Bitmap image = new Bitmap(PetSize.Width, PetSize.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.CompositingMode = CompositingMode.SourceCopy; g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
                if(row>=20 && row<=29)
                {
                    using(Bitmap pose=ExtraArt.Make(atlas,row,frame))g.DrawImage(pose,new Rectangle(Point.Empty,PetSize),new Rectangle(0,0,pose.Width,pose.Height),GraphicsUnit.Pixel);
                }
                else g.DrawImage(atlas, new Rectangle(Point.Empty, PetSize), new Rectangle(frame * 192, row * 208, 192, 208), GraphicsUnit.Pixel);
            }
            sprite = new Sprite(image); cache.Add(key, sprite); return sprite;
        }
        private void Render(int row, int frame)
        {
            Sprite sprite = GetSprite(row, frame);
            Native.POINT position = new Native.POINT((int)Math.Round(brain.X), (int)Math.Round(brain.Y));
            if (current == sprite && Left == position.X && Top == position.Y) return;
            IntPtr prior = Native.SelectObject(memoryDc, sprite.Handle);
            if (oldObject == IntPtr.Zero) oldObject = prior;
            Native.SIZE size = new Native.SIZE(PetSize.Width, PetSize.Height); Native.POINT source = new Native.POINT(0, 0);
            Native.BLEND blend = new Native.BLEND { Op = 0, Flags = 0, Alpha = 255, Format = 1 };
            if (!Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref position, ref size, memoryDc, ref source, 0, ref blend, 2)) throw new Win32Exception(Marshal.GetLastWin32Error());
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002);
            current = sprite; renders++;
            string nextKey = row + ":" + frame;
            if (nextKey != frameKey) { frameChanges++; frameKey = nextKey; }
        }
        private void WriteSmoke()
        {
            Rectangle fixtureArea=Screen.FromPoint(fixture.Location).WorkingArea;
            List<WindowLandmark> discovered=FindWindows(fixtureArea);
            if (!discovered.Exists(delegate(WindowLandmark window) { return window.Handle==fixture.Handle; })) throw new Exception("Visible window discovery failed");
            fixture.WindowState=FormWindowState.Minimized;
            if (FindWindows(fixtureArea).Exists(delegate(WindowLandmark window) { return window.Handle==fixture.Handle; })) throw new Exception("Minimized window was eligible");
            fixture.Close(); fixture.Dispose(); fixture=null;
            Point corner = PointToScreen(Point.Empty);
            IntPtr emptyHit = Native.SendMessage(Handle, 0x84, IntPtr.Zero, EncodePoint(corner));
            Point solid = Point.Empty;
            bool found = false;
            for (int y = 0; y < current.Image.Height && !found; y++)
                for (int x = 0; x < current.Image.Width; x++)
                    if (current.Image.GetPixel(x,y).A > 200) { solid = PointToScreen(new Point(x,y)); found = true; break; }
            IntPtr solidHit = Native.SendMessage(Handle, 0x84, IntPtr.Zero, EncodePoint(solid));
            if (emptyHit.ToInt64() != -1 || solidHit.ToInt64() != 1) throw new Exception("Transparent/solid hit testing failed");
            FindMenu("Take a nap").PerformClick();
            if (!brain.ForcedSleep) throw new Exception("Nap menu failed");
            FindMenu("Wake up").PerformClick();
            if (brain.ForcedSleep) throw new Exception("Wake menu failed");
            pauseItem.PerformClick();
            if (!brain.Paused) throw new Exception("Pause menu failed");
            pauseItem.PerformClick();
            if (brain.Paused) throw new Exception("Resume menu failed");
            clickThroughItem.PerformClick();
            if ((Native.GetWindowLong(Handle,-20) & Native.ExTransparent) == 0) throw new Exception("Click-through setting failed");
            clickThroughItem.PerformClick();
            foreach (ToolStripMenuItem item in FindMenu("Size").DropDownItems) if (item.Text == "Large") item.PerformClick();
            if (PetSize.Width != 288) throw new Exception("Large size menu failed");
            foreach (ToolStripMenuItem item in FindMenu("Size").DropDownItems) if (item.Text == "Normal") item.PerformClick();
            if (PetSize.Width != 192) throw new Exception("Normal size menu failed");
            foreach (Behavior mode in new[] { Behavior.Wander, Behavior.Stay, Behavior.Follow })
            {
                modeItems[mode].PerformClick();
                if (brain.Mode != mode) throw new Exception("Behavior menu failed");
            }
            FindMenu("Sound effects").PerformClick(); if (prefs.SoundsEnabled) throw new Exception("Mute menu failed");
            FindMenu("Sound effects").PerformClick(); if (!prefs.SoundsEnabled) throw new Exception("Unmute menu failed");
            FindMenu("Play around open windows").PerformClick(); if (brain.WindowInteractions) throw new Exception("Window interaction toggle failed");
            FindMenu("Play around open windows").PerformClick(); if (!brain.WindowInteractions) throw new Exception("Window interaction toggle restore failed");
            for(int actionRow=20;actionRow<=29;actionRow++)for(int actionFrame=0;actionFrame<6;actionFrame++)Render(actionRow,actionFrame);
            toy.DrawToy(ToyKind.Yarn,0,brain.X+80,brain.Y+80,1);
            if(!Native.IsWindowVisible(toy.Handle))throw new Exception("Yarn overlay did not show");
            int toyStyle=Native.GetWindowLong(toy.Handle,-20);
            if((toyStyle&Native.ExNoActivate)==0 || (toyStyle&Native.ExTransparent)==0)throw new Exception("Toy intercepted input");
            if(Native.SendMessage(toy.Handle,0x84,IntPtr.Zero,EncodePoint(toy.Location)).ToInt64()!=-1)throw new Exception("Toy hit test failed");
            toy.DrawToy(ToyKind.Laser,1,brain.X+120,brain.Y+90,1.5);toy.Hide();
            ToolStripMenuItem toyMenu=FindMenu("Toys");((ToolStripMenuItem)toyMenu.DropDownItems[0]).PerformClick();
            if(brain.Toy!=ToyKind.Yarn)throw new Exception("Spawn yarn menu failed");
            ((ToolStripMenuItem)toyMenu.DropDownItems[2]).PerformClick();if(brain.ToyVisible)throw new Exception("Put toys away menu failed");
            ((ToolStripMenuItem)toyMenu.DropDownItems[1]).PerformClick();if(brain.Toy!=ToyKind.Laser)throw new Exception("Spawn laser menu failed");
            ((ToolStripMenuItem)toyMenu.DropDownItems[2]).PerformClick();
            ((ToolStripMenuItem)FindMenu("Cat actions").DropDownItems[0]).PerformClick();if(brain.Action!=CatAction.Loaf)throw new Exception("Loaf menu failed");
            FindMenu("Notice my nearby cursor").PerformClick();if(brain.CursorCuriosity)throw new Exception("Cursor curiosity toggle failed");
            FindMenu("Notice my nearby cursor").PerformClick();
            FindMenu("Energetic play").PerformClick();if(brain.EnergeticPlay)throw new Exception("Energetic play toggle failed");
            FindMenu("Energetic play").PerformClick();
            FindMenu("Scratch Bob's head").PerformClick();if(brain.Action!=CatAction.HeadScratch)throw new Exception("Head scratch menu failed");
            FindMenu("Belly rub").PerformClick();if(brain.Action!=CatAction.BellyRub)throw new Exception("Belly rub menu failed");
            FindMenu("Pet with cursor hover").PerformClick();if(prefs.HoverPettingEnabled)throw new Exception("Hover petting toggle failed");
            FindMenu("Pet with cursor hover").PerformClick();
            Native.RECT bounds; Native.GetWindowRect(Handle, out bounds);
            int style = Native.GetWindowLong(Handle, -20);
            File.WriteAllText(smokeReport, "visible=" + Native.IsWindowVisible(Handle) + "\nlayered=" + ((style & Native.ExLayered) != 0) + "\nnoactivate=" + ((style & Native.ExNoActivate) != 0) + "\ntray=" + tray.Visible + "\nrenders=" + renders + "\nframeChanges=" + frameChanges + "\nrenderErrors=" + renderErrors + "\ncachedFrames=" + cache.Count + "\ngdiStart=" + gdiBefore + "\ngdiEnd=" + Native.GetGuiResources(Process.GetCurrentProcess().Handle, 0) + "\nbounds=" + bounds.Left + "," + bounds.Top + "," + bounds.Right + "," + bounds.Bottom + "\ntransparentHit=PASS\nsolidHit=PASS\nnapWakeMenu=PASS\npauseResumeMenu=PASS\nclickThroughMenu=PASS\nresizeMenu=PASS\nbehaviorMenu=PASS\n");
            File.AppendAllText(smokeReport,"windowDiscovery=PASS\nminimizedWindowFilter=PASS\nsoundResources=PASS\nmuteMenu=PASS\nwindowInteractionMenu=PASS\n");
            File.AppendAllText(smokeReport,"allActionFrames=PASS\ntoyOverlayNoActivation=PASS\ntoyClickThrough=PASS\nyarnLaserSpawnRemoveMenus=PASS\nloafMenu=PASS\ncursorCuriosityMenu=PASS\nenergeticPlayMenu=PASS\n");
            File.AppendAllText(smokeReport,"headScratchMenu=PASS\nbellyRubMenu=PASS\nhoverPettingToggle=PASS\n");
            using (Bitmap sample = new Bitmap(192 * 4, 208, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(sample))
            {
                g.Clear(Color.FromArgb(248, 242, 225));
                for (int i = 0; i < 4; i++) g.DrawImage(atlas, new Rectangle(i * 192, 0, 192, 208), new Rectangle(0, (i == 0 ? 0 : i == 1 ? 1 : i == 2 ? 3 : 4) * 208, 192, 208), GraphicsUnit.Pixel);
                sample.Save(Path.Combine(Path.GetDirectoryName(smokeReport), "desktop-render-preview.png"), ImageFormat.Png);
            }
        }
        private static IntPtr EncodePoint(Point point) { return new IntPtr(unchecked((int)(((uint)(ushort)point.Y << 16) | (ushort)point.X))); }
        private ToolStripMenuItem FindMenu(string text)
        {
            foreach (ToolStripItem item in menu.Items) if (item.Text == text) return (ToolStripMenuItem)item;
            throw new Exception("Missing menu item: " + text);
        }
        private void ReleaseFrames()
        {
            if (memoryDc != IntPtr.Zero && oldObject != IntPtr.Zero) Native.SelectObject(memoryDc, oldObject);
            foreach (Sprite frame in cache.Values) frame.Dispose(); cache.Clear();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true; timer.Stop(); timer.Dispose(); tray.Visible = false; tray.Dispose(); menu.Dispose();
                toy.Dispose();sounds.Dispose(); if (fixture!=null) { fixture.Close();fixture.Dispose(); }
                ReleaseFrames(); if (memoryDc != IntPtr.Zero) Native.DeleteDC(memoryDc);
                atlas.Dispose(); if (trayIcon != null) trayIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal static class Program
    {
        public static Bitmap LoadAtlas()
        {
            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream("Bob.spritesheet.png"))
            using (Bitmap source = new Bitmap(input)) return new Bitmap(source);
        }
        public static void Log(string message)
        {
            try { Directory.CreateDirectory(Preferences.Folder); File.AppendAllText(Path.Combine(Preferences.Folder, "errors.log"), DateTime.Now.ToString("s") + " " + message + Environment.NewLine); } catch { }
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Preview(string folder)
        {
            Directory.CreateDirectory(folder);
            string[] labels={"Loaf","Stretch","Scratch","Groom","Roll","Window grip","Fall","Pounce","Belly rub","Head scratch"};
            using(Bitmap atlas=LoadAtlas())
            using(Bitmap sheet=new Bitmap(192*6,232*10,PixelFormat.Format32bppArgb))
            using(Graphics g=Graphics.FromImage(sheet))
            using(Font font=new Font("Segoe UI",12))
            using(Brush ink=new SolidBrush(Color.FromArgb(91,43,18)))
            {
                g.Clear(Color.FromArgb(248,242,225));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
                for(int row=0;row<10;row++)
                {
                    g.DrawString(labels[row],font,ink,4,row*232);
                    for(int frame=0;frame<6;frame++)using(Bitmap pose=ExtraArt.Make(atlas,row+20,frame))
                    {
                        g.DrawImage(pose,new Rectangle(frame*192,row*232+24,192,208));
                        pose.Save(Path.Combine(folder,"pose-"+row+"-"+frame+".png"),ImageFormat.Png);
                    }
                }
                sheet.Save(Path.Combine(folder,"actions-preview.png"),ImageFormat.Png);
            }
        }
        private static void SelfTest(string output)
        {
            List<string> results = new List<string>();
            Size size = new Size(192,208); Rectangle area = new Rectangle(0,0,1920,1040);
            CatBrain b = new CatBrain { X = 100, Y = 200, Mode = Behavior.Follow };
            b.Tick(.1, 1, new Point(900,500), area,size,0,0,60);
            Assert(b.Moving && b.State == 1 && b.X > 100, "Follow-right failed"); results.Add("PASS follow-right");
            b.Tick(.1,2,new Point(0,100),area,size,0,0,60);
            Assert(b.Moving && b.State == 2,"Follow-left failed"); results.Add("PASS follow-left");
            b.Tick(.1,65,new Point(0,100),area,size,65,65,60);
            Assert(b.State == -1 && !b.Moving,"Automatic sleep failed"); results.Add("PASS automatic sleep");
            b.Tick(.1,66,new Point(900,500),area,size,0,0,60);
            Assert(b.State != -1,"Wake on activity failed"); results.Add("PASS wake on activity");
            b.ForcedSleep=true; b.Tick(.1,67,new Point(900,500),area,size,0,0,60);
            Assert(b.State == -1,"Manual nap failed"); b.Wake(); Assert(!b.ForcedSleep,"Manual wake failed"); results.Add("PASS manual nap/wake");
            b.Paused=true; double x=b.X,y=b.Y; b.Tick(.1,68,new Point(1800,900),area,size,0,0,60);
            Assert(b.X==x && b.Y==y,"Pause moved pet"); b.Paused=false; results.Add("PASS pause");
            b.Mode=Behavior.Stay; b.Tick(.1,69,new Point(1800,900),area,size,0,0,60);
            Assert(b.X==x && b.Y==y,"Stay mode moved pet"); results.Add("PASS stay mode");
            b.Mode=Behavior.Follow; b.X=-9999; b.Y=9999; Rectangle negative=new Rectangle(-1920,-100,1920,1080);
            b.Tick(.1,70,new Point(-500,200),negative,size,0,0,60);
            Assert(b.X>=negative.Left && b.X+size.Width<=negative.Right && b.Y>=negative.Top && b.Y+size.Height<=negative.Bottom,"Monitor boundary clamp failed"); results.Add("PASS negative monitor coordinates and work-area bounds");
            b.X=500;b.Y=200; b.Mode=Behavior.Stay;
            Point center=new Point(596,339);
            b.Tick(.1,71,new Point(center.X,center.Y-300),area,size,0,0,60); Assert(b.State==100,"Look up failed");
            b.Tick(.1,72,new Point(center.X+300,center.Y),area,size,0,0,60); Assert(b.State==104,"Look right failed");
            b.Tick(.1,73,new Point(center.X,center.Y+300),area,size,0,0,60); Assert(b.State==108,"Look down failed");
            b.Tick(.1,74,new Point(center.X-300,center.Y),area,size,0,0,60); Assert(b.State==112,"Look left failed"); results.Add("PASS four gaze cardinal mappings");
            Assert(PetForm.FrameAt(4,0)==0 && PetForm.FrameAt(4,300)==2 && PetForm.FrameAt(4,840)==0,"Jump timing failed"); results.Add("PASS animation timing and loop");
            CatBrain autonomousA = new CatBrain(new Random(42)) { X=500,Y=300,Mode=Behavior.Wander,CursorCuriosity=false };
            CatBrain autonomousB = new CatBrain(new Random(42)) { X=500,Y=300,Mode=Behavior.Wander,CursorCuriosity=false };
            bool walked=false, rested=false, napped=false, played=false, watched=false;
            for (int step=0;step<18000;step++)
            {
                double now=step*.1;
                autonomousA.Tick(.1,now,new Point(10,10),area,size,0,0,60);
                autonomousB.Tick(.1,now,new Point(1900,1000),area,size,0,0,60);
                Assert(autonomousA.X==autonomousB.X && autonomousA.Y==autonomousB.Y && autonomousA.State==autonomousB.State,"Autonomous behavior depends on cursor");
                Assert(autonomousA.X>=area.Left && autonomousA.X+size.Width<=area.Right && autonomousA.Y>=area.Top && autonomousA.Y+size.Height<=area.Bottom,"Autonomous screen bounds failed");
                walked |= autonomousA.Moving; rested |= autonomousA.State==0; napped |= autonomousA.State==-1; played |= autonomousA.State==4; watched |= autonomousA.State>=100;
            }
            Assert(walked&&rested&&napped&&played&&watched,"Autonomous activity variety failed: walked="+walked+", rested="+rested+", napped="+napped+", played="+played+", watched="+watched); results.Add("PASS 30 simulated minutes of autonomous strolls, rests, naps, play and watching"); results.Add("PASS autonomous behavior is independent of cursor position");
            autonomousA.Wake(); autonomousA.Tick(.1,1801,new Point(10,10),area,size,0,0,60);
            Assert(autonomousA.State==0&&!autonomousA.Moving,"Autonomous wake did not enter calm rest"); results.Add("PASS autonomous wake returns to rest");
            WindowLandmark perch=WindowLandmark.Create(new IntPtr(123),new Rectangle(300,400,800,500),area,size);
            Assert(Math.Abs(perch.Position.Y+202-400)<1,"Window top perch alignment failed");
            WindowLandmark corner=WindowLandmark.Create(new IntPtr(124),area,area,size);
            Assert(corner.Position.Y>=area.Top && corner.Position.Y+size.Height<=area.Bottom,"Maximized window corner placement failed");
            CatBrain visitor=new CatBrain(new Random(42)) { X=500,Y=300,Mode=Behavior.Wander,CursorCuriosity=false };
            visitor.Windows.Add(perch);
            double visitTime=0;
            for(int step=0;step<18000;step++)
            {
                visitTime=step*.1;
                visitor.Tick(.1,visitTime,new Point(0,0),area,size,0,0,60);
                if(visitor.Perched) break;
            }
            Assert(visitor.Perched&&visitor.WindowVisits>0,"Autonomous window visit failed");
            Assert(Math.Abs(visitor.Y-perch.Position.Y)<5,"Cat did not reach window edge");
            visitor.Windows[0]=WindowLandmark.Create(new IntPtr(123),new Rectangle(450,500,800,400),area,size);
            visitor.Tick(.1,visitTime+.1,new Point(0,0),area,size,0,0,60);
            Assert(visitor.Moving,"Cat did not adjust to moved window");
            visitor.Windows.Clear(); visitor.Tick(.1,visitTime+.2,new Point(0,0),area,size,0,0,60);
            Assert(!visitor.Perched&&!visitor.Moving,"Cat did not leave missing window perch");
            results.Add("PASS window-edge alignment, autonomous visits, moving windows and removed windows");
            foreach(CatAction action in new[] { CatAction.Loaf,CatAction.Stretch,CatAction.Scratch,CatAction.Groom,CatAction.Roll,CatAction.Zoomies,CatAction.Yarn,CatAction.Laser,CatAction.Hop,CatAction.Fall,CatAction.HeadScratch,CatAction.BellyRub })
            {
                CatBrain actor=new CatBrain(new Random(19)) { X=500,Y=400,Mode=Behavior.Stay,CursorCuriosity=false };
                Assert(actor.StartAction(action,0,area,size),"Action did not start: "+action);
                for(int step=0;step<320;step++)
                {
                    actor.Tick(.1,step*.1,new Point(0,0),area,size,0,0,60);
                    Assert(actor.X>=area.Left && actor.X+size.Width<=area.Right && actor.Y>=area.Top && actor.Y+size.Height<=area.Bottom,"Action escaped screen: "+action);
                    if(actor.ToyVisible) Assert(actor.ToyX>=area.Left && actor.ToyX<=area.Right && actor.ToyY>=area.Top && actor.ToyY<=area.Bottom,"Toy escaped screen");
                }
                Assert(actor.Action==CatAction.None && !actor.ToyVisible,"Action or toy did not end: "+action);
                if(action==CatAction.Yarn) Assert(actor.YarnBats>0,"Cat never batted yarn");
            }
            CatBrain grip=new CatBrain(new Random(9)) { X=500,Y=350,Mode=Behavior.Stay };
            grip.Windows.Add(perch);Assert(grip.StartAction(CatAction.Cling,0,area,size),"Cling failed to start");
            bool hung=false;
            for(int step=0;step<200;step++) { grip.Tick(.1,step*.1,new Point(0,0),area,size,0,0,60);if(grip.State==25){hung=true;break;} }
            Assert(hung,"Cat never grabbed window");grip.Windows.Clear();grip.Tick(.1,20,new Point(0,0),area,size,0,0,60);
            Assert(grip.Action==CatAction.Fall,"Missing window did not trigger fall");
            for(int step=0;step<40;step++)grip.Tick(.1,20+step*.1,new Point(0,0),area,size,0,0,60);
            Assert(grip.Y==area.Bottom-size.Height && grip.Action!=CatAction.Fall,"Fall never landed");
            CatBrain curious=new CatBrain(new Random(5)) { X=500,Y=300,Mode=Behavior.Wander };
            curious.Tick(.1,1,new Point(770,439),area,size,0,0,60);Assert(curious.Curious && curious.Moving,"Nearby cursor was ignored");
            curious.Tick(.1,5,new Point(770,439),area,size,0,0,60);Assert(!curious.Curious,"Cursor curiosity did not expire");
            curious.Tick(.1,6,new Point(770,439),area,size,0,0,60);Assert(!curious.Curious,"Curiosity cooldown failed");
            CatBrain toysOff=new CatBrain { ToysEnabled=false };Assert(!toysOff.StartAction(CatAction.Yarn,0,area,size),"Disabled toy spawned");
            toysOff.ToysEnabled=true;toysOff.StartAction(CatAction.Laser,0,area,size);toysOff.Wake();Assert(!toysOff.ToyVisible,"Wake left toy behind");
            results.Add("PASS new actions finish, remain on-screen, yarn bats, toy cleanup, cling/window loss/gravity landing and bounded cursor curiosity");
            Rectangle petBounds=new Rectangle(0,0,192,208);
            HoverPetting headGesture=new HoverPetting();
            Assert(headGesture.Update(new Point(96,80),petBounds,1,true,0)==PetRegion.None,"Hover fired without strokes");
            headGesture.Update(new Point(96,95),petBounds,1,true,.2);headGesture.Update(new Point(96,80),petBounds,1,true,.4);
            Assert(headGesture.Update(new Point(96,95),petBounds,1,true,.6)==PetRegion.Head,"Head strokes were ignored");
            Assert(headGesture.Update(new Point(96,80),petBounds,1,true,.8)==PetRegion.None,"Hover cooldown failed");
            HoverPetting bellyGesture=new HoverPetting();bellyGesture.Update(new Point(96,150),petBounds,1,true,0);bellyGesture.Update(new Point(96,165),petBounds,1,true,.2);bellyGesture.Update(new Point(96,150),petBounds,1,true,.4);
            Assert(bellyGesture.Update(new Point(96,165),petBounds,1,true,.6)==PetRegion.Belly,"Belly strokes were ignored");
            HoverPetting outside=new HoverPetting();for(int n=0;n<10;n++)Assert(outside.Update(new Point(96,n%2==0?80:95),petBounds,1,false,n*.1)==PetRegion.None,"Transparent space triggered petting");
            HoverPetting sideways=new HoverPetting();for(int n=0;n<10;n++)Assert(sideways.Update(new Point(n%2==0?90:110,90),petBounds,1,true,n*.1)==PetRegion.None,"Horizontal hover triggered vertical strokes");
            results.Add("PASS head/belly hover strokes, cooldown, transparent-space rejection and horizontal-motion rejection");
            using(SoundBank soundCheck=new SoundBank())
            {
                soundCheck.Enabled=false;
                Assert(!soundCheck.Play("meow",0,true),"Muted audio played");
                HashSet<string> heard = new HashSet<string>();
                string previous = null;
                for (int i=0; i<100; i++)
                {
                    string chosen = soundCheck.ChooseMeow();
                    Assert(chosen != previous,"Meow repeated consecutively");
                    heard.Add(chosen); previous = chosen;
                }
                Assert(heard.Count == 4,"Missing meow variation");
                Assert(Assembly.GetExecutingAssembly().GetManifestResourceStream("Bob.purr.wav") == null,"Purr still embedded");
                results.Add("PASS four embedded meows, no consecutive repeats, chirp loading, mute gate and no purr resource");
            }
            using(Bitmap atlas=LoadAtlas())
            {
                for(int row=20;row<=29;row++)for(int frame=0;frame<6;frame++)using(Bitmap pose=ExtraArt.Make(atlas,row,frame))
                {
                    bool occupied=false;for(int yy=0;yy<pose.Height;yy++)for(int xx=0;xx<pose.Width;xx++)occupied|=pose.GetPixel(xx,yy).A>0;
                    Assert(occupied && pose.Width==192 && pose.Height==208,"Missing new pose or incorrect resolution");
                }
                using(Stream actionInput=Assembly.GetExecutingAssembly().GetManifestResourceStream("Bob.action-sprites.png"))
                using(Bitmap generated=new Bitmap(actionInput))
                using(Bitmap pose=ExtraArt.Make(atlas,29,0))
                {
                    Assert(generated.Width==1152 && generated.Height==2080,"Generated action atlas geometry failed");
                    for(int yy=0;yy<208;yy++)for(int xx=0;xx<192;xx++)
                        Assert(pose.GetPixel(xx,yy).ToArgb()==generated.GetPixel(xx,9*208+yy).ToArgb(),"Generated artwork changed during frame extraction");
                }
                results.Add("PASS exact embedded image-generated action pixels at native resolution");
                foreach(ToyKind kind in new[] { ToyKind.Yarn,ToyKind.Laser })using(Bitmap toyImage=ExtraArt.Toy(kind,0))Assert(toyImage.Width==48 && toyImage.Height==48,"Toy resolution changed");
                results.Add("PASS all 60 action frames at native resolution, exact generated frame pixels and native-size toys");
                Assert(atlas.Width==1536&&atlas.Height==2288,"Asset geometry failed");
                int[] counts={6,8,8,4,5,8,6,6,6,8,8};
                for(int r=0;r<11;r++) for(int c=0;c<8;c++)
                {
                    bool occupied=false;
                    for(int iy=0;iy<208&&!occupied;iy++) for(int ix=0;ix<192;ix++) if(atlas.GetPixel(c*192+ix,r*208+iy).A>0) { occupied=true;break; }
                    Assert(occupied==(c<counts[r]),"Unexpected asset occupancy row "+r+" col "+c);
                }
                Assert(atlas.GetPixel(0,0).A==0,"Background not transparent"); results.Add("PASS all 73 embedded frames and unused-cell transparency");
            }
            File.WriteAllLines(output,results.ToArray());
        }
        [STAThread] private static void Main(string[] args)
        {
            try
            {
                if(args.Length==2&&args[0]=="--self-test") { SelfTest(Path.GetFullPath(args[1]));return; }
                if(args.Length==2&&args[0]=="--preview") { Preview(Path.GetFullPath(args[1]));return; }
                string smoke=args.Length==2&&args[0]=="--smoke-test"?Path.GetFullPath(args[1]):null;
                bool owner;
                using(Mutex mutex=new Mutex(true,smoke==null?"Local\\BobDesktopPet-v1":"Local\\BobDesktopPet-smoke",out owner))
                {
                    if(!owner) return;
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { Log(e.Exception.ToString()); Application.Exit(); };
                    using(PetForm pet=new PetForm(smoke)) Application.Run(pet);
                }
            }
            catch(Exception error)
            {
                Log(error.ToString()); Environment.ExitCode=1;
                if(args.Length==2) { try {File.WriteAllText(Path.GetFullPath(args[1]),"FAIL "+error);}catch{} }
                else MessageBox.Show("Bob couldn't start. Details are saved in "+Path.Combine(Preferences.Folder,"errors.log"),"Bob The Cat",MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
        }
    }
}
