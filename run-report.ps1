param(
    [int]$Days = 7
)

# Determine script directory
$scriptDir = Split-Path -Path $PSCommandPath -Parent

# Run the JiraDiffReporter
Push-Location $scriptDir
& dotnet run -- -d $Days
Pop-Location

# Path to generated HTML
$htmlPath = Join-Path $scriptDir 'JiraAenderungen.html'

# Load recipients
$recipientsFile = Join-Path $scriptDir 'recipients.txt'
if (-Not (Test-Path $recipientsFile)) {
    throw "Recipients file not found: $recipientsFile"
}
$recipients = (
    Get-Content $recipientsFile |
    ForEach-Object { $_.Trim() } |
    Where-Object     { $_ -ne '' }
) -join ';'

# Create Outlook email with attachment
$outlook = New-Object -ComObject Outlook.Application
$mail    = $outlook.CreateItem(0)

# Add each recipient explicitly
$recipients -split ';' | ForEach-Object {
    if ($_ -match '\S') {
        $r = $mail.Recipients.Add($_)
        $r.Type = 1   # 1 = olTo
    }
}
$mail.Recipients.ResolveAll()

# Inject local date & time into the subject
$timestamp = Get-Date -Format "dd.MM.yyyy, HH:mm"
$mail.Subject  = "Jira Änderungsbericht ($timestamp)"

# Load the HTML body and attach
$mail.HTMLBody = Get-Content -Path $htmlPath -Raw
$mail.Attachments.Add($htmlPath)
$mail.Display()
