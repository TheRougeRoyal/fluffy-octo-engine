using System.Diagnostics;
using System.Text;
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

    public OcamlPdeBridge(ILogger<OcamlPdeBridge> logger, IOptions<TradingServerConfig> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public async Task<PdeResponse> GetFairValueAsync(PdeRequest request)
    {
        try
        {
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

            process.Start();

            using (var sw = process.StandardInput)
            {
                await sw.WriteLineAsync(jsonInput);
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure in OCaml PDE Bridge");
            return new PdeResponse(false, 0, 0, 0, new Greeks(0,0,0,0,0), ex.Message);
        }
    }
}
