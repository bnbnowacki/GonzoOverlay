# GonzoOverlay

Pierwszy etap projektu: konsolowy monitor telemetrii iRacing dla Windows. Projekt nie zawiera jeszcze graficznego overlaya, WPF, WebView2 ani interfejsu React/TypeScript.

## Wymagania

- Windows 10/11
- .NET SDK 8.0 lub nowszy
- zainstalowany iRacing oraz działająca sesja symulatora

## Uruchomienie

Z katalogu głównego repozytorium:

```powershell
dotnet restore .\GonzoOverlay.sln
dotnet build .\GonzoOverlay.sln
dotnet run --project .\src\GonzoOverlay.TelemetryConsole\GonzoOverlay.TelemetryConsole.csproj
```

Program czeka na połączenie z iRacing i pokazuje co sekundę:

- prędkość w km/h oraz wartość źródłową w m/s,
- obroty silnika,
- aktualny bieg,
- poziom paliwa w litrach.

Naciśnij `Ctrl+C`, aby zakończyć. Stan połączenia jest raportowany przez callback SDK. Po rozłączeniu program pozostaje aktywny i czeka na ponowne połączenie; dodatkowa pętla retry zabezpiecza też przypadek nieoczekiwanego zakończenia monitora.

## Test z iRacing

1. Uruchom iRacing i zaloguj się.
2. Uruchom test drive albo dołącz do sesji, tak aby symulator udostępniał dane telemetryczne.
3. W drugim oknie PowerShell uruchom komendę `dotnet run` z sekcji powyżej.
4. Sprawdź, czy stan przechodzi do `Connected` oraz czy wartości prędkości, RPM, biegu i paliwa aktualizują się co sekundę.
5. Zamknij sesję iRacing. Konsola powinna zasygnalizować rozłączenie, a po ponownym uruchomieniu sesji — ponowne połączenie bez restartu aplikacji.
6. Zakończ test przez `Ctrl+C`.

Jeżeli aplikacja długo pokazuje `Waiting for iRacing`, sprawdź, czy iRacing faktycznie działa na tym samym komputerze i czy aplikacja nie jest uruchomiona w środowisku innym niż Windows. Live telemetry SDK wymaga działającego iRacing; odtwarzanie plików IBT nie jest jeszcze częścią tego etapu.

## Struktura

```text
GonzoOverlay.sln
├── README.md
└── src
    └── GonzoOverlay.TelemetryConsole
        ├── GonzoOverlay.TelemetryConsole.csproj
        └── Program.cs
```

## SDK

Projekt korzysta z `SVappsLAB.iRacingTelemetrySDK` 2.3.0. `TelemetryData` jest generowany przez SDK na podstawie atrybutu `RequiredTelemetryVars`; nie należy definiować tego typu ręcznie.
