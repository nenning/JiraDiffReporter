# Register scheduled task to run weekly on Tuesday at 9 AM
$scriptDir = Split-Path -Path $PSCommandPath -Parent
$schtaskName = 'JiraReportWeekly'
$action = "powershell.exe -ExecutionPolicy Bypass -File `"$scriptDir\run-report.ps1`" -Days 7"

# Create or update the scheduled task
if (Get-ScheduledTask -TaskName $schtaskName -ErrorAction SilentlyContinue) {
    Set-ScheduledTask -TaskName $schtaskName -Action (New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-ExecutionPolicy Bypass -File `"$scriptDir\run-report.ps1`" -Days 7")
    Write-Output "🔄 Scheduled task '$schtaskName' updated."
} else {
    Register-ScheduledTask -Action (New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-ExecutionPolicy Bypass -File `"$scriptDir\run-report.ps1`" -Days 7") `
                          -Trigger (New-ScheduledTaskTrigger -Weekly -DaysOfWeek Tuesday -At 9am) `
                          -TaskName $schtaskName `
                          -Description "Runs Jira diff report weekly" `
                          -User (whoami) `
                          -RunLevel Highest
    Write-Output "✅ Scheduled task '$schtaskName' registered."
}