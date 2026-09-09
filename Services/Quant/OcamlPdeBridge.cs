using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingEngine;
using TradingEngine.Models;
using TradingEngine.Models.Quant;

namespace TradingEngine.Services.Quant;

public class OcamlPdeBridge : IPdeModel
{
    private readonly ILogger<OcamlPdeBridge> _logger;
    private readonly TradingServerConfig _config;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    public OcamlPdeBridge(ILogger<OcamlPdeBridge> logger, IOptions<TradingServerConfig> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public async Task<PdeResponse> GetFairValueAsync(PdeRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var outcome = "error";
        Process? process = null;
        using var cts = new CancellationTokenSource(_timeout);
        try
        {
            if (!File.Exists(_config.PdeBinaryPath))
            {
                _logger.LogError("OCaml PDE Binary not found at {Path}", _config.PdeBinaryPath);
                return new PdeResponse(false, 0, 0, 0, new Greeks(0,0,0,0,0), "PDE Binary missing");
            }

            var jsonInput = JsonSerializer.Serialize(request);
            var startInfo = new ProcessStartInfo
            {
                FileName = _config.PdeBinaryPath,
                Arguments = "",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process = new Process { StartInfo = startInfo };
            process.Start();

            // Write input
            using (var sw = process.StandardInput)
            {
                await sw.WriteLineAsync(jsonInput.AsMemory(), cts.Token);
            }

            // Read output with timeout
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var exitTask = process.WaitForExitAsync(cts.Token);

            await Task.WhenAll(outputTask, errorTask, exitTask).WaitAsync(cts.Token);

            var output = await outputTask;
            var error = await errorTask;

            if (!string.IsNullOrWhiteSpace(error) && string.IsNullOrWhiteSpace(output))
            {
                outcome = "error";
                _logger.LogError(
                    "OCaml PDE Solver error: {Error}. DurationMs: {DurationMs}",
                    error,
                    stopwatch.Elapsed.TotalMilliseconds);
                return new PdeResponse(false, 0, 0, 0, new Greeks(0,0,0,0,0), error);
            }

            var result = JsonSerializer.Deserialize<PdeResponse>(output);

            if (result == null)
            {
                throw new Exception("Failed to deserialize OCaml output.");
            }

            outcome = "success";
            return result;
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            outcome = "timeout";
            _logger.LogError("OCaml PDE Solver timed out after {Timeout}s", _timeout.TotalSeconds);
            try { process?.Kill(entireProcessTree: true); } catch { }
            return new PdeResponse(false, 0, 0, 0, new Greeks(0,0,0,0,0), $"PDE solver timed out after {_timeout.TotalSeconds}s");
        }
        catch (Exception ex)
        {
            outcome = "error";
            _logger.LogError(ex, "Critical failure in OCaml PDE Bridge");
            return new PdeResponse(false, 0, 0, 0, new Greeks(0,0,0,0,0), ex.Message);
        }
        finally
        {
            process?.Dispose();
            stopwatch.Stop();
            RecordDuration(stopwatch, outcome);
            _logger.LogInformation(
                "OCaml PDE bridge call completed with outcome {Outcome}. DurationMs: {DurationMs}",
                outcome,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static void RecordDuration(Stopwatch stopwatch, string outcome) =>
        TradingEngineInstrumentation.PdeBridgeDurationMs.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("outcome", outcome));
}
