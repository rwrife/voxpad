using System.Diagnostics;
using Voxpad.Core.Audio;

namespace Voxpad.Core.Capture;

public sealed class MacOsFfmpegAudioCapture : IAudioCapture
{
    private const int WhisperSampleRateHz = DecodedAudioPcm.WhisperSampleRateHz;

    private readonly string ffmpegPath;
    private readonly string avFoundationInput;

    private Process? ffmpegProcess;
    private Task<string>? stderrTask;
    private Task? captureLoopTask;
    private Pcm16CaptureBuffer? captureBuffer;
    private Func<AudioLevelSample, ValueTask>? onLevelSample;

    public MacOsFfmpegAudioCapture(string ffmpegPath = "ffmpeg", string avFoundationInput = ":0")
    {
        this.ffmpegPath = ffmpegPath;
        this.avFoundationInput = avFoundationInput;
    }

    public bool IsRecording { get; private set; }

    public Task StartAsync(Func<AudioLevelSample, ValueTask>? onLevelSample = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("MacOsFfmpegAudioCapture is only supported on macOS.");
        }

        if (IsRecording)
        {
            throw new InvalidOperationException("Audio capture is already running.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("avfoundation");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(avFoundationInput);
        startInfo.ArgumentList.Add("-ac");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-ar");
        startInfo.ArgumentList.Add(WhisperSampleRateHz.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("s16le");
        startInfo.ArgumentList.Add("pipe:1");

        ffmpegProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start ffmpeg capture process.");

        this.onLevelSample = onLevelSample;
        captureBuffer = new Pcm16CaptureBuffer();
        stderrTask = ffmpegProcess.StandardError.ReadToEndAsync();
        captureLoopTask = CaptureStdoutAsync(ffmpegProcess, captureBuffer, this.onLevelSample, cancellationToken);

        IsRecording = true;
        return Task.CompletedTask;
    }

    public async Task<DecodedAudioPcm> StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRecording || ffmpegProcess is null || captureBuffer is null || stderrTask is null || captureLoopTask is null)
        {
            throw new InvalidOperationException("Audio capture is not running.");
        }

        var localProcess = ffmpegProcess;
        var localBuffer = captureBuffer;
        var localCaptureLoop = captureLoopTask;
        var localStderrTask = stderrTask;

        try
        {
            if (!localProcess.HasExited)
            {
                try
                {
                    await localProcess.StandardInput.WriteLineAsync("q").ConfigureAwait(false);
                    await localProcess.StandardInput.FlushAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort graceful shutdown.
                }
            }

            var waitForExit = localProcess.WaitForExitAsync(cancellationToken);
            var timeout = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            if (await Task.WhenAny(waitForExit, timeout).ConfigureAwait(false) != waitForExit && !localProcess.HasExited)
            {
                localProcess.Kill(entireProcessTree: true);
                await localProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }

            await localCaptureLoop.ConfigureAwait(false);
            var stderr = await localStderrTask.ConfigureAwait(false);

            if (localBuffer.SampleCount == 0)
            {
                if (LooksLikeMacPermissionError(stderr))
                {
                    throw new InvalidOperationException(
                        "Microphone access appears blocked. Grant microphone permissions to voxpad/terminal and retry.");
                }

                throw new InvalidOperationException($"No audio samples were captured from avfoundation input '{avFoundationInput}'. ffmpeg stderr: {stderr}");
            }

            if (localProcess.ExitCode != 0 && !LooksLikeGracefulFfmpegExit(stderr))
            {
                throw new InvalidOperationException($"ffmpeg capture exited with code {localProcess.ExitCode}: {stderr}");
            }

            return localBuffer.ToDecodedAudioPcm(WhisperSampleRateHz);
        }
        finally
        {
            CleanupProcess(localProcess);

            ffmpegProcess = null;
            captureBuffer = null;
            captureLoopTask = null;
            stderrTask = null;
            onLevelSample = null;
            IsRecording = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!IsRecording)
        {
            return;
        }

        try
        {
            _ = await StopAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort cleanup from dispose path.
        }
    }

    private static async Task CaptureStdoutAsync(
        Process ffmpegProcess,
        Pcm16CaptureBuffer buffer,
        Func<AudioLevelSample, ValueTask>? onLevelSample,
        CancellationToken cancellationToken)
    {
        var stream = ffmpegProcess.StandardOutput.BaseStream;
        var chunk = new byte[4096];

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            var copied = new byte[read];
            Buffer.BlockCopy(chunk, 0, copied, 0, read);
            buffer.Append(copied);

            if (onLevelSample is not null)
            {
                var evenByteCount = read - (read % sizeof(short));
                if (evenByteCount > 0)
                {
                    var levelBytes = evenByteCount == read ? copied : copied[..evenByteCount];
                    var level = AudioLevelMeter.CalculateFromPcm16Mono(levelBytes);
                    await onLevelSample(level).ConfigureAwait(false);
                }
            }
        }
    }

    private static bool LooksLikeGracefulFfmpegExit(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return true;
        }

        return stderr.Contains("Exiting normally", StringComparison.OrdinalIgnoreCase)
               || stderr.Contains("Immediate exit requested", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeMacPermissionError(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return false;
        }

        return stderr.Contains("not authorized", StringComparison.OrdinalIgnoreCase)
               || stderr.Contains("permission", StringComparison.OrdinalIgnoreCase)
               || stderr.Contains("cannot open audio device", StringComparison.OrdinalIgnoreCase)
               || stderr.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase);
    }

    private static void CleanupProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }

        process.Dispose();
    }
}
