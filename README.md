# LogViewer

LogViewer is a high performance realtime log viewer for **.NET Framework 4.8**. It receives Chainsaw / NLogViewer / log4j XML over UDP, imports text files and archives, and stays usable on a large in-memory buffer.

## Download

<a href="https://sourceforge.net/projects/styort-logviewer/files/latest/download"><img alt="Download LogViewer" src="https://a.fsdn.com/con/app/sf-download-button" width=276 height=48 srcset="https://a.fsdn.com/con/app/sf-download-button?button_size=2x 2x"></a>

Overall downloads on sourceforge: <a href="https://sourceforge.net/projects/styort-logviewer/files/latest/download"><img alt="Download Log Viewer (Log4j, NLog)" src="https://img.shields.io/sourceforge/dt/styort-logviewer.svg" ></a>

Current ClickOnce line: **1.2.8.x**. Settings: `%Documents%\LogViewer\settings.xml` (fallback: application folder). UI: Russian and English.

## Features

 * UDP receivers (Chainsaw / NLog / Log4Net / log4j XML), several ports at once, ignored IPs.
 * Import `.txt` / `.log` by template (including NLog layout), drag-and-drop, `.zip` / `.rar` (nested folders).
 * Tail import of a large file: whole file, last N MB, or last N hours relative to the last record; live follow of imported files.
 * Logger tree with show / don’t show / don’t receive; min log level; time interval.
 * Search (plain text or regex, match case, whole word, optional level) across message, logger, address, thread, executable, throwable, and MDC properties; highlight in the message column.
 * Bookmarks with a comment, jump list, and Ctrl+B.
 * Logger statistics window; Warn/Error/Fatal density strip above the list.
 * Copy selected rows (Ctrl+C) and export the **currently visible** filtered list to `.txt` (Ctrl+S).
 * Save / open a session file (buffer, bookmarks, logger tree, Don't Receive). Chosen with Save As / Open — not auto-written to Documents. Not a filter preset.
 * Themes, font family/size for the list and details pane, show/hide source and thread columns.

## Hotkeys

 * **Ctrl+F** — Search (filter the main list)
 * **Ctrl+Shift+F** — Search results in another window (double-click a row to highlight it in the main window)
 * **Shift+F** — Find next match
 * **Shift+D** — Find previous match
 * **Shift+R** — Clear search text and search result
 * **Ctrl+R** — Clear all logs
 * **Ctrl+S** — Export visible logs to a file
 * **Ctrl+Shift+S** — Save session
 * **Ctrl+O** — Open session
 * **Ctrl+W** — Next warning
 * **Ctrl+E** — Next error
 * **Ctrl+T** — Go to timestamp
 * **Ctrl+Shift+T** — Set time interval
 * **Ctrl+B** — Toggle bookmark on the selected row(s)
 * **Ctrl+C** — Copy selected log line(s)

## Development

Solution: `src/LogViewer.sln` — WPF app, `LogViewer.Core` (no UI), `LogViewer.Core.Tests` (NUnit, parsers/filter/Don’t Receive/batcher). Tests do not cover WPF, ClickOnce, or Dispatcher.

```
dotnet test src/LogViewer.Core.Tests/LogViewer.Core.Tests.csproj
```

## Screenshots
Main window <br>
![Loading...](https://github.com/Styort/LogViewer/blob/master/docs/1-main.png?raw=true) 

![Loading...](https://github.com/Styort/LogViewer/blob/master/docs/4-main-searching.png?raw=true)

Dialogs <br>
![Loading...](https://github.com/Styort/LogViewer/blob/master/docs/2-log-dialog.png?raw=true)

![Loading...](https://github.com/Styort/LogViewer/blob/master/docs/3-logger-dialog.png?raw=true)

Settings <br>
![Loading...](https://github.com/Styort/LogViewer/blob/master/docs/5-settings-main.png?raw=true) 

![Loading...](https://github.com/Styort/LogViewer/blob/master/docs/6-settings-receivers.png?raw=true)

![Loading...](https://github.com/Styort/LogViewer/blob/master/docs/7-settings-ignored-ips.png?raw=true)
