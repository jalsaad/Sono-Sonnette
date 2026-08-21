using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SonoSonnette.Core.Services;

/// <summary>
/// Diffusion en direct du microphone vers un périphérique de sortie ("ON AIR" / talkback), tant que <see cref="Start"/>
/// n'a pas été suivi d'un <see cref="Stop"/>. Utilisé pour des annonces live, indépendamment des plannings.
/// </summary>
public interface IMicPassthroughService : IDisposable
{
    bool IsActive { get; }

    void Start(string? outputDeviceId, float volume);

    void Stop();
}

public sealed class MicPassthroughService : IMicPassthroughService
{
    private readonly object _lock = new();
    private Thread? _thread;
    private volatile bool _stopRequested;

    public bool IsActive { get; private set; }

    public void Start(string? outputDeviceId, float volume)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("La diffusion du microphone n'est disponible que sous Windows.");
        }

        lock (_lock)
        {
            if (IsActive)
            {
                return;
            }

            _stopRequested = false;

            using var ready = new ManualResetEventSlim(false);
            Exception? startupError = null;

            _thread = new Thread(() => RunPassthrough(outputDeviceId, volume, ready, ex => startupError = ex))
            {
                IsBackground = true,
                Name = "SonoSonnette-OnAir",
            };
            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();

            if (!ready.Wait(TimeSpan.FromSeconds(5)))
            {
                _stopRequested = true;
                throw new TimeoutException("Le démarrage de la diffusion micro a expiré.");
            }

            if (startupError is not null)
            {
                _thread = null;
                throw startupError;
            }

            IsActive = true;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!IsActive)
            {
                return;
            }

            _stopRequested = true;
            _thread?.Join(TimeSpan.FromSeconds(2));
            _thread = null;
            IsActive = false;
        }
    }

    private void RunPassthrough(string? outputDeviceId, float volume, ManualResetEventSlim ready, Action<Exception> onError)
    {
        MMDeviceEnumerator? enumerator = null;
        MMDevice? outputDevice = null;
        WasapiCapture? capture = null;
        WasapiOut? output = null;

        try
        {
            enumerator = new MMDeviceEnumerator();
            outputDevice = string.IsNullOrEmpty(outputDeviceId)
                ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                : enumerator.GetDevice(outputDeviceId);

            capture = new WasapiCapture();

            var buffer = new BufferedWaveProvider(capture.WaveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2),
            };
            capture.DataAvailable += (_, e) => buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);

            var volumeProvider = new VolumeSampleProvider(buffer.ToSampleProvider())
            {
                Volume = Math.Clamp(volume, 0f, 1f),
            };

            output = new WasapiOut(outputDevice, AudioClientShareMode.Shared, true, 50);
            output.Init(volumeProvider);

            capture.StartRecording();
            output.Play();

            ready.Set();

            while (!_stopRequested)
            {
                Thread.Sleep(50);
            }
        }
        catch (Exception ex)
        {
            onError(ex);
            ready.Set();
        }
        finally
        {
            SafeStop(capture, output);
            output?.Dispose();
            capture?.Dispose();
            outputDevice?.Dispose();
            enumerator?.Dispose();
        }
    }

    private static void SafeStop(WasapiCapture? capture, WasapiOut? output)
    {
        try
        {
            capture?.StopRecording();
        }
        catch (COMException)
        {
            // Le périphérique a pu être débranché ou désactivé pendant la diffusion.
        }

        try
        {
            output?.Stop();
        }
        catch (COMException)
        {
            // Idem côté sortie.
        }
    }

    public void Dispose() => Stop();
}
