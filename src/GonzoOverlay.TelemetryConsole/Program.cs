using GonzoOverlay.Core.Models;
using GonzoOverlay.Infrastructure.iRacing;
using Microsoft.Extensions.Logging;

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

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        await using var provider = new IracingTelemetryProvider(
            loggerFactory.CreateLogger<IracingTelemetryProvider>());

        provider.ConnectionStateChanged += state =>
        {
            Volatile.Write(ref _connectionState, state);
            WriteLine($"Stan połączenia: {state}");
        };
        provider.TelemetryUpdated += snapshot =>
        {
            Interlocked.Exchange(ref _latestTelemetry, snapshot);
            Interlocked.Increment(ref _sampleCount);
        };

        PrintHeader();

        var displayTask = DisplayLoopAsync(cancellation.Token);
        await provider.StartAsync(cancellation.Token);

        cancellation.Cancel();
        await displayTask;
        WriteLine("Zakończono.");
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
                : $"Prędkość: {snapshot.SpeedKmh?.ToString("F1") ?? "N/A"} km/h "
                  + $"({snapshot.SpeedMetersPerSecond?.ToString("F1") ?? "N/A"} m/s) | "
                  + $"Obroty: {snapshot.Rpm?.ToString("F0") ?? "N/A"} RPM | "
                  + $"Bieg: {snapshot.Gear?.ToString() ?? "N/A"} | "
                  + $"Paliwo: {snapshot.FuelLevelLiters?.ToString("F2") ?? "N/A"} L";

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
        WriteLine(new string('-', 120));
    }

    private static void WriteLine(string message)
    {
        lock (ConsoleLock)
        {
            Console.WriteLine(message);
        }
    }
}
