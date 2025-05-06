# Unregister scheduled task
$schtaskName = 'JiraReportWeekly'
if (Get-ScheduledTask -TaskName $schtaskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $schtaskName -Confirm:$false
    Write-Output "✅ Scheduled task '$schtaskName' removed."
} else {
    Write-Output "⚠ No scheduled task found with name '$schtaskName'."
}