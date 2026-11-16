param([string]$Run = 'run-01', [ValidateRange(0,300)][int]$SmokeSeconds = 0)
$ErrorActionPreference = 'Stop'
if ($Run -notmatch '^run-[0-9]+$') { throw 'Use a simple run-NN label.' }
if (Get-Process -Name Unity,dotnet,DonDon -ErrorAction SilentlyContinue) { throw 'Another editor or validation process is running.' }
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOut = Join-Path $taskRoot ('Validation/AutonomousChat/' + $Run)
if (Test-Path -LiteralPath $taskOut) { throw 'Preserve prior evidence: select a new run label.' }
New-Item -ItemType Directory -Path $taskOut -Force | Out-Null
$taskScene = Join-Path $taskRoot 'Assets/Scenes 1/TradeScene.unity'
$taskInputs = @($taskScene) + @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'Assets/Resources/Chat') -File | Where-Object { $_.Extension -eq '.json' } | ForEach-Object { $_.FullName })
$taskHashes = @($taskInputs | ForEach-Object { [pscustomobject]@{path=$_; hash=(Get-FileHash -LiteralPath $_).Hash} })
$taskHashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOut 'inputs-before.json') -Encoding utf8
$taskSelf = [Diagnostics.Process]::GetCurrentProcess()
$taskSelf.ProcessorAffinity = [IntPtr]1
$taskSelf.PriorityClass = 'BelowNormal'
$env:DONDON_AUTONOMOUS_CHAT_OUTPUT = $taskOut
$env:DONDON_CHAT_SMOKE_SECONDS = [string]$SmokeSeconds
$taskStart = Get-Date
$taskPeak = 0L
$taskCpu = 0.0
$taskNextHeartbeat = 0.0
$taskUnity = Start-Process -FilePath 'C:/Program Files/Unity/6000.5.5f1/Editor/Unity.exe' -ArgumentList @('-batchmode','-nographics','-job-worker-count','1','-assetImportWorkerCount','1','-projectPath',('"' + $taskRoot + '"'),'-executeMethod','AutonomousChatObservation.Run','-logFile',('"' + $taskOut + '/unity.log"')) -WindowStyle Hidden -PassThru
$taskUnity.ProcessorAffinity = [IntPtr]1
$taskUnity.PriorityClass = 'BelowNormal'
try {
    while (!$taskUnity.HasExited) {
        Start-Sleep -Seconds 1
        $taskUnity.Refresh()
        if ($taskUnity.HasExited) { break }
        $taskPeak = [Math]::Max($taskPeak,$taskUnity.WorkingSet64)
        $taskCpu = $taskUnity.TotalProcessorTime.TotalSeconds
        $taskElapsed = ((Get-Date) - $taskStart).TotalSeconds
        if ($taskElapsed -ge $taskNextHeartbeat) {
            $taskNextHeartbeat = $taskElapsed + 5
            [pscustomobject]@{pid=$taskUnity.Id;affinity=1;priority=$taskUnity.PriorityClass.ToString();memoryMB=[Math]::Round($taskUnity.WorkingSet64/1MB);cpuSeconds=$taskCpu;wallSeconds=[Math]::Round($taskElapsed,1)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOut 'heartbeat.json') -Encoding utf8
        }
        $taskStartupBlocked = $taskElapsed -gt 240 -and !(Test-Path -LiteralPath (Join-Path $taskOut 'status.json'))
        if ($taskElapsed -gt 1380 -or $taskUnity.WorkingSet64 -gt 4GB -or $taskStartupBlocked -or (Test-Path -LiteralPath (Join-Path $taskOut 'stop-requested'))) {
            Stop-Process -Id $taskUnity.Id -Force
            throw 'Observation stopped at the CPU/memory/time resource budget.'
        }
    }
    $taskUnity.WaitForExit()
    $taskUnchanged = $true
    foreach ($taskInput in $taskHashes) {
        if ((Get-FileHash -LiteralPath $taskInput.path).Hash -ne $taskInput.hash) { $taskUnchanged = $false }
    }
    [pscustomobject]@{affinity=1;priority='BelowNormal';frameRate=20;timeScale=1;fixedStepAcceleration=$false;peakMB=[Math]::Round($taskPeak/1MB);cpuSeconds=$taskCpu;wallSeconds=[Math]::Round(((Get-Date)-$taskStart).TotalSeconds,1);exitCode=$taskUnity.ExitCode;sceneAndChatSettingsUnchanged=$taskUnchanged} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOut 'resources.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $taskOut 'status.json') -ErrorAction SilentlyContinue
    if (!$taskUnchanged) { throw 'Scene or chat settings changed during observation.' }
    exit $taskUnity.ExitCode
} finally {
    if (!$taskUnity.HasExited) { Stop-Process -Id $taskUnity.Id -Force }
    Remove-Item Env:DONDON_AUTONOMOUS_CHAT_OUTPUT -ErrorAction SilentlyContinue
    Remove-Item Env:DONDON_CHAT_SMOKE_SECONDS -ErrorAction SilentlyContinue
}
