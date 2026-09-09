using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TradingEngine.Models;

namespace TradingEngine.Services;

public sealed class PdeBinaryHealthCheck : IHealthCheck
{
    private readonly TradingServerConfig _config;

    public PdeBinaryHealthCheck(IOptions<TradingServerConfig> config)
    {
        _config = config.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var path = _config.PdeBinaryPath;
        if (!File.Exists(path))
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy($"OCaml PDE binary was not found at '{path}'."));
        }

        if (!OperatingSystem.IsWindows()
            && (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy($"OCaml PDE binary is not executable at '{path}'."));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
