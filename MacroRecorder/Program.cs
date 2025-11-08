using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Text.Json;
using System.Linq;

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
        private Panel headerPanel, controlPanel, statusPanel;

        private readonly List<MacroAction> actions = new List<MacroAction>();
        private readonly object actionsLock = new object();
        private long recordingStartTicks = 0;
        private Thread playThread;

        private volatile bool recording, playing, stopFlag;
        private volatile bool ignoreNextClick = false;

        private readonly LowLevelKeyboardProc kbProc;
        private readonly LowLevelMouseProc mouseProc;
        private IntPtr kbHook, mouseHook;

        private int lastX = -1, lastY = -1;
        private long lastMoveTimeTicks = 0;
        private const int MOVE_THRESHOLD_NORMAL = 8;
        private const int MOVE_THRESHOLD_PRECISE = 3;
        private const long MOVE_INTERVAL_TICKS_NORMAL = 500000; // ~50ms
        private const long MOVE_INTERVAL_TICKS_PRECISE = 200000; // ~20ms

        private int totalActionsRecorded = 0;
        private int totalActionsPlayed = 0;

        private readonly Stopwatch precisionTimer = new Stopwatch();
        private System.Windows.Forms.Timer updateTimer;

        // Для предотвращения GC делегатов
        private GCHandle kbProcHandle;
        private GCHandle mouseProcHandle;

        public MacroForm()
        {
            InitUI();
            kbProc = KbCallback;
            mouseProc = MouseCallback;

            // Защита от GC
            kbProcHandle = GCHandle.Alloc(kbProc);
            mouseProcHandle = GCHandle.Alloc(mouseProc);

            SetHooks();
            precisionTimer.Start();

            // Таймер для обновления UI
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 100;
            updateTimer.Tick += (s, e) => SafeUpdateUI();
            updateTimer.Start();
        }

        private void InitUI()
        {
            Text = "Macro Recorder Pro";
            ClientSize = new Size(450, 380);
            MinimumSize = new Size(450, 380);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            TopMost = true;
            BackColor = Color.FromArgb(24, 24, 27);

            // Header Panel
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(39, 39, 42)
            };

            var lblTitle = new Label
            {
                Text = "MACRO RECORDER PRO",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(168, 85, 247),
                TextAlign = ContentAlignment.MiddleCenter
            };
            headerPanel.Controls.Add(lblTitle);

            // Control Panel
            controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 24, 27),
                Padding = new Padding(15, 10, 15, 10)
            };

            // Record Button
            btnRecord = CreateButton("● RECORD (F9)", 0, 10, Color.FromArgb(34, 197, 94));
            btnRecord.Click += (s, e) => ToggleRecord();

            // Play Button
            btnPlay = CreateButton("▶ PLAY", 0, 70, Color.FromArgb(59, 130, 246));
            btnPlay.Enabled = false;
            btnPlay.Click += (s, e) => TogglePlay();

            // Settings Group
            var settingsY = 130;

            chkRecordMouse = new CheckBox
            {
                Text = "Record Mouse Moves",
                Location = new Point(10, settingsY),
                Size = new Size(180, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White,
                Checked = true,
                FlatStyle = FlatStyle.Flat
            };

            chkHighPrecision = new CheckBox
            {
                Text = "High Precision",
                Location = new Point(220, settingsY),
                Size = new Size(150, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(251, 191, 36),
                Checked = false,
                FlatStyle = FlatStyle.Flat
            };

            // Loop Control
            var lblLoops = new Label
            {
                Text = "Loops:",
                Location = new Point(10, settingsY + 35),
                Size = new Size(50, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };

            numLoops = new NumericUpDown
            {
                Location = new Point(65, settingsY + 33),
                Size = new Size(70, 25),
                Minimum = 1,
                Maximum = 9999,
                Value = 1,
                BackColor = Color.FromArgb(39, 39, 42),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Speed Control
            lblSpeed = new Label
            {
                Text = "Speed: 100%",
                Location = new Point(150, settingsY + 35),
                Size = new Size(90, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackSpeed = new TrackBar
            {
                Location = new Point(245, settingsY + 30),
                Size = new Size(180, 35),
                Minimum = 10,
                Maximum = 500,
                Value = 100,
                TickFrequency = 50,
                BackColor = Color.FromArgb(24, 24, 27)
            };
            trackSpeed.ValueChanged += (s, e) => lblSpeed.Text = $"Speed: {trackSpeed.Value}%";

            // Action Buttons
            var btnY = settingsY + 75;

            btnSave = CreateSmallButton("💾 Save", 10, btnY, Color.FromArgb(71, 85, 105));
            btnSave.Click += (s, e) => SaveMacro();

            btnLoad = CreateSmallButton("📂 Load", 145, btnY, Color.FromArgb(71, 85, 105));
            btnLoad.Click += (s, e) => LoadMacro();

            btnClear = CreateSmallButton("🗑 Clear", 280, btnY, Color.FromArgb(220, 38, 38));
            btnClear.Click += (s, e) => ClearActions();

            // Status Panel
            statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = Color.FromArgb(39, 39, 42)
            };

            lblStatus = new Label
            {
                Text = "Габэн гытыв рвать пукан",
                Location = new Point(10, 10),
                Size = new Size(410, 25),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(34, 197, 94),
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblCount = new Label
            {
                Text = "Actions: 0 | Duration: 0.0s",
                Location = new Point(10, 40),
                Size = new Size(410, 30),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(156, 163, 175),
                TextAlign = ContentAlignment.TopCenter
            };

            statusPanel.Controls.AddRange(new Control[] { lblStatus, lblCount });

            controlPanel.Controls.AddRange(new Control[] {
                btnRecord, btnPlay, chkRecordMouse, chkHighPrecision,
                lblLoops, numLoops, lblSpeed, trackSpeed,
                btnSave, btnLoad, btnClear
            });

            Controls.AddRange(new Control[] { headerPanel, controlPanel, statusPanel });
        }

        private Button CreateButton(string text, int x, int y, Color color)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x + 10, y),
                Size = new Size(410, 45),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.1f);
            return btn;
        }

        private Button CreateSmallButton(string text, int x, int y, Color color)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(125, 32),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.1f);
            return btn;
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

            // Shift+Tab для остановки воспроизведения
            if (playing && down && key == 0x09 && (GetAsyncKeyState(0x10) & 0x8000) != 0)
            {
                stopFlag = true;
                return (IntPtr)1;
            }

            // F9 для записи
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
            }

            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        private IntPtr MouseCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0) return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);

            var m = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int msg = (int)wParam;

            // ИСПРАВЛЕНИЕ: Игнорируем первый клик при старте воспроизведения
            if (ignoreNextClick && (msg == 0x201 || msg == 0x204 || msg == 0x207))
            {
                ignoreNextClick = false;
                return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
            }

            if (!recording || playing)
                return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);

            long nowTicks = precisionTimer.ElapsedTicks;
            long relativeTime = nowTicks - recordingStartTicks;

            int moveThreshold = chkHighPrecision.Checked ? MOVE_THRESHOLD_PRECISE : MOVE_THRESHOLD_NORMAL;
            long moveIntervalTicks = chkHighPrecision.Checked ? MOVE_INTERVAL_TICKS_PRECISE : MOVE_INTERVAL_TICKS_NORMAL;

            // Обработка движения мыши с улучшенной фильтрацией
            if (msg == 0x200 && chkRecordMouse.Checked)
            {
                // Фильтрация дублирующихся координат
                if (lastX == m.pt.x && lastY == m.pt.y)
                    return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);

                if (lastX != -1 && lastY != -1)
                {
                    int dx = Math.Abs(m.pt.x - lastX);
                    int dy = Math.Abs(m.pt.y - lastY);
                    long dt = nowTicks - lastMoveTimeTicks;

                    // Фильтрация мелких движений и частых событий
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

            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        private void SafeUpdateUI()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(SafeUpdateUI));
                return;
            }

            lock (actionsLock)
            {
                if (actions.Count > 0)
                {
                    double duration = (actions[actions.Count - 1].TimeTicks - actions[0].TimeTicks) / (double)TimeSpan.TicksPerSecond;
                    lblCount.Text = $"Actions: {actions.Count} | Duration: {duration:F2}s\n" +
                                   $"Recorded: {totalActionsRecorded} | Played: {totalActionsPlayed}";
                }
                else
                {
                    lblCount.Text = "Actions: 0 | Duration: 0.0s";
                }

                btnPlay.Enabled = actions.Count > 0 && !recording;
                btnSave.Enabled = actions.Count > 0 && !recording;
            }
        }

        private void ClearActions()
        {
            if (recording || playing)
            {
                MessageBox.Show("Cannot clear while recording or playing!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            lock (actionsLock)
            {
                actions.Clear();
            }
            totalActionsRecorded = 0;
            totalActionsPlayed = 0;
            lastX = lastY = -1;
            lastMoveTimeTicks = 0;

            SafeUpdateUI();
            lblStatus.Text = "Ready";
            lblStatus.ForeColor = Color.FromArgb(156, 163, 175);
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
                btnRecord.BackColor = Color.FromArgb(220, 38, 38);
                btnPlay.Enabled = false;
                btnClear.Enabled = false;
                btnSave.Enabled = false;
                btnLoad.Enabled = false;
                chkRecordMouse.Enabled = false;
                chkHighPrecision.Enabled = false;
                trackSpeed.Enabled = false;
                numLoops.Enabled = false;

                lblStatus.Text = "● RECORDING...";
                lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
            }
            else
            {
                recording = false;

                btnRecord.Text = "● RECORD (F9)";
                btnRecord.BackColor = Color.FromArgb(34, 197, 94);

                btnClear.Enabled = true;
                btnLoad.Enabled = true;
                chkRecordMouse.Enabled = true;
                chkHighPrecision.Enabled = true;
                trackSpeed.Enabled = true;
                numLoops.Enabled = true;

                lblStatus.Text = "✓ Recording Complete";
                lblStatus.ForeColor = Color.FromArgb(34, 197, 94);
                SafeUpdateUI();
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
                ignoreNextClick = true; // ИСПРАВЛЕНИЕ: Игнорируем клик по кнопке Play

                btnPlay.Text = "■ STOP (Shift+Tab)";
                btnPlay.BackColor = Color.FromArgb(251, 146, 60);
                btnRecord.Enabled = false;
                btnClear.Enabled = false;
                btnSave.Enabled = false;
                btnLoad.Enabled = false;
                chkRecordMouse.Enabled = false;
                chkHighPrecision.Enabled = false;
                trackSpeed.Enabled = false;
                numLoops.Enabled = false;

                lblStatus.Text = $"▶ Playing at {trackSpeed.Value}% speed...";
                lblStatus.ForeColor = Color.FromArgb(251, 146, 60);

                // ИСПРАВЛЕНИЕ: Получаем значения UI перед запуском потока
                int loopsToPlay = (int)numLoops.Value;
                double speed = trackSpeed.Value / 100.0;

                playThread = new Thread(() => PlayLoop(loopsToPlay, speed))
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

        private void PlayLoop(int loops, double speedMultiplier)
        {
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

            // ИСПРАВЛЕНИЕ: Нормализация временных меток
            long firstActionTicks = playbackActions[0].TimeTicks;
            var normalizedActions = playbackActions.Select(a => new MacroAction
            {
                Type = a.Type,
                Key = a.Key,
                X = a.X,
                Y = a.Y,
                Button = a.Button,
                Down = a.Down,
                WheelDelta = a.WheelDelta,
                TimeTicks = a.TimeTicks - firstActionTicks
            }).ToList();

            // ИСПРАВЛЕНИЕ: Задержка перед стартом для предотвращения регистрации клика по Play
            Thread.Sleep(100);

            for (int loop = 0; loop < loops && !stopFlag; loop++)
            {
                Stopwatch playbackTimer = Stopwatch.StartNew();

                foreach (var action in normalizedActions)
                {
                    if (stopFlag) break;

                    long targetTicks = (long)(action.TimeTicks / speedMultiplier);
                    PreciseWait(playbackTimer, targetTicks);

                    Exec(action);
                    Interlocked.Increment(ref totalActionsPlayed);
                }

                if (stopFlag) break;

                // Пауза между циклами
                if (loop < loops - 1)
                {
                    Thread.Sleep(50);
                }
            }

            StopPlay();
        }

        private void PreciseWait(Stopwatch timer, long targetTicks)
        {
            long remainingTicks = targetTicks - timer.ElapsedTicks;
            if (remainingTicks <= 0) return;

            double remainingMs = remainingTicks / (double)TimeSpan.TicksPerMillisecond;

            // Sleep для больших задержек (экономия CPU)
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
            while (timer.ElapsedTicks < targetTicks && !stopFlag)
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

                // Клампинг координат
                absX = Math.Max(0, Math.Min(65535, absX));
                absY = Math.Max(0, Math.Min(65535, absY));

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
            ignoreNextClick = false;

            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    btnPlay.Text = "▶ PLAY";
                    btnPlay.BackColor = Color.FromArgb(59, 130, 246);
                    btnRecord.Enabled = true;
                    btnClear.Enabled = true;
                    btnSave.Enabled = true;
                    btnLoad.Enabled = true;
                    chkRecordMouse.Enabled = true;
                    chkHighPrecision.Enabled = true;
                    trackSpeed.Enabled = true;
                    numLoops.Enabled = true;
                    lblStatus.Text = "⏹ Playback Stopped";
                    lblStatus.ForeColor = Color.FromArgb(156, 163, 175);
                    SafeUpdateUI();
                }));
            }
        }

        private void SaveMacro()
        {
            lock (actionsLock)
            {
                if (actions.Count == 0)
                {
                    MessageBox.Show("No actions to save!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
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

                        SafeUpdateUI();
                        lblStatus.Text = "✓ Macro Loaded";
                        lblStatus.ForeColor = Color.FromArgb(34, 197, 94);
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

            if (updateTimer != null)
            {
                updateTimer.Stop();
                updateTimer.Dispose();
            }

            if (kbHook != IntPtr.Zero) UnhookWindowsHookEx(kbHook);
            if (mouseHook != IntPtr.Zero) UnhookWindowsHookEx(mouseHook);

            // Освобождаем GC handles
            if (kbProcHandle.IsAllocated) kbProcHandle.Free();
            if (mouseProcHandle.IsAllocated) mouseProcHandle.Free();

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