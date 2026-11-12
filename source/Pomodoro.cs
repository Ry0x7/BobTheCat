using System;
using System.Drawing;
using System.Windows.Forms;

namespace BobDesktop
{
    public enum PomodoroPhase
    {
        Idle,
        Work,
        ShortBreak,
        LongBreak
    }

    public enum PomodoroEvent
    {
        None,
        WorkCompleted,
        BreakCompleted
    }

    public class PomodoroTimer
    {
        public PomodoroPhase Phase = PomodoroPhase.Idle;
        public bool IsPaused = false;
        public double SecondsRemaining = 0;
        public double TotalDurationSeconds = 0;
        public int CurrentCycle = 1;
        public int TotalCompletedWorkSessions = 0;

        // Customizable intervals and preferences
        public int WorkMinutes = 25;
        public int ShortBreakMinutes = 5;
        public int LongBreakMinutes = 15;
        public int CyclesBeforeLongBreak = 4;
        public bool AutoStartBreaks = true;
        public bool AutoStartWork = false;
        public bool QuietCatDuringWork = true;
        public bool SoundAlert = true;
        public bool NotifyBalloon = true;

        public bool IsRunning { get { return Phase != PomodoroPhase.Idle && !IsPaused; } }
        public bool IsInWork { get { return Phase == PomodoroPhase.Work; } }
        public bool IsInBreak { get { return Phase == PomodoroPhase.ShortBreak || Phase == PomodoroPhase.LongBreak; } }

        public void LoadFromPreferences(Preferences prefs)
        {
            if (prefs == null) return;
            WorkMinutes = Math.Max(1, Math.Min(180, prefs.PomodoroWorkMinutes));
            ShortBreakMinutes = Math.Max(1, Math.Min(60, prefs.PomodoroShortBreakMinutes));
            LongBreakMinutes = Math.Max(1, Math.Min(90, prefs.PomodoroLongBreakMinutes));
            CyclesBeforeLongBreak = Math.Max(1, Math.Min(12, prefs.PomodoroCycles));
            AutoStartBreaks = prefs.PomodoroAutoStartBreaks;
            AutoStartWork = prefs.PomodoroAutoStartWork;
            SoundAlert = prefs.PomodoroSoundAlert;
            NotifyBalloon = prefs.PomodoroNotify;
            QuietCatDuringWork = prefs.PomodoroQuietCat;
        }

        public void SaveToPreferences(Preferences prefs)
        {
            if (prefs == null) return;
            prefs.PomodoroWorkMinutes = WorkMinutes;
            prefs.PomodoroShortBreakMinutes = ShortBreakMinutes;
            prefs.PomodoroLongBreakMinutes = LongBreakMinutes;
            prefs.PomodoroCycles = CyclesBeforeLongBreak;
            prefs.PomodoroAutoStartBreaks = AutoStartBreaks;
            prefs.PomodoroAutoStartWork = AutoStartWork;
            prefs.PomodoroSoundAlert = SoundAlert;
            prefs.PomodoroNotify = NotifyBalloon;
            prefs.PomodoroQuietCat = QuietCatDuringWork;
        }

        public void StartWork()
        {
            Phase = PomodoroPhase.Work;
            IsPaused = false;
            TotalDurationSeconds = SecondsRemaining = Math.Max(1, WorkMinutes) * 60.0;
        }

        public void StartShortBreak()
        {
            Phase = PomodoroPhase.ShortBreak;
            IsPaused = false;
            TotalDurationSeconds = SecondsRemaining = Math.Max(1, ShortBreakMinutes) * 60.0;
        }

        public void StartLongBreak()
        {
            Phase = PomodoroPhase.LongBreak;
            IsPaused = false;
            TotalDurationSeconds = SecondsRemaining = Math.Max(1, LongBreakMinutes) * 60.0;
        }

        public void TogglePause()
        {
            if (Phase == PomodoroPhase.Idle)
            {
                StartWork();
                return;
            }
            IsPaused = !IsPaused;
        }

        public void Pause()
        {
            if (Phase != PomodoroPhase.Idle) IsPaused = true;
        }

        public void Resume()
        {
            if (Phase != PomodoroPhase.Idle) IsPaused = false;
        }

        public void Reset()
        {
            Phase = PomodoroPhase.Idle;
            IsPaused = false;
            SecondsRemaining = 0;
            TotalDurationSeconds = 0;
            CurrentCycle = 1;
        }

        public void Skip()
        {
            if (Phase == PomodoroPhase.Work)
            {
                TotalCompletedWorkSessions++;
                if (CurrentCycle >= CyclesBeforeLongBreak)
                {
                    StartLongBreak();
                }
                else
                {
                    StartShortBreak();
                }
            }
            else if (Phase == PomodoroPhase.ShortBreak || Phase == PomodoroPhase.LongBreak)
            {
                if (Phase == PomodoroPhase.LongBreak)
                {
                    CurrentCycle = 1;
                }
                else
                {
                    CurrentCycle++;
                }
                StartWork();
            }
            else
            {
                StartWork();
            }
        }

        public PomodoroEvent Tick(double dt)
        {
            if (Phase == PomodoroPhase.Idle || IsPaused) return PomodoroEvent.None;

            SecondsRemaining -= dt;
            if (SecondsRemaining <= 0)
            {
                SecondsRemaining = 0;
                if (Phase == PomodoroPhase.Work)
                {
                    TotalCompletedWorkSessions++;
                    bool isLong = CurrentCycle >= CyclesBeforeLongBreak;
                    if (isLong)
                    {
                        if (AutoStartBreaks) StartLongBreak();
                        else { Phase = PomodoroPhase.LongBreak; IsPaused = true; TotalDurationSeconds = SecondsRemaining = LongBreakMinutes * 60.0; }
                    }
                    else
                    {
                        if (AutoStartBreaks) StartShortBreak();
                        else { Phase = PomodoroPhase.ShortBreak; IsPaused = true; TotalDurationSeconds = SecondsRemaining = ShortBreakMinutes * 60.0; }
                    }
                    return PomodoroEvent.WorkCompleted;
                }
                else // ShortBreak or LongBreak
                {
                    if (Phase == PomodoroPhase.LongBreak)
                    {
                        CurrentCycle = 1;
                    }
                    else
                    {
                        CurrentCycle++;
                    }

                    if (AutoStartWork)
                    {
                        StartWork();
                    }
                    else
                    {
                        Phase = PomodoroPhase.Idle;
                        IsPaused = false;
                    }
                    return PomodoroEvent.BreakCompleted;
                }
            }
            return PomodoroEvent.None;
        }

        public double Progress
        {
            get
            {
                if (Phase == PomodoroPhase.Idle || TotalDurationSeconds <= 0) return 0.0;
                double elapsed = TotalDurationSeconds - Math.Max(0, SecondsRemaining);
                return Math.Max(0.0, Math.Min(1.0, elapsed / TotalDurationSeconds));
            }
        }

        public void AddMinutes(int minutes)
        {
            if (Phase != PomodoroPhase.Idle)
            {
                double addSec = minutes * 60.0;
                SecondsRemaining += addSec;
                TotalDurationSeconds += addSec;
            }
        }

        public string GetTimeFormatted()
        {
            int total = (int)Math.Ceiling(Math.Max(0, SecondsRemaining));
            int m = total / 60;
            int s = total % 60;
            return string.Format("{0:D2}:{1:D2}", m, s);
        }

        public string GetStatusString()
        {
            if (Phase == PomodoroPhase.Idle) return "Pomodoro: Ready (" + WorkMinutes + "m focus)";
            string prefix = Phase == PomodoroPhase.Work ? "🍅 Focus" : Phase == PomodoroPhase.ShortBreak ? "☕ Short Break" : "☕ Long Break";
            string paused = IsPaused ? " [Paused]" : "";
            string cycle = string.Format(" ({0}/{1})", CurrentCycle, CyclesBeforeLongBreak);
            return string.Format("{0}: {1}{2}{3}", prefix, GetTimeFormatted(), cycle, paused);
        }

        public string GetTrayShortString()
        {
            if (Phase == PomodoroPhase.Idle) return "";
            string tag = Phase == PomodoroPhase.Work ? "🍅" : "☕";
            string paused = IsPaused ? " [P]" : "";
            return string.Format("{0} {1}{2}", tag, GetTimeFormatted(), paused);
        }
    }

    public class CustomPomodoroDialog : Form
    {
        public int WorkMinutes { get; private set; }
        public int ShortBreakMinutes { get; private set; }
        public int LongBreakMinutes { get; private set; }
        public int Cycles { get; private set; }

        private NumericUpDown numWork;
        private NumericUpDown numShort;
        private NumericUpDown numLong;
        private NumericUpDown numCycles;
        private Preferences userPrefs;

        private CheckBox chkVisualTimer;
        private ComboBox cboTheme;
        private ComboBox cboMode;
        private ComboBox cboScale;
        private ComboBox cboOpacity;
        private CheckBox chkAutoHide;
        private CheckBox chkClickThrough;

        public CustomPomodoroDialog(int work, int shortBreak, int longBreak, int cycles, Preferences prefs = null)
        {
            WorkMinutes = work;
            ShortBreakMinutes = shortBreak;
            LongBreakMinutes = longBreak;
            Cycles = cycles;
            userPrefs = prefs;
            InitializeUI();
        }

        private void InitializeUI()
        {
            Text = "Customize Pomodoro & Visual Timer";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(390, 340);
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(248, 246, 242);

            TabControl tabs = new TabControl { Location = new Point(12, 12), Size = new Size(366, 275) };
            TabPage tabDurations = new TabPage("Session Intervals") { BackColor = Color.White };
            TabPage tabVisual = new TabPage("Visual Timer HUD") { BackColor = Color.White };

            // === TAB 1: DURATIONS ===
            int labelX = 20, inputX = 220, y = 18, rowHeight = 38;

            Label lblWork = new Label { Text = "Focus session duration (min):", Location = new Point(labelX, y + 2), AutoSize = true };
            numWork = new NumericUpDown { Location = new Point(inputX, y), Width = 95, Minimum = 1, Maximum = 180, Value = Math.Min(180, Math.Max(1, WorkMinutes)) };

            y += rowHeight;
            Label lblShort = new Label { Text = "Short break duration (min):", Location = new Point(labelX, y + 2), AutoSize = true };
            numShort = new NumericUpDown { Location = new Point(inputX, y), Width = 95, Minimum = 1, Maximum = 60, Value = Math.Min(60, Math.Max(1, ShortBreakMinutes)) };

            y += rowHeight;
            Label lblLong = new Label { Text = "Long break duration (min):", Location = new Point(labelX, y + 2), AutoSize = true };
            numLong = new NumericUpDown { Location = new Point(inputX, y), Width = 95, Minimum = 1, Maximum = 90, Value = Math.Min(90, Math.Max(1, LongBreakMinutes)) };

            y += rowHeight;
            Label lblCycles = new Label { Text = "Cycles before long break:", Location = new Point(labelX, y + 2), AutoSize = true };
            numCycles = new NumericUpDown { Location = new Point(inputX, y), Width = 95, Minimum = 1, Maximum = 12, Value = Math.Min(12, Math.Max(1, Cycles)) };

            tabDurations.Controls.AddRange(new Control[] { lblWork, numWork, lblShort, numShort, lblLong, numLong, lblCycles, numCycles });

            // === TAB 2: VISUAL TIMER HUD ===
            int vy = 14, vrow = 32;

            chkVisualTimer = new CheckBox
            {
                Text = "Show floating visual timer HUD",
                Location = new Point(16, vy),
                AutoSize = true,
                Checked = userPrefs != null && userPrefs.PomodoroVisualTimerEnabled
            };

            vy += vrow;
            Label lblTheme = new Label { Text = "Theme:", Location = new Point(16, vy + 3), AutoSize = true };
            cboTheme = new ComboBox { Location = new Point(170, vy), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            cboTheme.Items.AddRange(new object[] { "Cozy Bob (Warm Amber)", "Modern Glass (Obsidian)", "Minimal Pill (Compact)", "Light Ceramic (Ivory)" });
            string curTheme = userPrefs != null ? userPrefs.PomodoroTimerTheme : "CozyBob";
            cboTheme.SelectedIndex = curTheme == "ModernGlass" ? 1 : curTheme == "MinimalPill" ? 2 : curTheme == "LightCeramic" ? 3 : 0;

            vy += vrow;
            Label lblMode = new Label { Text = "Placement mode:", Location = new Point(16, vy + 3), AutoSize = true };
            cboMode = new ComboBox { Location = new Point(170, vy), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            cboMode.Items.AddRange(new object[] { "Follow Bob (Dynamic)", "Free-Floating Desktop HUD", "Compact Bar Under Bob" });
            string curMode = userPrefs != null ? userPrefs.PomodoroTimerMode : "Follow";
            cboMode.SelectedIndex = curMode == "Detached" ? 1 : curMode == "CompactBar" ? 2 : 0;

            vy += vrow;
            Label lblScale = new Label { Text = "HUD Size:", Location = new Point(16, vy + 3), AutoSize = true };
            cboScale = new ComboBox { Location = new Point(170, vy), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            cboScale.Items.AddRange(new object[] { "Compact (85%)", "Normal (100%)", "Large (125%)" });
            double curScale = userPrefs != null ? userPrefs.PomodoroTimerScale : 1.0;
            cboScale.SelectedIndex = curScale < 0.9 ? 0 : curScale > 1.1 ? 2 : 1;

            vy += vrow;
            Label lblOpacity = new Label { Text = "HUD Opacity:", Location = new Point(16, vy + 3), AutoSize = true };
            cboOpacity = new ComboBox { Location = new Point(170, vy), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            cboOpacity.Items.AddRange(new object[] { "100%", "90% (Recommended)", "75%", "50% (Subtle)" });
            byte curOp = userPrefs != null ? userPrefs.PomodoroTimerOpacity : (byte)230;
            cboOpacity.SelectedIndex = curOp >= 250 ? 0 : curOp >= 215 ? 1 : curOp >= 160 ? 2 : 3;

            vy += vrow + 4;
            chkAutoHide = new CheckBox
            {
                Text = "Auto-hide HUD when idle (show only during timer)",
                Location = new Point(16, vy),
                AutoSize = true,
                Checked = userPrefs != null && userPrefs.PomodoroTimerAutoHideWhenIdle
            };

            vy += 24;
            chkClickThrough = new CheckBox
            {
                Text = "Let clicks pass through HUD (click-through)",
                Location = new Point(16, vy),
                AutoSize = true,
                Checked = userPrefs != null && userPrefs.PomodoroTimerClickThrough
            };

            tabVisual.Controls.AddRange(new Control[] {
                chkVisualTimer, lblTheme, cboTheme, lblMode, cboMode,
                lblScale, cboScale, lblOpacity, cboOpacity, chkAutoHide, chkClickThrough
            });

            tabs.TabPages.Add(tabDurations);
            tabs.TabPages.Add(tabVisual);

            int btnY = 298;
            Button btnOk = new Button { Text = "Save", Location = new Point(208, btnY), Width = 80, Height = 30, DialogResult = DialogResult.OK };
            Button btnCancel = new Button { Text = "Cancel", Location = new Point(298, btnY), Width = 80, Height = 30, DialogResult = DialogResult.Cancel };

            btnOk.Click += delegate
            {
                WorkMinutes = (int)numWork.Value;
                ShortBreakMinutes = (int)numShort.Value;
                LongBreakMinutes = (int)numLong.Value;
                Cycles = (int)numCycles.Value;

                if (userPrefs != null)
                {
                    userPrefs.PomodoroVisualTimerEnabled = chkVisualTimer.Checked;
                    userPrefs.PomodoroTimerTheme = cboTheme.SelectedIndex == 1 ? "ModernGlass" :
                                                   cboTheme.SelectedIndex == 2 ? "MinimalPill" :
                                                   cboTheme.SelectedIndex == 3 ? "LightCeramic" : "CozyBob";
                    userPrefs.PomodoroTimerMode = cboMode.SelectedIndex == 1 ? "Detached" :
                                                  cboMode.SelectedIndex == 2 ? "CompactBar" : "Follow";
                    userPrefs.PomodoroTimerScale = cboScale.SelectedIndex == 0 ? 0.85 :
                                                   cboScale.SelectedIndex == 2 ? 1.25 : 1.0;
                    userPrefs.PomodoroTimerOpacity = cboOpacity.SelectedIndex == 0 ? (byte)255 :
                                                     cboOpacity.SelectedIndex == 1 ? (byte)230 :
                                                     cboOpacity.SelectedIndex == 2 ? (byte)190 : (byte)128;
                    userPrefs.PomodoroTimerAutoHideWhenIdle = chkAutoHide.Checked;
                    userPrefs.PomodoroTimerClickThrough = chkClickThrough.Checked;
                }
                Close();
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[] { tabs, btnOk, btnCancel });
        }
    }
}
