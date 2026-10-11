# Minecraft Clicker

Autoclicker dla Windowsa napisany w C# i WinForms. Umożliwia niezależne automatyczne klikanie lewym i prawym przyciskiem myszy.

## Funkcje

- **LPM i PPM** — niezależne autoclickery.
- **Regulacja CPS** — od 1 do 150 kliknięć na sekundę.
- **Globalne skróty klawiszowe** — własne klawisze dla LPM, PPM i awaryjnego STOP.
- **Zapisywanie ustawień** — wartości CPS i skróty są zapamiętywane po ponownym uruchomieniu.
- **Ciemny interfejs** — prosty i czytelny wygląd.
- **Samodzielny plik EXE** — bez konieczności instalowania .NET na komputerze docelowym.

## Sterowanie

Domyślne skróty:

| Funkcja | Klawisz |
|---|---|
| LPM | `F6` |
| PPM | `F7` |
| STOP | `F8` |

Klawisze można zmienić w aplikacji.

## Pobieranie

1. Otwórz zakładkę **Releases** w repozytorium.
2. Pobierz `MinecraftClicker.exe` z najnowszego wydania.
3. Uruchom aplikację.

Nie trzeba pobierać kodu źródłowego ani instalować dodatkowych zależności.

## Ustawienia

Konfiguracja aplikacji jest zapisywana w:

`%AppData%\MinecraftClicker\settings.json`

Plik ustawień nie jest tworzony obok pliku EXE.

## Technologie

- C#
- .NET 9
- Windows Forms

## Uwagi

Rzeczywista szybkość klikania zależy od systemu Windows i aplikacji docelowej. Korzystaj z autoclickera zgodnie z zasadami serwera lub gry.

## Licencja

Jeśli chcesz zezwolić innym na używanie, modyfikowanie i rozpowszechnianie projektu, dodaj do repozytorium plik `LICENSE` z wybraną licencją.
