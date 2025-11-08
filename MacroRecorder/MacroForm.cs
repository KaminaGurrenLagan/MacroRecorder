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

        private Button btnRecord, btnPlay, btnClear, btnSave, btnLoad;
        private Label lblStatus, lblCount, lblSpeed;
        private CheckBox chkRecordMouse, chkHighPrecision;
        private TrackBar trackSpeed;
        private NumericUpDown numLoops;
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

        private void InitializeUI()
        {
            Text = "Macro Recorder Pro";
            ClientSize = new Size(480, 420);
            MinimumSize = new Size(480, 420);
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
                Height = 70,
                GradientColorStart = ColorScheme.AccentDark,
                GradientColorEnd = ColorScheme.AccentLight,
                GradientAngle = 135f
            };

            var lblTitle = new Label
            {
                Text = "MACRO RECORDER PRO",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            headerPanel.Controls.Add(lblTitle);
        }

        private void CreateControlPanel()
        {
            controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorScheme.Background,
                Padding = new Padding(20, 15, 20, 15)
            };

            btnRecord = UIFactory.CreateGradientButton("● RECORD (F9)", 0, 10,
                ColorScheme.SuccessDark, ColorScheme.SuccessGlow);
            btnRecord.Click += OnRecordClick;

            btnPlay = UIFactory.CreateGradientButton("▶ PLAY", 0, 75,
                ColorScheme.AccentDark, ColorScheme.AccentLight);
            btnPlay.Click += OnPlayClick;
            btnPlay.Enabled = false;

            var settingsY = 140;

            chkRecordMouse = UIFactory.CreateCheckBox("Record Mouse Moves", 10, settingsY);
            chkRecordMouse.Checked = true;
            chkRecordMouse.CheckedChanged += (s, e) => recordingConfig.RecordMouseMoves = chkRecordMouse.Checked;

            chkHighPrecision = UIFactory.CreateCheckBox("High Precision", 250, settingsY);
            chkHighPrecision.ForeColor = ColorScheme.AccentLight;
            chkHighPrecision.CheckedChanged += (s, e) => recordingConfig.HighPrecision = chkHighPrecision.Checked;

            var lblLoops = UIFactory.CreateLabel("Loops:", 10, settingsY + 40);
            numLoops = UIFactory.CreateNumericUpDown(70, settingsY + 38);

            lblSpeed = UIFactory.CreateLabel("Speed: 100%", 160, settingsY + 40, 100);
            trackSpeed = UIFactory.CreateTrackBar(270, settingsY + 35);
            trackSpeed.ValueChanged += (s, e) => lblSpeed.Text = $"Speed: {trackSpeed.Value}%";

            var btnY = settingsY + 85;
            btnSave = UIFactory.CreateSmallButton("💾 Save", 10, btnY, ColorScheme.Surface);
            btnSave.Click += OnSaveClick;

            btnLoad = UIFactory.CreateSmallButton("📂 Load", 155, btnY, ColorScheme.Surface);
            btnLoad.Click += OnLoadClick;

            btnClear = UIFactory.CreateSmallButton("🗑 Clear", 300, btnY, ColorScheme.DangerDark);
            btnClear.Click += OnClearClick;

            controlPanel.Controls.AddRange(new Control[] {
                btnRecord, btnPlay, chkRecordMouse, chkHighPrecision,
                lblLoops, numLoops, lblSpeed, trackSpeed,
                btnSave, btnLoad, btnClear
            });
        }

        private void CreateStatusPanel()
        {
            statusPanel = new GradientPanel
            {
                Dock = DockStyle.Bottom,
                Height = 90,
                GradientColorStart = ColorScheme.Surface,
                GradientColorEnd = ColorScheme.SurfaceLight,
                GradientAngle = 45f
            };

            lblStatus = new Label
            {
                Text = "Ready",
                Location = new Point(15, 15),
                Size = new Size(450, 30),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = ColorScheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            lblCount = new Label
            {
                Text = "Actions: 0 | Duration: 0.0s",
                Location = new Point(15, 50),
                Size = new Size(450, 25),
                Font = new Font("Segoe UI", 9f),
                ForeColor = ColorScheme.TextSecondary,
                TextAlign = ContentAlignment.TopCenter,
                BackColor = Color.Transparent
            };

            statusPanel.Controls.AddRange(new Control[] { lblStatus, lblCount });
        }
    }
}