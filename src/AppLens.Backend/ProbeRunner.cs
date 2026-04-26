using System.Diagnostics;

namespace AppLens.Backend;

public sealed class ProbeRunner
{
    private readonly List<ProbeStatus> _statuses = [];

    public IReadOnlyList<ProbeStatus> Statuses => _statuses;

    public async Task<T> RunAsync<T>(
        string name,
        Func<CancellationToken, Task<T>> action,
        T fallback,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await action(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _statuses.Add(new ProbeStatus
            {
                Name = name,
                State = ProbeState.Succeeded,
                Duration = stopwatch.Elapsed
            });
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _statuses.Add(new ProbeStatus
            {
                Name = name,
                State = ProbeState.Partial,
                Message = "Cancelled",
                Duration = stopwatch.Elapsed
            });
            return fallback;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _statuses.Add(new ProbeStatus
            {
                Name = name,
                State = ProbeState.Failed,
                Message = ex.Message,
                Duration = stopwatch.Elapsed
            });
            return fallback;
        }
    }

    public ToolProbe RunTool(string name, string fileName, string arguments, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (!process.Start())
            {
                return ToolStatus(name, ProbeState.Skipped, $"{fileName} did not start", stopwatch.Elapsed);
            }

            if (!process.WaitForExit(timeout))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort cleanup only.
                }

                return ToolStatus(name, ProbeState.Partial, $"{fileName} timed out", stopwatch.Elapsed);
            }

            var output = Formatting.OneLine((process.StandardOutput.ReadToEnd() + " " + process.StandardError.ReadToEnd()).Trim(), 240);
            return ToolStatus(name, ProbeState.Succeeded, output.Length == 0 ? "(no output)" : output, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            return ToolStatus(name, ProbeState.Skipped, ex.Message, stopwatch.Elapsed);
        }
    }

    private ToolProbe ToolStatus(string name, ProbeState state, string message, TimeSpan duration)
    {
        _statuses.Add(new ProbeStatus
        {
            Name = name,
            State = state,
            Message = message,
            Duration = duration
        });

        return new ToolProbe
        {
            Name = name,
            Status = state.ToString(),
            Output = message
        };
    }
}
