using System.Diagnostics.Metrics;

namespace TradingEngine;

public static class TradingEngineInstrumentation
{
    // Export through OpenTelemetry or another MeterListener when a metrics backend is configured.
    public static readonly Meter Meter = new("TradingEngine");
    public static readonly Histogram<double> OrderProcessingDurationMs =
        Meter.CreateHistogram<double>("trading_engine.order_processing.duration", "ms");
    public static readonly Histogram<double> PdeBridgeDurationMs =
        Meter.CreateHistogram<double>("trading_engine.pde_bridge.duration", "ms");
}
