param([string]$Job='UI',[string]$Method='SystemMenuValidation.Run', [int]$Seconds=360)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskBusy = @(Get-Process -Name Unity,DonDon,'Stock Whale Simulator' -ErrorAction SilentlyContinue)
if ($taskBusy.Count) { throw 'A Unity or game process is already running.' }
$taskOut = Join-Path $taskRoot ('Validation/Settings/' + $Job)
New-Item -ItemType Directory -Path $taskOut -Force | Out-Null
$taskSelf = [Diagnostics.Process]::GetCurrentProcess()
$taskSelf.ProcessorAffinity = [IntPtr]1
$taskSelf.PriorityClass = 'BelowNormal'
$taskArgs = @('-batchmode','-job-worker-count','1','-assetImportWorkerCount','1','-projectPath',('"'+$taskRoot+'"'),'-logFile',('"'+$taskOut+'/unity.log"'))
if ($Method) { $taskArgs += @('-force-d3d11','-executeMethod',$Method) } else { $taskArgs += @('-nographics','-quit') }
$env:DONDON_SETTINGS_OUTPUT=$taskOut
$taskStart=Get-Date
$taskPeak=0L
$taskUnity=Start-Process -FilePath 'C:/Program Files/Unity/6000.5.5f1/Editor/Unity.exe' -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
$taskUnity.ProcessorAffinity=[IntPtr]1
$taskUnity.PriorityClass='BelowNormal'
try {
    while (!$taskUnity.HasExited) {
        Start-Sleep -Seconds 1
        $taskUnity.Refresh()
        if ($taskUnity.HasExited) { break }
        $taskPeak=[Math]::Max($taskPeak,$taskUnity.WorkingSet64)
        $taskElapsed=((Get-Date)-$taskStart).TotalSeconds
        if ($taskElapsed -gt $Seconds -or $taskUnity.WorkingSet64 -gt 4GB) {
            [pscustomobject]@{exitCode=-1;peakMB=[Math]::Round($taskPeak/1MB);seconds=[Math]::Round($taskElapsed,1);affinity=1;budgetStopped=$true} | ConvertTo-Json | Set-Content (Join-Path $taskOut 'resources.json')
            Stop-Process -Id $taskUnity.Id -Force
            throw 'Unity resource budget reached.'
        }
    }
    $taskUnity.WaitForExit()
    [pscustomobject]@{exitCode=$taskUnity.ExitCode;peakMB=[Math]::Round($taskPeak/1MB);seconds=[Math]::Round(((Get-Date)-$taskStart).TotalSeconds,1);affinity=1} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $taskOut 'resources.json')
    exit $taskUnity.ExitCode
} finally {
    if (!$taskUnity.HasExited) { Stop-Process -Id $taskUnity.Id -Force }
    Remove-Item Env:DONDON_SETTINGS_OUTPUT -ErrorAction SilentlyContinue
}

