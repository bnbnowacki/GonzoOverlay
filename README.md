# GonzoOverlay

GonzoOverlay to modułowy overlay dla symulatora iRacing na Windows. Aplikacja łączy natywny host WPF, przezroczyste okno WebView2 oraz frontend React/TypeScript. Dane z iRacing są pobierane na żywo przez SDK telemetryczne i przekazywane do aktywnych widgetów.

Projekt jest przygotowany tak, aby kolejne widgety można było dodawać niezależnie od siebie. Wszystkie aktywne widgety są renderowane w jednym pełnoekranowym oknie WebView2 i korzystają ze wspólnego systemu konfiguracji oraz edycji pozycji.

## Aktualne widgety

### Pogoda toru

Widget pogodowy pokazuje aktualny stan nawierzchni toru odczytany z TrackWetness. Przykładowe wartości to Dry, LightlyWet i VeryWet.

Widget ma własną pozycję i rozmiar, a jego tekst skaluje się razem z rozmiarem widgetu.

### Standings

Widget Standings pokazuje aktualną tabelę kierowców na podstawie danych z `SessionInfo` oraz tablic telemetrii `CarIdx`.

Kolumny są ułożone następująco: pozycja, kierowca, numer, SR, iRating, samochód, INT, LEADER i czas okrążenia.

Widget pokazuje nazwiska, numery startowe, pozycje, safety rating, iRating, samochód, interval do poprzedniej pozycji, stratę do lidera oraz czas okrążenia.

W zależności od typu sesji ostatnia kolumna pokazuje:

- w `Race` — czas poprzedniego okrążenia oraz rzeczywiste różnice czasowe na torze,
- w `Practice` i `Qualify` — najlepsze okrążenie każdego kierowcy oraz różnice względem najlepszego czasu w tabeli.

W sesjach innych niż race wartości `INT` i `LEADER` są porównaniem best lapów, a nie stratą wynikającą z różnicy pozycji na torze. Dane są budowane z całej aktualnej tabeli sesji, również gdy practice jest podzielone na grupy.

Gdy kierowca poprawi swój best lap, jego komórka czasu pulsuje na zielono. Zielony kolor utrzymuje się przez około 10 sekund, a następnie wygasa przez około 3 sekundy.

Czcionka tabeli jest ustawiona na stałe. Zmiana szerokości widgetu poszerza kolumny proporcjonalnie, natomiast zmiana wysokości może spowodować obcięcie dalszych wierszy.

### Pit Speed Helper

Pit Speed Helper pomaga kontrolować prędkość podczas zjazdu do alei serwisowej. Widget korzysta z:

- aktualnej prędkości auta,
- limitu prędkości pit lane,
- flagi OnPitRoad,
- lokalizacji auta AproachingPits z iRacing SDK.

Widget działa w trzech trybach.

#### 1. pre-pitlane

Tryb aktywuje się, gdy iRacing zgłasza AproachingPits, czyli jeszcze przed właściwym wjazdem do pit lane.

Pasek jest kalibrowany w momencie rozpoczęcia hamowania. Za rozpoczęcie hamowania uznawane są trzy kolejne próbki telemetrii ze spadkiem prędkości. Najwyższa prędkość zarejestrowana przed tym momentem staje się górnym zakresem paska.

- 0% oznacza limit pit lane,
- 100% oznacza prędkość auta w momencie rozpoczęcia hamowania,
- pasek pokazuje, ile z tego skalibrowanego zakresu nadal pozostało,
- pasek jest czerwony, aby wyraźnie sygnalizować konieczność hamowania.

Tryb ma prosty filtr szumu oparty na kolejnych malejących próbkach, dzięki czemu pojedyncze wahanie prędkości nie uruchamia kalibracji.

#### 2. inPitsPrePitstop

Tryb uruchamia się, gdy jednocześnie:

1. aktywna jest flaga OnPitRoad,
2. auto osiągnęło prędkość niższą od limitu pit lane.

Jest to powiększenie na okolice limitu, przydatne szczególnie na torach ovalowych i w samochodach bez pit limitera.

- 0% oznacza limit - 10 km/h,
- 50% oznacza dokładnie limit pit lane,
- 100% oznacza limit + 10 km/h,
- dolna połowa paska jest zielona,
- górna połowa paska jest czerwona,
- pozioma linia na środku wskazuje dokładny limit.

Po wyjściu z trybu postoju widget wraca do tego trybu, gdy prędkość przekroczy limit - 10 km/h, ale tylko dopóki OnPitRoad pozostaje aktywne.

#### 3. afterPitstop

Tryb aktywuje się, gdy auto zatrzyma się na OnPitRoad, np. podczas wymiany opon.

- 0% oznacza 0 km/h,
- 100% oznacza limit pit lane,
- pasek jest w całości zielony,
- po przekroczeniu prędkości limit - 10 km/h widget wraca do trybu inPitsPrePitstop.

Po zmianie OnPitRoad na false cały widget jest ukrywany. Pozostające przez chwilę AproachingPits nie uruchamia go ponownie — kolejny cykl zaczyna się dopiero po nowym przejściu tej flagi z false na true.

## Konfigurator

Główne okno WPF służy do wyboru widgetów i sterowania overlayem.

### Start i zatrzymanie overlayu

Po kliknięciu Start overlay:

- zapisywany jest wybór aktywnych widgetów,
- tworzony jest jeden pełnoekranowy overlay WPF,
- overlay automatycznie wybiera monitor, na którym znajduje się konfigurator,
- WebView2 wyświetla wszystkie włączone widgety w jednym układzie,
- konfigurator pozostaje normalnym oknem i nie jest wymuszany nad iRacingiem.

Stop overlay zamyka okno overlayu i zwalnia połączenie telemetryczne.

### Edit mode

Przycisk Edit mode uruchamia natywną warstwę edycji WPF:

- konfigurator zostaje tymczasowo ustawiony jako always on top,
- WebView2 jest ukrywany na czas edycji,
- aktywne widgety są reprezentowane przez niebieskie prostokąty WPF,
- każdy widget można przeciągać po całym monitorze,
- prawy dolny uchwyt służy do zmiany rozmiaru,
- widgety można układać niezależnie od siebie,
- rozmiar i pozycja są ograniczane do granic monitora.

Po wyłączeniu Edit mode:

- pozycje i rozmiary wszystkich aktywnych widgetów są zatwierdzane,
- konfigurator przestaje być always on top,
- WebView2 wraca na ekran,
- React otrzymuje nową konfigurację i renderuje widgety w zapisanych miejscach.

Tryb edycji jest więc tylko natywną warstwą pomocniczą. Finalny overlay nadal składa się z jednego okna WebView2.

## Wygląd widgetów

Wspólny styling frontendu znajduje się w `src/GonzoOverlay.UI/src/styles.css`.

Ogólny wygląd widgetów jest sterowany zmiennymi CSS w sekcji `:root`:

```css
--widget-background
--widget-border
--widget-shadow
--widget-radius
```

Zmiana tych wartości wpływa na ciemne tło, obramowanie, cień i zaokrąglenie wszystkich obecnych widgetów. Style specyficzne dla danego widgetu, takie jak kolory paska Pit Speed Helper albo szerokości kolumn Standings, pozostają w osobnych klasach.

## Zapisywanie ustawień

Ustawienia są przechowywane w:

~~~text
%AppData%\GonzoOverlay\settings.json
~~~

Zapisywane są między innymi:

- włączenie lub wyłączenie widgetu,
- pozycja widgetu względem wybranego monitora,
- szerokość widgetu,
- wysokość widgetu.

Przy uruchomieniu aplikacja uzupełnia brakujące wpisy domyślnymi ustawieniami widgetów, dzięki czemu starszy plik konfiguracyjny pozostaje kompatybilny po dodaniu nowego widgetu.

## Architektura

~~~text
src/
├── GonzoOverlay.App/                     # WPF, konfigurator i host WebView2
├── GonzoOverlay.Core/                    # wspólne modele i kontrakty
├── GonzoOverlay.Infrastructure.iRacing/ # adapter SDK iRacing
├── GonzoOverlay.TelemetryConsole/       # narzędzie do testów live telemetry
└── GonzoOverlay.UI/                     # React + TypeScript + Vite
~~~

Przepływ danych wygląda następująco:

~~~text
iRacing SDK
    ↓
IracingTelemetryProvider
    ├── TelemetrySnapshot
    ├── WeatherSnapshot
    └── StandingsSnapshot
    ↓
OverlayWindow WPF
    ↓ WebView2 postMessage
React App
    ↓
WidgetRegistry → WidgetSlot → konkretny widget
~~~

WidgetRegistry jest punktem wejścia dla widgetów po stronie Reacta. Konfiguracja zawiera listę widgetów wraz z ich identyfikatorem, statusem aktywności, pozycją i rozmiarem. WidgetSlot odpowiada za umieszczenie widgetu w zapisanym miejscu, a sam widget zajmuje się prezentacją i własną logiką.

Po stronie iRacing `IracingTelemetryProvider` publikuje osobne aktualizacje telemetrii, pogody i standings. `OverlayWindow` tłumaczy je na komunikaty WebView2, a React aktualizuje tylko odpowiednie widgety.

## Dodawanie kolejnego widgetu

Typowy proces dodania widgetu:

1. Dodać identyfikator i domyślne ustawienia w modelach WPF.
2. Dodać checkbox widgetu w konfiguratorze oraz obsługę zapisu jego stanu.
3. Utworzyć komponent React w src/GonzoOverlay.UI/src/widgets/.
4. Zarejestrować komponent w WidgetRegistry.tsx.
5. Dodać potrzebne dane do modelu telemetrii i mapowania SDK, jeśli widget korzysta z nowych wartości.
6. Dodać style widgetu w styles.css.
7. Dodać tytuł widgetu do natywnej warstwy Edit mode.

Dzięki temu nowy widget automatycznie korzysta ze wspólnego pozycjonowania, skalowania, konfiguracji i trybu edycji wielu widgetów.

## Wymagania

- Windows 10 lub Windows 11,
- .NET SDK 8.0 lub nowszy,
- Node.js i npm,
- zainstalowany iRacing,
- aktywna sesja iRacing udostępniająca dane telemetryczne,
- WebView2 Runtime.

## Uruchomienie overlaya

Z katalogu głównego repozytorium:

~~~powershell
cd .\src\GonzoOverlay.UI
npm install
npm run build
cd ..\..
dotnet run --project .\src\GonzoOverlay.App\GonzoOverlay.App.csproj
~~~

Build Reacta trafia do katalogu dist, a następnie jest kopiowany do katalogu wyjściowego aplikacji WPF. Jeżeli frontend nie został zbudowany, host WPF pokaże stronę informacyjną zamiast właściwego UI.

W trybie deweloperskim frontend można uruchomić przez:

~~~powershell
cd .\src\GonzoOverlay.UI
npm run dev
~~~

## Narzędzie konsolowe telemetrii

Do testowania połączenia z iRacing można użyć osobnego programu:

~~~powershell
dotnet restore .\GonzoOverlay.sln
dotnet build .\GonzoOverlay.sln
dotnet run --project .\src\GonzoOverlay.TelemetryConsole\GonzoOverlay.TelemetryConsole.csproj
~~~

Konsola pokazuje między innymi:

- prędkość w km/h oraz wartość źródłową w m/s,
- limit prędkości pit lane,
- OnPitRoad,
- lokalizację AproachingPits,
- obroty silnika,
- aktualny bieg,
- poziom paliwa.

Program czeka na połączenie z iRacing, automatycznie ponawia monitorowanie po rozłączeniu i kończy pracę po Ctrl+C.

## Test z iRacing

1. Uruchom iRacing i rozpocznij test drive albo sesję.
2. Uruchom GonzoOverlay.
3. W konfiguratorze wybierz widgety, które mają być widoczne.
4. Kliknij Start overlay.
5. W razie potrzeby uruchom Edit mode i ustaw pozycje oraz rozmiary widgetów.
6. Wyłącz Edit mode, aby zapisać układ.
7. Przetestuj widgety podczas jazdy i obserwuj aktualizacje telemetrii.

Jeżeli aplikacja długo pokazuje Waiting for iRacing, sprawdź, czy iRacing działa na tym samym komputerze i czy aktywna jest sesja udostępniająca SDK telemetryczne. Odtwarzanie plików IBT nie jest obecnie obsługiwane.

## SDK iRacing

Projekt korzysta z pakietu SVappsLAB.iRacingTelemetrySDK w wersji 2.3.0. Lista wymaganych zmiennych telemetrii jest deklarowana przez RequiredTelemetryVars, a TelemetryData jest generowany przez SDK. Nie należy definiować tego typu ręcznie.
