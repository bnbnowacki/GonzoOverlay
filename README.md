# GonzoOverlay

GonzoOverlay to konfigurowalny overlay dla iRacing. Wyświetla najważniejsze informacje bezpośrednio nad obrazem z symulatora, dzięki czemu nie trzeba odrywać wzroku od jazdy.

Overlay składa się z niezależnych widgetów. Każdy widget można włączyć lub wyłączyć, ustawić w dowolnym miejscu ekranu i zmienić jego rozmiar.

## Szybki start

1. Uruchom iRacing i rozpocznij sesję albo test drive.
2. Uruchom GonzoOverlay.
3. Zaznacz widgety, które chcesz widzieć.
4. Kliknij **Start overlay**.
5. Jeśli chcesz zmienić układ, kliknij **Edit mode**.
6. Po ustawieniu widgetów wyłącz **Edit mode**, aby zapisać układ.

Overlay automatycznie wybiera monitor, na którym znajduje się konfigurator.

## Konfigurator

### Start overlay

Przycisk **Start overlay** uruchamia overlay na wybranym monitorze. Konfigurator pozostaje zwykłym oknem i nie zasłania iRacingu ani innych aplikacji.

### Stop overlay

Przycisk **Stop overlay** zamyka overlay. Wszystkie ustawienia pozycji i rozmiarów są zachowywane.

### Edit mode

Tryb edycji służy do ustawiania widgetów na ekranie.

Po jego włączeniu:

- każdy aktywny widget pojawia się jako osobny niebieski prostokąt,
- widget można przeciągnąć w dowolne miejsce monitora,
- prawy dolny róg służy do zmiany rozmiaru,
- wszystkie widgety można układać niezależnie od siebie,
- konfigurator pozostaje na wierzchu, aby można było wygodnie sterować edycją.

Po wyłączeniu trybu edycji układ zostaje zapisany, a widgety wracają do normalnego wyglądu.

## Widgety

### Pogoda toru

Widget pokazuje aktualną wilgotność nawierzchni toru, między innymi:

- Dry — sucho,
- Lightly Wet — lekko mokro,
- Very Wet — bardzo mokro.

Tekst widgetu skaluje się razem z jego rozmiarem. Dzięki temu można ustawić zarówno duży, czytelny panel, jak i bardzo mały widget.

### Pit Speed Helper

Pit Speed Helper pomaga kontrolować prędkość podczas zjazdu do alei serwisowej. Jest szczególnie przydatny na torach ovalowych oraz w samochodach, które nie mają automatycznego ogranicznika prędkości w pit lane.

Widget pokazuje pionowy pasek prędkości i automatycznie przełącza się między trzema trybami.

#### Pre-pitlane — hamowanie przed aleją

Tryb uruchamia się, gdy zjazd do pit lane został rozpoznany, jeszcze przed przejechaniem przez właściwe znaczniki wjazdu.

Pasek kalibruje się w chwili rozpoczęcia hamowania. Pojedyncze wahania prędkości są ignorowane, aby przypadkowy szum nie uruchamiał kalibracji.

- 0% paska oznacza limit prędkości w pit lane,
- 100% oznacza prędkość auta w chwili rozpoczęcia hamowania,
- czerwony pasek pokazuje, że trzeba nadal mocno zwalniać.

#### In pits, pre pitstop — jazda z limitem

Tryb włącza się po faktycznym wjechaniu do pit lane i osiągnięciu prędkości poniżej limitu.

Zakres jest celowo wąski, aby ułatwić precyzyjne utrzymanie prędkości:

- 0% oznacza limit minus 10 km/h,
- połowa paska oznacza dokładnie limit,
- 100% oznacza limit plus 10 km/h,
- dolna część paska jest zielona,
- górna część paska jest czerwona,
- pozioma linia na środku wskazuje limit.

Po postoju widget wraca do tego trybu, gdy auto przekroczy prędkość o 10 km/h poniżej limitu — pod warunkiem, że nadal znajduje się w pit lane.

#### After pitstop — zatrzymanie w stanowisku

Tryb uruchamia się, gdy auto zatrzyma się w pitach, na przykład podczas wymiany opon.

- 0% oznacza 0 km/h,
- 100% oznacza limit prędkości w pit lane,
- cały pasek jest zielony.

Po rozpędzeniu auta powyżej limitu minus 10 km/h widget wraca do trybu jazdy z limitem.

Po opuszczeniu pit lane widget znika. Sam fakt, że samochód przez chwilę nadal znajduje się na drodze prowadzącej do pitów, nie uruchamia go ponownie. Kolejny cykl zaczyna się dopiero przy następnym zjeździe.

### Standings

Standings pokazuje aktualną tabelę kierowców.

Kolumny znajdują się w kolejności:

1. pozycja,
2. nazwisko kierowcy,
3. numer startowy,
4. safety rating,
5. iRating,
6. samochód,
7. INT — odstęp do kierowcy bezpośrednio przed nami,
8. LEADER — strata do lidera,
9. czas okrążenia.

#### Race

W wyścigu ostatnia kolumna pokazuje ostatni ukończony czas okrążenia.

Wartości INT i LEADER pokazują rzeczywiste różnice czasowe na torze.

#### Practice i Qualify

W treningu i kwalifikacjach ostatnia kolumna pokazuje najlepszy czas okrążenia każdego kierowcy.

INT i LEADER porównują wtedy najlepsze czasy okrążeń, a nie odległość wynikającą z aktualnej pozycji na torze. Jeżeli trening jest podzielony na grupy, tabela nadal może pokazywać kierowców ze wszystkich grup.

#### Kolorowanie czasów okrążeń

Komórki czasów reagują na nowe wyniki:

- zwykła aktualizacja czasu miga na zielono,
- nowy osobisty rekord kierowcy zostaje oznaczony na zielono,
- najlepszy czas całej sesji jest oznaczony na fioletowo,
- gdy ktoś pobije fioletowy rekord, poprzedni rekord zmienia się na zielony, a fiolet przechodzi do nowego kierowcy.

Czcionka tabeli ma stały rozmiar. Poszerzenie widgetu rozszerza kolumny, natomiast zmniejszenie wysokości może ukryć dalsze wiersze tabeli.

### Relative

Relative pokazuje samochody znajdujące się najbliżej nas na torze, a nie tylko kierowców znajdujących się obok nas w klasyfikacji.

Widget zawsze ma siedem wierszy:

- trzy samochody przed nami,
- nasz samochód w czwartym wierszu,
- trzy samochody za nami.

Jeśli na torze jest mniej samochodów, brakujące miejsca pozostają puste. Dzięki temu nasz wiersz zawsze znajduje się na środku i tabela nie zmienia wysokości podczas jazdy.

Kolumny pokazują:

- pozycję kierowcy,
- jego nazwisko,
- różnicę czasową względem nas.

Znaczenie znaków:

- wartość dodatnia oznacza samochód przed nami,
- wartość ujemna oznacza samochód za nami,
- wartość jest pokazywana z dokładnością do jednej cyfry po przecinku.

Overlay wybiera krótszą drogę czasową wokół linii mety. Dzięki temu samochód, który właśnie przejechał metę, nie pojawia się nagle prawie całe okrążenie od nas.

Relative nie bierze pod uwagę liczby ukończonych okrążeń. Jeżeli kierowca jest kilka okrążeń za nami, ale fizycznie znajduje się tuż za naszym samochodem, nadal pojawi się jako najbliższy kierowca za nami.

Kierowcy znajdujący się w pit lane są wyszarzeni. Nasze nazwisko jest zawsze złoto-żółte, również wtedy, gdy sami jesteśmy w pitach.

## Układ i wygląd

Wszystkie widgety mają spójny, ciemny wygląd z półprzezroczystym tłem i niebieskim obramowaniem.

Każdy widget ma własną pozycję i rozmiar. Zmiana jednego widgetu nie wpływa na pozostałe. Układ jest zapisywany automatycznie po wyjściu z trybu edycji.

## Zapisywanie ustawień

Zapisywane są między innymi:

- włączone i wyłączone widgety,
- pozycja każdego widgetu,
- szerokość widgetu,
- wysokość widgetu.

Ustawienia zostają zachowane po zamknięciu aplikacji i są ponownie używane przy kolejnym uruchomieniu.

## Rozwiązywanie problemów

### Widgety są puste

Sprawdź, czy:

1. iRacing jest uruchomiony,
2. aktywna jest sesja albo test drive,
3. widget jest zaznaczony w konfiguratorze,
4. overlay został uruchomiony przyciskiem **Start overlay**.

Niektóre informacje pojawiają się dopiero po rozpoczęciu jazdy lub po otrzymaniu pierwszych danych z sesji.

### Overlay pojawia się na złym monitorze

Przenieś konfigurator na właściwy monitor i uruchom overlay ponownie. Overlay wybiera monitor, na którym znajduje się konfigurator w momencie startu.

### Widget zniknął po zmianie rozmiaru

Sprawdź, czy jego obszar nie został zmniejszony albo przesunięty poza ekran. Włącz **Edit mode**, ustaw widget ponownie i wyłącz tryb edycji, aby zapisać zmiany.

## Wymagania

- Windows 10 lub Windows 11,
- zainstalowany iRacing,
- aktywna sesja iRacing albo test drive,
- środowisko umożliwiające wyświetlanie aplikacji webowych w systemie Windows.

GonzoOverlay korzysta z danych udostępnianych przez iRacing podczas aktywnej sesji. Bez uruchomionego iRacingu widgety nie będą miały aktualnych danych do pokazania.
