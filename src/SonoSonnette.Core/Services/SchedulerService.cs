using SonoSonnette.Core.Models;

namespace SonoSonnette.Core.Services;

public sealed class ScheduleTriggeredEventArgs(ScheduleEntry schedule) : EventArgs
{
    public ScheduleEntry Schedule { get; } = schedule;
}

public sealed class ScheduleFailedEventArgs(ScheduleEntry schedule, Exception exception) : EventArgs
{
    public ScheduleEntry Schedule { get; } = schedule;
    public Exception Exception { get; } = exception;
}

/// <summary>
/// Vérifie périodiquement les plannings et déclenche la lecture audio quand l'heure et le jour de la semaine
/// correspondent. Une même entrée ne peut se déclencher qu'une fois par minute (protège contre les doubles
/// déclenchements dus à la fréquence de sondage).
/// </summary>
public sealed class SchedulerService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);

    private readonly IAudioPlaybackService _playbackService;
    private readonly Func<IReadOnlyList<ScheduleEntry>> _getSchedules;
    private readonly Func<(string? DeviceId, float Volume)> _getPlaybackOptions;
    private readonly TimeProvider _timeProvider;
    private readonly Timer _timer;
    private readonly HashSet<(Guid ScheduleId, DateOnly Date, int MinuteOfDay)> _firedThisMinute = new();

    public event EventHandler<ScheduleTriggeredEventArgs>? ScheduleTriggered;
    public event EventHandler<ScheduleFailedEventArgs>? ScheduleFailed;

    public SchedulerService(
        IAudioPlaybackService playbackService,
        Func<IReadOnlyList<ScheduleEntry>> getSchedules,
        Func<(string? DeviceId, float Volume)> getPlaybackOptions,
        TimeProvider? timeProvider = null)
    {
        _playbackService = playbackService;
        _getSchedules = getSchedules;
        _getPlaybackOptions = getPlaybackOptions;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _timer = new Timer(Tick, null, TimeSpan.Zero, PollInterval);
    }

    private void Tick(object? state)
    {
        var now = _timeProvider.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);
        var minuteOfDay = (currentTime.Hour * 60) + currentTime.Minute;

        _firedThisMinute.RemoveWhere(entry => entry.Date != today);

        foreach (var schedule in _getSchedules())
        {
            if (!schedule.Enabled || !schedule.IsActiveOn(now.DayOfWeek))
            {
                continue;
            }

            if (schedule.TimeOfDay.Hour != currentTime.Hour || schedule.TimeOfDay.Minute != currentTime.Minute)
            {
                continue;
            }

            if (!_firedThisMinute.Add((schedule.Id, today, minuteOfDay)))
            {
                continue;
            }

            Trigger(schedule);
        }
    }

    private void Trigger(ScheduleEntry schedule)
    {
        var (deviceId, volume) = _getPlaybackOptions();

        _playbackService.PlayAsync(schedule.AudioFilePath, deviceId, volume).ContinueWith(
            task =>
            {
                if (task.IsFaulted)
                {
                    ScheduleFailed?.Invoke(this, new ScheduleFailedEventArgs(schedule, task.Exception!.GetBaseException()));
                }
                else
                {
                    ScheduleTriggered?.Invoke(this, new ScheduleTriggeredEventArgs(schedule));
                }
            },
            TaskScheduler.Default);
    }

    public void Dispose() => _timer.Dispose();
}
