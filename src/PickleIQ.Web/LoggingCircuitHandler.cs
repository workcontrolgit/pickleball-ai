using Microsoft.AspNetCore.Components.Server.Circuits;

namespace PickleIQ.Web;

/// <summary>
/// Logs unhandled exceptions that occur inside Blazor Server circuits.
/// These are not caught by the HTTP exception handler middleware.
/// </summary>
public class LoggingCircuitHandler(ILogger<LoggingCircuitHandler> logger) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogDebug("Circuit opened: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogDebug("Circuit connection down: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnUnhandledExceptionAsync(Circuit circuit, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception in Blazor circuit {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }
}
