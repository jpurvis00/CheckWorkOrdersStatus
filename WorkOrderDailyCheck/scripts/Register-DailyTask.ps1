<#
.SYNOPSIS
  Registers a Windows scheduled task that runs WorkOrderDailyCheck every day at 6:00 PM.

.DESCRIPTION
  Run this once, in an elevated PowerShell, on a Windows PC or server that can reach the
  Oracle ERP database, the P: share and your mail server. The time is the machine's local
  time, so set the machine to Eastern time (or change -At).

  The task runs whether or not you are logged on. A mapped drive letter such as P: is NOT
  available in that case, so set Excel:Path in appsettings.json to the UNC path
  (\\server\share\jeff\CheckStatus\...). Run "net use" to see what P: points to.

.EXAMPLE
  .\Register-DailyTask.ps1 -ExePath "C:\Tools\WorkOrderDailyCheck\WorkOrderDailyCheck.exe"
#>
param(
    [Parameter(Mandatory = $true)] [string] $ExePath,
    [string] $TaskName = "Work Order Daily Check",
    [string] $At = "6:00PM",
    [int] $Days = 1
)

$ErrorActionPreference = "Stop"
$ExePath = (Resolve-Path $ExePath).Path
$workDir = Split-Path $ExePath

$action   = New-ScheduledTaskAction -Execute $ExePath -Argument "--days $Days" -WorkingDirectory $workDir
$trigger  = New-ScheduledTaskTrigger -Daily -At $At
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
                -RestartCount 2 -RestartInterval (New-TimeSpan -Minutes 15)

# Runs as you, whether or not you are logged on. Windows asks for your password once.
$cred = Get-Credential -UserName "$env:USERDOMAIN\$env:USERNAME" -Message "Account the task runs as (needs access to the share)"
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings `
    -User $cred.UserName -Password $cred.GetNetworkCredential().Password -RunLevel Limited -Force | Out-Null

Write-Host "Registered '$TaskName' to run daily at $At."
Write-Host "Test it now with:  Start-ScheduledTask -TaskName '$TaskName'"
