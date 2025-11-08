using System;
using System.Drawing;
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
        private Panel headerPanel, controlPanel, statusPanel;
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
            btnRecord.BackColor = ColorScheme.Danger;
            SetControlsEnabled(false);
            UpdateStatus("● RECORDING...", ColorScheme.Danger);
        }

        private void OnRecordingStopped(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnRecordingStopped(sender, e)));
                return;
            }

            btnRecord.Text = "● RECORD (F9)";
            btnRecord.BackColor = ColorScheme.Success;
            SetControlsEnabled(true);
            UpdateStatus("✓ Recording Complete", ColorScheme.Success);
        }

        private void OnPlaybackStarted(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnPlaybackStarted(sender, e)));
                return;
            }

            btnPlay.Text = "■ STOP (Shift+Tab)";
            btnPlay.BackColor = ColorScheme.Warning;
            SetControlsEnabled(false);
            UpdateStatus($"▶ Playing at {trackSpeed.Value}% speed...", ColorScheme.Warning);
        }

        private void OnPlaybackStopped(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnPlaybackStopped(sender, e)));
                return;
            }

            btnPlay.Text = "▶ PLAY";
            btnPlay.BackColor = ColorScheme.Primary;
            SetControlsEnabled(true);
            UpdateStatus("⏹ Playback Stopped", ColorScheme.Gray);
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
            UpdateStatus("Ready", ColorScheme.Gray);
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
                        UpdateStatus("✓ Macro Loaded", ColorScheme.Success);
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
            //player.WaitForCompletion(2000);

            updateTimer?.Stop();
            updateTimer?.Dispose();

            coordinator?.Dispose();

            base.OnFormClosing(e);
        }

        private void InitializeUI()
        {
            Text = "Macro Recorder Pro";
            ClientSize = new Size(450, 380);
            MinimumSize = new Size(450, 380);
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
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = ColorScheme.Surface
            };

            var lblTitle = new Label
            {
                Text = "MACRO RECORDER PRO",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = ColorScheme.Accent,
                TextAlign = ContentAlignment.MiddleCenter
            };
            headerPanel.Controls.Add(lblTitle);
        }

        private void CreateControlPanel()
        {
            controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorScheme.Background,
                Padding = new Padding(15, 10, 15, 10)
            };

            btnRecord = UIFactory.CreateButton("● RECORD (F9)", 0, 10, ColorScheme.Success);
            btnRecord.Click += OnRecordClick;

            btnPlay = UIFactory.CreateButton("▶ PLAY", 0, 70, ColorScheme.Primary);
            btnPlay.Click += OnPlayClick;
            btnPlay.Enabled = false;

            var settingsY = 130;

            chkRecordMouse = UIFactory.CreateCheckBox("Record Mouse Moves", 10, settingsY);
            chkRecordMouse.Checked = true;
            chkRecordMouse.CheckedChanged += (s, e) => recordingConfig.RecordMouseMoves = chkRecordMouse.Checked;

            chkHighPrecision = UIFactory.CreateCheckBox("High Precision", 220, settingsY);
            chkHighPrecision.ForeColor = ColorScheme.Warning;
            chkHighPrecision.CheckedChanged += (s, e) => recordingConfig.HighPrecision = chkHighPrecision.Checked;

            var lblLoops = UIFactory.CreateLabel("Loops:", 10, settingsY + 35);
            numLoops = UIFactory.CreateNumericUpDown(65, settingsY + 33);

            lblSpeed = UIFactory.CreateLabel("Speed: 100%", 150, settingsY + 35, 90);
            trackSpeed = UIFactory.CreateTrackBar(245, settingsY + 30);
            trackSpeed.ValueChanged += (s, e) => lblSpeed.Text = $"Speed: {trackSpeed.Value}%";

            var btnY = settingsY + 75;
            btnSave = UIFactory.CreateSmallButton("💾 Save", 10, btnY, ColorScheme.Secondary);
            btnSave.Click += OnSaveClick;

            btnLoad = UIFactory.CreateSmallButton("📂 Load", 145, btnY, ColorScheme.Secondary);
            btnLoad.Click += OnLoadClick;

            btnClear = UIFactory.CreateSmallButton("🗑 Clear", 280, btnY, ColorScheme.Danger);
            btnClear.Click += OnClearClick;

            controlPanel.Controls.AddRange(new Control[] {
                btnRecord, btnPlay, chkRecordMouse, chkHighPrecision,
                lblLoops, numLoops, lblSpeed, trackSpeed,
                btnSave, btnLoad, btnClear
            });
        }

        private void CreateStatusPanel()
        {
            statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = ColorScheme.Surface
            };

            lblStatus = new Label
            {
                Text = "Ready",
                Location = new Point(10, 10),
                Size = new Size(410, 25),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = ColorScheme.Gray,
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblCount = new Label
            {
                Text = "Actions: 0 | Duration: 0.0s",
                Location = new Point(10, 40),
                Size = new Size(410, 30),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ColorScheme.Gray,
                TextAlign = ContentAlignment.TopCenter
            };

            statusPanel.Controls.AddRange(new Control[] { lblStatus, lblCount });
        }
    }
}