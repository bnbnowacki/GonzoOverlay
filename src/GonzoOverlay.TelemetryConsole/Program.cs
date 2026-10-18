using Microsoft.Extensions.Logging;
using SVappsLAB.iRacingTelemetrySDK;

[RequiredTelemetryVars([
    TelemetryVar.Speed,
    TelemetryVar.RPM,
    TelemetryVar.Gear,
    TelemetryVar.FuelLevel])]
internal static class Program
{
    private static readonly object ConsoleLock = new();
    private static TelemetrySnapshot? _latestTelemetry;
    private static string _connectionState = "Starting";
    private static long _sampleCount;

    public static async Task Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information)
                .AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
        });

        var logger = loggerFactory.CreateLogger("GonzoOverlay.TelemetryConsole");
        using var cancellation = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        PrintHeader();

        var displayTask = DisplayLoopAsync(cancellation.Token);
        await MonitorUntilCancelledAsync(logger, cancellation.Token);

        cancellation.Cancel();
        await displayTask;
        WriteLine("Zakończono.");
    }

    private static async Task MonitorUntilCancelledAsync(
        ILogger logger,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Volatile.Write(ref _connectionState, "Waiting for iRacing");

                await using var client = TelemetryClient<TelemetryData>.Create(logger);
                var handlers = new TelemetryHandlers<TelemetryData>
                {
                    OnTelemetryUpdate = data =>
                    {
                        var speed = data.Speed is { } speedMetersPerSecond
                            ? $"{speedMetersPerSecond * 3.6f:F1} km/h ({speedMetersPerSecond:F1} m/s)"
                            : "N/A";

                        var snapshot = new TelemetrySnapshot(
                            speed,
                            data.RPM?.ToString("F0") ?? "N/A",
                            data.Gear?.ToString() ?? "N/A",
                            data.FuelLevel?.ToString("F2") ?? "N/A");

                        Interlocked.Exchange(ref _latestTelemetry, snapshot);
                        Interlocked.Increment(ref _sampleCount);
                        return Task.CompletedTask;
                    },
                    OnConnectStateChanged = state =>
                    {
                        var stateText = state.ToString();
                        Volatile.Write(ref _connectionState, stateText);
                        WriteLine($"Stan połączenia: {stateText}");
                        return Task.CompletedTask;
                    },
                    OnError = error =>
                    {
                        logger.LogError(error, "Błąd SDK telemetrii");
                        return Task.CompletedTask;
                    }
                };

                await client.Monitor(handlers, cancellationToken);

                if (!cancellationToken.IsCancellationRequested)
                {
                    Volatile.Write(ref _connectionState, "Monitor stopped; retrying");
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Monitor zakończył się błędem; ponawiam połączenie");
                Volatile.Write(ref _connectionState, "Retrying after error");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private static async Task DisplayLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshot = Volatile.Read(ref _latestTelemetry);
            var connectionState = Volatile.Read(ref _connectionState);
            var samples = Interlocked.Read(ref _sampleCount);
            var telemetryText = snapshot is null
                ? "brak danych"
                : $"Prędkość: {snapshot.Speed} | Obroty: {snapshot.Rpm} RPM | "
                  + $"Bieg: {snapshot.Gear} | Paliwo: {snapshot.FuelLevel} L";

            WriteLine($"[{DateTime.Now:HH:mm:ss}] {connectionState} | {telemetryText} | Próbek: {samples}");

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static void PrintHeader()
    {
        WriteLine("GonzoOverlay — test telemetrii iRacing");
        WriteLine("Uruchom iRacing, aby rozpocząć odczyt. Ctrl+C kończy program.");
        WriteLine(new string('-', 110));
    }

    private static void WriteLine(string message)
    {
        lock (ConsoleLock)
        {
            Console.WriteLine(message);
        }
    }

    private sealed record TelemetrySnapshot(
        string Speed,
        string Rpm,
        string Gear,
        string FuelLevel);
}
