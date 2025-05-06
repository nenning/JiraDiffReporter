run-report.ps1
Report erzeugen und Mail mit Attachment öffnen

(un)register-scheduled-report.ps1
Report als Windows Task anlegen oder löschen. Als Admin ausführen!


*Konfiguration:*
Kommandozeilen Parameter: -d <n> oder -days <n>. Z.B. -d 21. Default: 7.
Berücksichtige Änderungen in den letzten n Tagen.

Jql im appsettings.json

Und folgende User Secrets:

dotnet restore
cd JiraDiffReporter
dotnet user-secrets init
dotnet user-secrets set "Jira:BaseUrl"   "https://yourcompany.atlassian.net"
dotnet user-secrets set "Jira:Email"     "you@company.com"
dotnet user-secrets set "Jira:ApiToken"  "YOUR_API_TOKEN_HERE"