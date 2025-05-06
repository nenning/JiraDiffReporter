## Übersicht

Dieses Repository enthält eine .NET 9.0 Konsolenanwendung **JiraDiffReporter**, die wöchentlich Änderungen an Jira-Tickets (Summaries und Beschreibungen) erfasst und einen farbig formatierten HTML-Bericht erstellt. 
Dazu gibt es PowerShell- und Bash-Skripte, um den Report auszuführen, eine E-Mail zu öffnen und optional per Windows Task Scheduler automatisch jede Woche Dienstag 09:00 Uhr auszuführen.

---

## Inhalte
* **Program.cs**: Hauptprogramm, das:

  * JQL aus `appsettings.json` liest
  * Jira Cloud API per Basic Auth (User Secrets) abfragt
  * Änderungen der letzten N Tage (default 7) sammelt
  * HTML-Diff-Bericht (`JiraAenderungen.html`) erzeugt
* **appsettings.json**: Enthält die konfigurierbare JQL-Abfrage
* **PowerShell-Skripte**:

  * `run-report.ps1`: Führt den Report aus und öffnet eine Outlook-E-Mail mit dem HTML-Anhang
  * `register-report.ps1`: Registriert in Task Scheduler (jede Woche Di 09:00) **(Admin-Rechte erforderlich)**
  * `unregister-report.ps1`: Entfernt den geplanten Task **(Admin-Rechte erforderlich)**

---

## Voraussetzungen

* [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
* Windows mit Outlook für PowerShell-Integration
* Jira Cloud Account mit API-Token

---

## Einrichtung

1. Repository klonen:

   ```bash
   ```

git clone [https://github.com/nenning/JiraDiffReporter.git](https://github.com/nenning/JiraDiffReporter.git) cd JiraDiffReporter

````
2. `appsettings.json` erstellen und JQL anpassen (wird ignoriert):
   ```json
   {
     "Jira": {
       "Jql": "parentEpic=edu-85 AND project IN (EDU) ORDER BY key ASC"
     }
   }
````

3. User Secrets initialisieren und Jira-Zugangsdaten setzen:

   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "Jira:BaseUrl" "https://yourcompany.atlassian.net"
   dotnet user-secrets set "Jira:Email" "you@company.com"
   dotnet user-secrets set "Jira:ApiToken" "YOUR_API_TOKEN_HERE"
   ```
4. Abhängigkeiten installieren:

   ```bash
   dotnet restore
   ```

---

## Ausführung

### PowerShell (Windows)

```powershell
.\\run-report.ps1
```

Der generierte Bericht liegt als `JiraAenderungen.html` im gleichen Verzeichnis.

### Kommandozeilen-Parameter für die Applikation

* `-d <Anzahl>` oder `--days <Anzahl>`: Anzahl der Tage, die im Report berücksichtigt werden (Standard: 7).

---

## Automatische Planung (Windows Task Scheduler)

**Hinweis**: Registrierung und Entfernung erfordern Administratorrechte.

* **Task registrieren**:

  ```powershell
  .\register-report.ps1
  ```
* **Task entfernen**:

  ```powershell
  .\unregister-report.ps1
  ```

Der Task `JiraReportWeekly` läuft dann jeden Dienstag um 09:00 Uhr.
