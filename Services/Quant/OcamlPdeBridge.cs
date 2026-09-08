using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

        using var process = new Process { StartInfo = startInfo };
        using var cts = new CancellationTokenSource(_timeout);

        try
        {
            process.Start();

            // Write input
            using (var sw = process.StandardInput)
            {
                await sw.WriteLineAsync(jsonInput).WaitAsync(cts.Token);
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
                _logger.LogError("OCaml PDE Solver error: {Error}", error);
                return new PdeResponse(false, 0, 0, 0, new Greeks(0,0,0,0,0), error);
            }

            var result = JsonSerializer.Deserialize<PdeResponse>(output);

            if (result == null)
            {
                throw new Exception("Failed to deserialize OCaml output.");
            }

            return result;
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            _logger.LogError("OCaml PDE Solver timed out after {Timeout}s", _timeout.TotalSeconds);
            try { process.Kill(entireProcessTree: true); } catch { }
            return new PdeResponse(false, 0, 0, 0, new Greeks(0,0,0,0,0), $"PDE solver timed out after {_timeout.TotalSeconds}s");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure in OCaml PDE Bridge");
            return new PdeResponse(false, 0, 0, 0, new Greeks(0,0,0,0,0), ex.Message);
        }
    }
}
