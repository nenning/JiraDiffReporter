Konfiguration:
Jql im appsettings.json

Und folgende User Secrets:

dotnet restore
cd JiraDiffReporter
dotnet user-secrets init
dotnet user-secrets set "Jira:BaseUrl"   "https://yourcompany.atlassian.net"
dotnet user-secrets set "Jira:Email"     "you@company.com"
dotnet user-secrets set "Jira:ApiToken"  "YOUR_API_TOKEN_HERE"