using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MacroRecorderPro.Core;
using MacroRecorderPro.Interfaces;

namespace MacroRecorderPro.UI
{
    // MVP Pattern - View layer (SRP - отвечает только за UI)
    public class MacroForm : Form
    {
        private readonly DependencyContainer container;
        private readonly IMacroRecorder recorder;
        private readonly IMacroPlayer player;
        private readonly IMacroStorage storage;
        private readonly IMacroRepository repository;
        private readonly RecordingConfiguration recordingConfig;
        private readonly PlaybackConfiguration playbackConfig;
        private readonly MacroCoordinator coordinator;

        private GradientButton btnRecord, btnPlay;
        private ModernButton btnClear, btnSave, btnLoad;
        private Label lblStatus, lblCount, lblSpeed;
        private ModernCheckBox chkRecordMouse, chkHighPrecision;
        private ModernTrackBar trackSpeed;
        private ModernNumericUpDown numLoops;
        private GradientPanel headerPanel, statusPanel;
        private Panel controlPanel;
        private System.Windows.Forms.Timer updateTimer;

        public MacroForm()
        {
            // Инициализация зависимостей через DI Container
            container = new DependencyContainer();
            recorder = container.GetRecorder();
            player = container.GetPlayer();
            storage = container.GetStorage();
            repository = container.GetRepository();
            recordingConfig = container.GetRecordingConfiguration();
            playbackConfig = container.GetPlaybackConfiguration();
            coordinator = container.GetCoordinator();

            InitializeUI();
            SetupEventHandlers();
            coordinator.Initialize();
            StartUIUpdateTimer();
        }

        private void SetupEventHandlers()
        {
            recorder.RecordingStarted += OnRecordingStarted;
            recorder.RecordingStopped += OnRecordingStopped;
            recorder.ActionsChanged += (s, e) => UpdateUI();

            player.PlaybackStarted += OnPlaybackStarted;
            player.PlaybackStopped += OnPlaybackStopped;

            coordinator.StatusChanged += (s, msg) => UpdateStatus(msg);
        }

        private void StartUIUpdateTimer()
        {
            updateTimer = new System.Windows.Forms.Timer
            {
                Interval = 100
            };
            updateTimer.Tick += (s, e) => UpdateUI();
            updateTimer.Start();
        }

        private void OnRecordingStarted(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnRecordingStarted(sender, e)));
                return;
            }

            btnRecord.Text = "■ STOP (F9)";
            SetControlsEnabled(false);
            UpdateStatus("● RECORDING...", ColorScheme.DangerGlow);
        }

        private void OnRecordingStopped(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnRecordingStopped(sender, e)));
                return;
            }

            btnRecord.Text = "● RECORD (F9)";
            SetControlsEnabled(true);
            UpdateStatus("✓ Recording Complete", ColorScheme.SuccessGlow);
        }

        private void OnPlaybackStarted(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnPlaybackStarted(sender, e)));
                return;
            }

            btnPlay.Text = "■ STOP (Shift+Tab)";
            SetControlsEnabled(false);
            UpdateStatus($"▶ Playing at {trackSpeed.Value}% speed...", ColorScheme.AccentLight);
        }

        private void OnPlaybackStopped(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnPlaybackStopped(sender, e)));
                return;
            }

            btnPlay.Text = "▶ PLAY";
            SetControlsEnabled(true);
            UpdateStatus("⏹ Playback Stopped", ColorScheme.TextSecondary);
        }

        private void UpdateUI()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateUI));
                return;
            }

            int count = repository.Count;
            double duration = CalculateDuration();

            lblCount.Text = count > 0
                ? $"Actions: {count} | Duration: {duration:F2}s"
                : "Actions: 0 | Duration: 0.0s";

            btnPlay.Enabled = count > 0 && !recorder.IsRecording;
            btnSave.Enabled = count > 0 && !recorder.IsRecording;
        }

        private double CalculateDuration()
        {
            var actions = repository.GetAll();
            if (actions.Count == 0)
                return 0;

            return (actions[actions.Count - 1].TimeTicks - actions[0].TimeTicks) /
                   (double)TimeSpan.TicksPerSecond;
        }

        private void UpdateStatus(string message, Color? color = null)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatus(message, color)));
                return;
            }

            lblStatus.Text = message;
            if (color.HasValue)
                lblStatus.ForeColor = color.Value;
        }

        private void SetControlsEnabled(bool enabled)
        {
            btnRecord.Enabled = enabled;
            btnPlay.Enabled = enabled && repository.Count > 0;
            btnClear.Enabled = enabled;
            btnSave.Enabled = enabled && repository.Count > 0;
            btnLoad.Enabled = enabled;
            chkRecordMouse.Enabled = enabled;
            chkHighPrecision.Enabled = enabled;
            trackSpeed.Enabled = enabled;
            numLoops.Enabled = enabled;
        }

        private void OnRecordClick(object sender, EventArgs e)
        {
            if (recorder.IsRecording)
                recorder.StopRecording();
            else
                recorder.StartRecording();
        }

        private void OnPlayClick(object sender, EventArgs e)
        {
            if (player.IsPlaying)
                player.Stop();
            else
            {
                playbackConfig.LoopCount = (int)numLoops.Value;
                playbackConfig.SpeedMultiplier = trackSpeed.Value / 100.0;
                player.Play();
            }
        }

        private void OnClearClick(object sender, EventArgs e)
        {
            if (recorder.IsRecording || player.IsPlaying)
            {
                MessageBox.Show("Cannot clear while recording or playing!", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            recorder.Clear();
            UpdateStatus("Ready", ColorScheme.TextSecondary);
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            if (repository.Count == 0)
            {
                MessageBox.Show("No actions to save!", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Macro Files (*.macro)|*.macro|All Files (*.*)|*.*";
                dialog.DefaultExt = "macro";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        storage.Save(dialog.FileName);
                        MessageBox.Show("Macro saved successfully!", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving macro: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void OnLoadClick(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Macro Files (*.macro)|*.macro|All Files (*.*)|*.*";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        storage.Load(dialog.FileName);
                        UpdateStatus("✓ Macro Loaded", ColorScheme.SuccessGlow);
                        MessageBox.Show($"Loaded {repository.Count} actions successfully!", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading macro: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            player.Stop();
            updateTimer?.Stop();
            updateTimer?.Dispose();
            coordinator?.Dispose();
            base.OnFormClosing(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Добавляем легкое свечение по краям формы
            using (Pen glowPen = new Pen(Color.FromArgb(30, ColorScheme.AccentLight), 2))
            {
                e.Graphics.DrawRectangle(glowPen, 0, 0, Width - 1, Height - 1);
            }
        }

        private void InitializeUI()
        {
            Text = "Macro Recorder Pro";
            ClientSize = new Size(520, 500);
            MinimumSize = new Size(520, 500);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            TopMost = true;
            BackColor = ColorScheme.Background;

            CreateHeaderPanel();
            CreateControlPanel();
            CreateStatusPanel();

            Controls.AddRange(new Control[] { headerPanel, controlPanel, statusPanel });
        }

        private void CreateHeaderPanel()
        {
            headerPanel = new GradientPanel
            {
                Dock = DockStyle.Top,
                Height = 90,
                GradientColorStart = ColorScheme.AccentDark,
                GradientColorMiddle = ColorScheme.Accent,
                GradientColorEnd = ColorScheme.AccentLight,
                GradientAngle = 135f,
                UseThreeColorGradient = true
            };

            var lblTitle = new Label
            {
                Text = "MACRO RECORDER",
                Location = new Point(0, 20),
                Size = new Size(520, 30),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var lblSubtitle = new Label
            {
                Text = "PRO EDITION",
                Location = new Point(0, 52),
                Size = new Size(520, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 255, 255, 255),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
        }

        private void CreateControlPanel()
        {
            controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorScheme.Background,
                Padding = new Padding(25, 20, 25, 20)
            };

            btnRecord = UIFactory.CreateGradientButton("● RECORD (F9)", 0, 15,
                ColorScheme.SuccessDark, ColorScheme.Success, ColorScheme.SuccessGlow);
            btnRecord.Click += OnRecordClick;

            btnPlay = UIFactory.CreateGradientButton("▶ PLAY", 0, 85,
                ColorScheme.AccentDark, ColorScheme.Accent, ColorScheme.AccentLight);
            btnPlay.Click += OnPlayClick;
            btnPlay.Enabled = false;

            var settingsY = 155;

            // Settings card
            var settingsCard = new Panel
            {
                Location = new Point(10, settingsY),
                Size = new Size(450, 130),
                BackColor = ColorScheme.Surface
            };
            UIFactory.ApplyRoundedCorners(settingsCard, 20);

            chkRecordMouse = UIFactory.CreateCheckBox("Record Mouse Moves", 15, 15);
            chkRecordMouse.Checked = true;
            chkRecordMouse.CheckedChanged += (s, e) => recordingConfig.RecordMouseMoves = chkRecordMouse.Checked;

            chkHighPrecision = UIFactory.CreateCheckBox("High Precision Mode", 260, 15);
            chkHighPrecision.ForeColor = ColorScheme.AccentLight;
            chkHighPrecision.CheckedChanged += (s, e) => recordingConfig.HighPrecision = chkHighPrecision.Checked;

            var lblLoops = UIFactory.CreateLabel("Loops:", 15, 52);
            numLoops = UIFactory.CreateNumericUpDown(80, 50);

            lblSpeed = UIFactory.CreateLabel("Speed: 100%", 15, 87, 110);
            trackSpeed = UIFactory.CreateTrackBar(130, 82);
            trackSpeed.ValueChanged += (s, e) => lblSpeed.Text = $"Speed: {trackSpeed.Value}%";

            settingsCard.Controls.AddRange(new Control[] {
                chkRecordMouse, chkHighPrecision,
                lblLoops, numLoops, lblSpeed, trackSpeed
            });

            var btnY = settingsY + 145;
            btnSave = UIFactory.CreateModernButton("💾 Save", 10, btnY);
            btnSave.Click += OnSaveClick;

            btnLoad = UIFactory.CreateModernButton("📂 Load", 170, btnY);
            btnLoad.Click += OnLoadClick;

            btnClear = UIFactory.CreateModernButton("🗑 Clear", 330, btnY);
            btnClear.Click += OnClearClick;

            controlPanel.Controls.AddRange(new Control[] {
                btnRecord, btnPlay, settingsCard,
                btnSave, btnLoad, btnClear
            });
        }

        private void CreateStatusPanel()
        {
            statusPanel = new GradientPanel
            {
                Dock = DockStyle.Bottom,
                Height = 100,
                GradientColorStart = ColorScheme.Surface,
                GradientColorMiddle = ColorScheme.SurfaceLight,
                GradientColorEnd = ColorScheme.Surface,
                GradientAngle = 90f,
                UseThreeColorGradient = true
            };

            lblStatus = new Label
            {
                Text = "Ready",
                Location = new Point(20, 20),
                Size = new Size(480, 30),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = ColorScheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            lblCount = new Label
            {
                Text = "Actions: 0 | Duration: 0.0s",
                Location = new Point(20, 55),
                Size = new Size(480, 25),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = ColorScheme.TextSecondary,
                TextAlign = ContentAlignment.TopCenter,
                BackColor = Color.Transparent
            };

            statusPanel.Controls.AddRange(new Control[] { lblStatus, lblCount });
        }
    }
}