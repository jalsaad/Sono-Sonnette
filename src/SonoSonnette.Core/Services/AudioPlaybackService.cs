using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SonoSonnette.Core.Models;

namespace SonoSonnette.Core.Services;

public interface IAudioPlaybackService
{
    /// <summary>Liste les périphériques de sortie actifs (ex: "Haut-parleurs", "Sortie Jack / Line Out").</summary>
    IReadOnlyList<AudioOutputDevice> GetOutputDevices();

    Task PlayAsync(string filePath, string? deviceId, float volume, CancellationToken cancellationToken = default);

    /// <summary>Joue un court bip de test sur le périphérique choisi, pour vérifier la sortie audio sélectionnée.</summary>
    Task PlayTestToneAsync(string? deviceId, float volume, CancellationToken cancellationToken = default);
}

/// <summary>Lecture audio via WASAPI (NAudio), avec choix explicite du périphérique de sortie (haut-parleurs, Jack, casque, etc.).</summary>
public sealed class AudioPlaybackService : IAudioPlaybackService
{
    public IReadOnlyList<AudioOutputDevice> GetOutputDevices()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<AudioOutputDevice>();
        }

        using var enumerator = new MMDeviceEnumerator();

        string? defaultDeviceId = null;
        try
        {
            using var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            defaultDeviceId = defaultDevice.ID;
        }
        catch (COMException)
        {
            // Aucun périphérique de sortie par défaut n'est configuré sur cette machine.
        }

        var devices = new List<AudioOutputDevice>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            devices.Add(new AudioOutputDevice(device.ID, device.FriendlyName, device.ID == defaultDeviceId));
            device.Dispose();
        }

        return devices;
    }

    public Task PlayAsync(string filePath, string? deviceId, float volume, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("La lecture audio n'est disponible que sous Windows.");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Fichier audio introuvable.", filePath);
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => PlayOnDedicatedThread(filePath, deviceId, volume, tcs, cancellationToken))
        {
            IsBackground = true,
            Name = "SonoSonnette-Playback",
        };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();

        return tcs.Task;
    }

    public Task PlayTestToneAsync(string? deviceId, float volume, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("La lecture audio n'est disponible que sous Windows.");
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => PlayToneOnDedicatedThread(deviceId, volume, tcs, cancellationToken))
        {
            IsBackground = true,
            Name = "SonoSonnette-TestTone",
        };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();

        return tcs.Task;
    }

    private static void PlayToneOnDedicatedThread(
        string? deviceId,
        float volume,
        TaskCompletionSource tcs,
        CancellationToken cancellationToken)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = string.IsNullOrEmpty(deviceId)
                ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                : enumerator.GetDevice(deviceId);

            var tone = new SignalGenerator(44100, 1)
            {
                Type = SignalGeneratorType.Sin,
                Frequency = 880,
                Gain = Math.Clamp(volume, 0f, 1f),
            };
            var toneProvider = new OffsetSampleProvider(tone) { Take = TimeSpan.FromMilliseconds(600) };

            using var output = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
            using var stoppedEvent = new ManualResetEventSlim(false);

            Exception? playbackError = null;
            output.PlaybackStopped += (_, e) =>
            {
                playbackError = e.Exception;
                stoppedEvent.Set();
            };

            output.Init(toneProvider.ToWaveProvider());
            output.Play();

            using (cancellationToken.Register(() => output.Stop()))
            {
                stoppedEvent.Wait(cancellationToken);
            }

            if (playbackError is not null)
            {
                tcs.TrySetException(playbackError);
            }
            else
            {
                tcs.TrySetResult();
            }
        }
        catch (OperationCanceledException)
        {
            tcs.TrySetCanceled(cancellationToken);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    }

    private static void PlayOnDedicatedThread(
        string filePath,
        string? deviceId,
        float volume,
        TaskCompletionSource tcs,
        CancellationToken cancellationToken)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = string.IsNullOrEmpty(deviceId)
                ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                : enumerator.GetDevice(deviceId);

            using var audioFile = new AudioFileReader(filePath) { Volume = Math.Clamp(volume, 0f, 1f) };
            using var output = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
            using var stoppedEvent = new ManualResetEventSlim(false);

            Exception? playbackError = null;
            output.PlaybackStopped += (_, e) =>
            {
                playbackError = e.Exception;
                stoppedEvent.Set();
            };

            output.Init(audioFile);
            output.Play();

            using (cancellationToken.Register(() => output.Stop()))
            {
                stoppedEvent.Wait(cancellationToken);
            }

            if (playbackError is not null)
            {
                tcs.TrySetException(playbackError);
            }
            else
            {
                tcs.TrySetResult();
            }
        }
        catch (OperationCanceledException)
        {
            tcs.TrySetCanceled(cancellationToken);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    }
}
