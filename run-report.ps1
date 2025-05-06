param(
    [int]$Days = 7
)

# Determine script directory
$scriptDir = Split-Path -Path $PSCommandPath -Parent

# Run the JiraDiffReporter executable from script directory
Push-Location $scriptDir
& dotnet run -- -d $Days
Pop-Location

# Path to generated HTML
$htmlPath = Join-Path $scriptDir 'JiraAenderungen.html'

# Create Outlook email with attachment
$outlook = New-Object -ComObject Outlook.Application
$mail    = $outlook.CreateItem(0)
$mail.Subject  = "Jira Änderungsbericht"
$mail.HTMLBody = Get-Content -Path $htmlPath -Raw
$mail.Attachments.Add($htmlPath)
$mail.Display()