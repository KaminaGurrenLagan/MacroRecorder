using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MacroRecorderPro.Interfaces;
using MacroRecorderPro.Models;

namespace MacroRecorderPro.Core
{
    // SRP - отвечает только за воспроизведение макросов
    // DIP - зависит от абстракций
    public class MacroPlayer : IMacroPlayer
    {
        private readonly IMacroRepository repository;
        private readonly IActionExecutor executor;
        private readonly IPlaybackConfiguration configuration;

        private Thread playbackThread;
        private volatile bool isPlaying;
        private volatile bool stopRequested;

        public bool IsPlaying => isPlaying;

        public event EventHandler PlaybackStarted;
        public event EventHandler PlaybackStopped;
        public event EventHandler<int> ActionExecuted;

        public MacroPlayer(
            IMacroRepository repository,
            IActionExecutor executor,
            IPlaybackConfiguration configuration)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public void Play()
        {
            if (isPlaying || repository.Count == 0)
                return;

            isPlaying = true;
            stopRequested = false;

            PlaybackStarted?.Invoke(this, EventArgs.Empty);

            playbackThread = new Thread(PlaybackLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };
            playbackThread.Start();
        }

        public void Stop()
        {
            stopRequested = true;
        }

        private void PlaybackLoop()
        {
            var actions = repository.GetAll();
            if (actions.Count == 0)
            {
                StopPlayback();
                return;
            }

            var normalizedActions = NormalizeActions(actions);

            // Задержка перед началом
            Thread.Sleep(100);

            int totalActionsExecuted = 0;

            for (int loop = 0; loop < configuration.LoopCount && !stopRequested; loop++)
            {
                var timer = Stopwatch.StartNew();

                foreach (var action in normalizedActions)
                {
                    if (stopRequested)
                        break;

                    long targetTicks = (long)(action.TimeTicks / configuration.SpeedMultiplier);
                    WaitUntil(timer, targetTicks);

                    executor.Execute(action);
                    totalActionsExecuted++;
                    ActionExecuted?.Invoke(this, totalActionsExecuted);
                }

                if (stopRequested)
                    break;

                // Пауза между циклами
                if (loop < configuration.LoopCount - 1)
                    Thread.Sleep(50);
            }

            StopPlayback();
        }

        private List<MacroAction> NormalizeActions(List<MacroAction> actions)
        {
            if (actions.Count == 0)
                return actions;

            long firstActionTicks = actions[0].TimeTicks;

            return actions.Select(a =>
            {
                var normalized = a.Clone();
                normalized.TimeTicks -= firstActionTicks;
                return normalized;
            }).ToList();
        }

        private void WaitUntil(Stopwatch timer, long targetTicks)
        {
            long remainingTicks = targetTicks - timer.ElapsedTicks;
            if (remainingTicks <= 0)
                return;

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
            var spinner = new SpinWait();
            while (timer.ElapsedTicks < targetTicks && !stopRequested)
            {
                spinner.SpinOnce();
            }
        }

        private void StopPlayback()
        {
            isPlaying = false;
            stopRequested = false;
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        public void WaitForCompletion(int timeoutMs = 5000)
        {
            if (playbackThread != null && playbackThread.IsAlive)
                playbackThread.Join(timeoutMs);
        }
    }
}