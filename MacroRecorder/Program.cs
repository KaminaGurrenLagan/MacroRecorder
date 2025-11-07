using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Text.Json;

namespace MacroRecorderPro
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MacroForm());
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }

    public class MacroForm : Form
    {
        private Button btnRecord, btnPlay, btnClear, btnSave, btnLoad;
        private Label lblStatus, lblCount, lblSpeed;
        private CheckBox chkRecordMouse, chkHighPrecision;
        private TrackBar trackSpeed;
        private NumericUpDown numLoops;

        private readonly List<MacroAction> actions = new List<MacroAction>();
        private readonly object actionsLock = new object();
        private long recordingStartTicks = 0;
        private Thread playThread;

        private volatile bool recording, playing, stopFlag;

        private readonly LowLevelKeyboardProc kbProc;
        private readonly LowLevelMouseProc mouseProc;
        private IntPtr kbHook, mouseHook;

        private int lastX = -1, lastY = -1;
        private long lastMoveTimeTicks = 0;
        private const int MOVE_THRESHOLD_NORMAL = 5;
        private const int MOVE_THRESHOLD_PRECISE = 2;
        private const long MOVE_INTERVAL_TICKS_NORMAL = 250000;
        private const long MOVE_INTERVAL_TICKS_PRECISE = 100000;

        private int totalActionsRecorded = 0;
        private int totalActionsPlayed = 0;

        private readonly Stopwatch precisionTimer = new Stopwatch();

        public MacroForm()
        {
            InitUI();
            kbProc = KbCallback;
            mouseProc = MouseCallback;
            SetHooks();
            precisionTimer.Start();
        }

        private void InitUI()
        {
            Text = "Ultra-Precision Macro Recorder";
            ClientSize = new Size(520, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            TopMost = true;
            BackColor = Color.FromArgb(30, 30, 30);

            var lblTitle = new Label
            {
                Text = "ULTRA-PRECISION MACRO RECORDER",
                Bounds = new Rectangle(20, 15, 480, 30),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 200, 255),
                TextAlign = ContentAlignment.MiddleCenter
            };

            btnRecord = new Button
            {
                Text = "● RECORD (F9)",
                Bounds = new Rectangle(20, 60, 230, 50),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 200, 83),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnRecord.FlatAppearance.BorderSize = 0;
            btnRecord.Click += (s, e) => ToggleRecord();

            btnPlay = new Button
            {
                Text = "▶ PLAY",
                Bounds = new Rectangle(270, 60, 230, 50),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            btnPlay.FlatAppearance.BorderSize = 0;
            btnPlay.Click += (s, e) => TogglePlay();

            chkRecordMouse = new CheckBox
            {
                Text = "Record Mouse Moves",
                Bounds = new Rectangle(20, 125, 200, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White,
                Checked = true
            };

            chkHighPrecision = new CheckBox
            {
                Text = "High Precision Mode",
                Bounds = new Rectangle(270, 125, 200, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(255, 200, 0),
                Checked = true
            };

            var lblLoops = new Label
            {
                Text = "Loops:",
                Bounds = new Rectangle(20, 160, 60, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };

            numLoops = new NumericUpDown
            {
                Bounds = new Rectangle(85, 158, 80, 25),
                Minimum = 1,
                Maximum = 9999,
                Value = 1,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };

            lblSpeed = new Label
            {
                Text = "Speed: 100%",
                Bounds = new Rectangle(180, 160, 100, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackSpeed = new TrackBar
            {
                Bounds = new Rectangle(280, 155, 220, 35),
                Minimum = 10,
                Maximum = 500,
                Value = 100,
                TickFrequency = 50,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            trackSpeed.ValueChanged += (s, e) => lblSpeed.Text = $"Speed: {trackSpeed.Value}%";

            btnSave = new Button
            {
                Text = "💾 SAVE",
                Bounds = new Rectangle(20, 200, 110, 35),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(80, 80, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) => SaveMacro();

            btnLoad = new Button
            {
                Text = "📂 LOAD",
                Bounds = new Rectangle(140, 200, 110, 35),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(80, 80, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnLoad.FlatAppearance.BorderSize = 0;
            btnLoad.Click += (s, e) => LoadMacro();

            btnClear = new Button
            {
                Text = "🗑 CLEAR",
                Bounds = new Rectangle(270, 200, 230, 35),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(232, 17, 35),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (s, e) => ClearActions();

            lblStatus = new Label
            {
                Text = "Ready - High Precision Mode Active",
                Bounds = new Rectangle(20, 250, 480, 30),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 200, 83),
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblCount = new Label
            {
                Text = "Actions: 0 | Duration: 0.0s",
                Bounds = new Rectangle(20, 290, 480, 60),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.TopCenter
            };

            Controls.AddRange(new Control[] {
                lblTitle, btnRecord, btnPlay, chkRecordMouse, chkHighPrecision,
                lblLoops, numLoops, lblSpeed, trackSpeed,
                btnSave, btnLoad, btnClear, lblStatus, lblCount
            });
        }

        private void SetHooks()
        {
            using (var p = Process.GetCurrentProcess())
            using (var m = p.MainModule)
            {
                var h = GetModuleHandle(m.ModuleName);
                kbHook = SetWindowsHookEx(13, kbProc, h, 0);
                mouseHook = SetWindowsHookEx(14, mouseProc, h, 0);
            }
        }

        private IntPtr KbCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0) return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);

            int key = Marshal.ReadInt32(lParam);
            bool down = wParam == (IntPtr)0x0100;

            if (playing && down && key == 0x09 && (GetAsyncKeyState(0x10) & 0x8000) != 0)
            {
                stopFlag = true;
                return (IntPtr)1;
            }

            if (key == 0x78 && down && !playing)
            {
                BeginInvoke(new Action(ToggleRecord));
                return (IntPtr)1;
            }

            if (recording && !playing)
            {
                long relativeTime = precisionTimer.ElapsedTicks - recordingStartTicks;

                lock (actionsLock)
                {
                    actions.Add(new MacroAction
                    {
                        Type = ActionType.Keyboard,
                        Key = key,
                        Down = down,
                        TimeTicks = relativeTime
                    });
                    totalActionsRecorded++;
                }

                UpdateCount();
            }

            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        private IntPtr MouseCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0 || !recording || playing)
                return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);

            var m = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int msg = (int)wParam;
            long nowTicks = precisionTimer.ElapsedTicks;
            long relativeTime = nowTicks - recordingStartTicks;

            int moveThreshold = chkHighPrecision.Checked ? MOVE_THRESHOLD_PRECISE : MOVE_THRESHOLD_NORMAL;
            long moveIntervalTicks = chkHighPrecision.Checked ? MOVE_INTERVAL_TICKS_PRECISE : MOVE_INTERVAL_TICKS_NORMAL;

            if (msg == 0x200 && chkRecordMouse.Checked)
            {
                // ИСПРАВЛЕНИЕ: Фильтрация дублирующихся координат
                if (lastX == m.pt.x && lastY == m.pt.y)
                    return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);

                if (lastX != -1)
                {
                    int dx = Math.Abs(m.pt.x - lastX);
                    int dy = Math.Abs(m.pt.y - lastY);
                    long dt = nowTicks - lastMoveTimeTicks;

                    if ((dx < moveThreshold && dy < moveThreshold) || dt < moveIntervalTicks)
                        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
                }

                lastX = m.pt.x;
                lastY = m.pt.y;
                lastMoveTimeTicks = nowTicks;
            }

            var act = new MacroAction
            {
                Type = ActionType.Mouse,
                X = m.pt.x,
                Y = m.pt.y,
                TimeTicks = relativeTime
            };

            switch (msg)
            {
                case 0x201: act.Button = MouseButton.Left; act.Down = true; break;
                case 0x202: act.Button = MouseButton.Left; act.Down = false; break;
                case 0x204: act.Button = MouseButton.Right; act.Down = true; break;
                case 0x205: act.Button = MouseButton.Right; act.Down = false; break;
                case 0x207: act.Button = MouseButton.Middle; act.Down = true; break;
                case 0x208: act.Button = MouseButton.Middle; act.Down = false; break;
                case 0x20A:
                    act.Button = MouseButton.Wheel;
                    act.WheelDelta = (short)((m.mouseData >> 16) & 0xFFFF);
                    break;
                case 0x200:
                    if (!chkRecordMouse.Checked) return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
                    act.Button = MouseButton.Move;
                    break;
                default: return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
            }

            lock (actionsLock)
            {
                actions.Add(act);
                totalActionsRecorded++;
            }

            UpdateCount();
            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        // ИСПРАВЛЕНИЕ: Безопасный доступ к счётчику
        private void UpdateCount()
        {
            int count;
            lock (actionsLock)
            {
                count = actions.Count;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => lblCount.Text = $"Actions: {count}"));
            }
            else
            {
                lblCount.Text = $"Actions: {count}";
            }
        }

        private void UpdateUI()
        {
            lock (actionsLock)
            {
                double duration = actions.Count > 0 ?
                    (actions[actions.Count - 1].TimeTicks - actions[0].TimeTicks) / (double)TimeSpan.TicksPerSecond : 0;

                lblCount.Text = $"Actions: {actions.Count} | Duration: {duration:F2}s\n" +
                               $"Recorded: {totalActionsRecorded} | Played: {totalActionsPlayed}";
                btnPlay.Enabled = actions.Count > 0;
                btnSave.Enabled = actions.Count > 0;
            }
        }

        private void ClearActions()
        {
            lock (actionsLock)
            {
                actions.Clear();
            }
            totalActionsRecorded = 0;
            totalActionsPlayed = 0;
            UpdateUI();
            lblStatus.Text = "Ready - Actions Cleared";
            lblStatus.ForeColor = Color.Gray;
        }

        private void ToggleRecord()
        {
            if (playing) return;

            if (!recording)
            {
                lock (actionsLock)
                {
                    actions.Clear();
                }
                totalActionsRecorded = 0;
                lastX = lastY = -1;
                lastMoveTimeTicks = 0;

                recordingStartTicks = precisionTimer.ElapsedTicks;
                recording = true;

                btnRecord.Text = "■ STOP (F9)";
                btnRecord.BackColor = Color.FromArgb(232, 17, 35);
                btnPlay.Enabled = false;
                btnClear.Enabled = false;
                btnSave.Enabled = false;
                btnLoad.Enabled = false;
                chkRecordMouse.Enabled = false;
                chkHighPrecision.Enabled = false;
                trackSpeed.Enabled = false;
                numLoops.Enabled = false;

                lblStatus.Text = "● RECORDING - High Precision Active";
                lblStatus.ForeColor = Color.FromArgb(232, 17, 35);
                lblCount.Text = "Actions: 0";
            }
            else
            {
                recording = false;

                btnRecord.Text = "● RECORD (F9)";
                btnRecord.BackColor = Color.FromArgb(0, 200, 83);

                lock (actionsLock)
                {
                    btnPlay.Enabled = actions.Count > 0;
                    btnSave.Enabled = actions.Count > 0;
                }

                btnClear.Enabled = true;
                btnLoad.Enabled = true;
                chkRecordMouse.Enabled = true;
                chkHighPrecision.Enabled = true;
                trackSpeed.Enabled = true;
                numLoops.Enabled = true;

                lblStatus.Text = "✓ RECORDING COMPLETE";
                lblStatus.ForeColor = Color.FromArgb(0, 200, 83);
                UpdateUI();
            }
        }

        private void TogglePlay()
        {
            lock (actionsLock)
            {
                if (actions.Count == 0 || recording) return;
            }

            if (!playing)
            {
                playing = true;
                stopFlag = false;
                totalActionsPlayed = 0;

                btnPlay.Text = "■ STOP (Shift+Tab)";
                btnPlay.BackColor = Color.FromArgb(255, 185, 0);
                btnRecord.Enabled = false;
                btnClear.Enabled = false;
                btnSave.Enabled = false;
                btnLoad.Enabled = false;
                chkRecordMouse.Enabled = false;
                chkHighPrecision.Enabled = false;
                trackSpeed.Enabled = false;
                numLoops.Enabled = false;

                lblStatus.Text = $"▶ PLAYING at {trackSpeed.Value}% speed...";
                lblStatus.ForeColor = Color.FromArgb(255, 185, 0);

                playThread = new Thread(PlayLoop)
                {
                    IsBackground = true,
                    Priority = ThreadPriority.Highest
                };
                playThread.Start();
            }
            else
            {
                stopFlag = true;
            }
        }

        // ИСПРАВЛЕНИЕ: Правильный сброс таймера между циклами
        private void PlayLoop()
        {
            int loops = (int)numLoops.Value;
            double speedMultiplier = trackSpeed.Value / 100.0;

            List<MacroAction> playbackActions;
            lock (actionsLock)
            {
                playbackActions = new List<MacroAction>(actions);
            }

            if (playbackActions.Count == 0)
            {
                StopPlay();
                return;
            }

            // Нормализуем время относительно первого действия
            long firstActionTicks = playbackActions[0].TimeTicks;

            for (int loop = 0; loop < loops && !stopFlag; loop++)
            {
                // ИСПРАВЛЕНИЕ: Создаём НОВЫЙ таймер на каждой итерации
                Stopwatch playbackTimer = Stopwatch.StartNew();

                for (int i = 0; i < playbackActions.Count; i++)
                {
                    if (stopFlag) break;

                    // Вычисляем относительное время от первого действия
                    long relativeTimeTicks = playbackActions[i].TimeTicks - firstActionTicks;
                    long targetTicks = (long)(relativeTimeTicks / speedMultiplier);

                    // Ждём нужное время
                    PreciseWait(playbackTimer, targetTicks);

                    // Выполняем действие
                    Exec(playbackActions[i]);

                    Interlocked.Increment(ref totalActionsPlayed);
                }

                if (stopFlag) break;

                // ИСПРАВЛЕНИЕ: Ждём завершения последнего действия перед следующим циклом
                if (loop < loops - 1)
                {
                    // Вычисляем время последнего действия
                    long lastActionRelativeTicks = playbackActions[playbackActions.Count - 1].TimeTicks - firstActionTicks;
                    long lastActionTargetTicks = (long)(lastActionRelativeTicks / speedMultiplier);

                    // Дожидаемся полного завершения цикла
                    PreciseWait(playbackTimer, lastActionTargetTicks);

                    // Небольшая пауза для стабильности (опционально)
                    Thread.Sleep(5);
                }
            }

            StopPlay();
        }

        private void PreciseWait(Stopwatch timer, long targetTicks)
        {
            long remainingTicks = targetTicks - timer.ElapsedTicks;

            if (remainingTicks <= 0) return;

            double remainingMs = remainingTicks / (double)TimeSpan.TicksPerMillisecond;

            // Sleep для больших задержек
            if (remainingMs > 15.0)
            {
                Thread.Sleep((int)(remainingMs - 15.0));
            }
            else if (remainingMs > 2.0)
            {
                Thread.Sleep((int)(remainingMs - 2.0));
            }

            // SpinWait для точной синхронизации
            SpinWait spinner = new SpinWait();
            while (timer.ElapsedTicks < targetTicks)
            {
                spinner.SpinOnce();
            }
        }

        private void Exec(MacroAction a)
        {
            if (a.Type == ActionType.Keyboard)
            {
                INPUT inp = new INPUT { type = 1 };
                inp.U.ki.wVk = (ushort)a.Key;
                inp.U.ki.dwFlags = a.Down ? 0u : 2u;
                inp.U.ki.dwExtraInfo = GetMessageExtraInfo();
                SendInput(1, new[] { inp }, INPUT.Size);
            }
            else if (a.Type == ActionType.Mouse)
            {
                int screenW = GetSystemMetrics(0);
                int screenH = GetSystemMetrics(1);

                if (screenW == 0) screenW = Screen.PrimaryScreen.Bounds.Width;
                if (screenH == 0) screenH = Screen.PrimaryScreen.Bounds.Height;

                int absX = (int)((a.X * 65535.0) / screenW);
                int absY = (int)((a.Y * 65535.0) / screenH);

                INPUT inp = new INPUT { type = 0 };
                inp.U.mi.dx = absX;
                inp.U.mi.dy = absY;
                inp.U.mi.dwFlags = 0x8000 | 0x0001;
                inp.U.mi.dwExtraInfo = GetMessageExtraInfo();

                if (a.Button == MouseButton.Wheel)
                {
                    inp.U.mi.dwFlags |= 0x0800;
                    inp.U.mi.mouseData = (uint)a.WheelDelta;
                }
                else if (a.Button != MouseButton.Move && a.Button != MouseButton.None)
                {
                    uint clickFlag = a.Button switch
                    {
                        MouseButton.Left => a.Down ? 0x0002u : 0x0004u,
                        MouseButton.Right => a.Down ? 0x0008u : 0x0010u,
                        MouseButton.Middle => a.Down ? 0x0020u : 0x0040u,
                        _ => 0u
                    };
                    inp.U.mi.dwFlags |= clickFlag;
                }

                SendInput(1, new[] { inp }, INPUT.Size);
            }
        }

        private void StopPlay()
        {
            playing = false;
            stopFlag = false;

            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    btnPlay.Text = "▶ PLAY";
                    btnPlay.BackColor = Color.FromArgb(0, 122, 204);
                    btnRecord.Enabled = true;
                    btnClear.Enabled = true;
                    btnSave.Enabled = true;
                    btnLoad.Enabled = true;
                    chkRecordMouse.Enabled = true;
                    chkHighPrecision.Enabled = true;
                    trackSpeed.Enabled = true;
                    numLoops.Enabled = true;
                    lblStatus.Text = "⏹ PLAYBACK STOPPED";
                    lblStatus.ForeColor = Color.Gray;
                    UpdateUI();
                }));
            }
        }

        private void SaveMacro()
        {
            lock (actionsLock)
            {
                if (actions.Count == 0) return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Macro Files (*.macro)|*.macro|All Files (*.*)|*.*";
                sfd.DefaultExt = "macro";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        lock (actionsLock)
                        {
                            var json = JsonSerializer.Serialize(actions, new JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText(sfd.FileName, json);
                        }
                        MessageBox.Show("Macro saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving macro: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void LoadMacro()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Macro Files (*.macro)|*.macro|All Files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var json = File.ReadAllText(ofd.FileName);
                        var loadedActions = JsonSerializer.Deserialize<List<MacroAction>>(json);

                        if (loadedActions == null || loadedActions.Count == 0)
                        {
                            MessageBox.Show("Loaded file is empty or invalid!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        lock (actionsLock)
                        {
                            actions.Clear();
                            actions.AddRange(loadedActions);
                            totalActionsRecorded = actions.Count;
                        }

                        UpdateUI();
                        lblStatus.Text = "✓ MACRO LOADED";
                        lblStatus.ForeColor = Color.FromArgb(0, 200, 83);
                        MessageBox.Show($"Loaded {loadedActions.Count} actions successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading macro: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            stopFlag = true;
            playing = false;
            recording = false;

            if (playThread != null && playThread.IsAlive)
                playThread.Join(2000);

            if (kbHook != IntPtr.Zero) UnhookWindowsHookEx(kbHook);
            if (mouseHook != IntPtr.Zero) UnhookWindowsHookEx(mouseHook);

            base.OnFormClosing(e);
        }

        private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int id, Delegate proc, IntPtr mod, uint tid);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] private static extern uint SendInput(uint n, INPUT[] inp, int size);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
        [DllImport("user32.dll")] private static extern IntPtr GetMessageExtraInfo();

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData, flags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION U;
            public static readonly int Size = Marshal.SizeOf(typeof(INPUT));
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public IntPtr dwExtraInfo;
        }
    }

    public enum ActionType { Keyboard = 0, Mouse = 1 }
    public enum MouseButton { None = 0, Move = 0, Left = 1, Right = 2, Middle = 3, Wheel = 4 }

    public struct MacroAction
    {
        public ActionType Type { get; set; }
        public long TimeTicks { get; set; }
        public int Key { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public MouseButton Button { get; set; }
        public bool Down { get; set; }
        public int WheelDelta { get; set; }
    }
}