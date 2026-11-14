param([string]$Case='baseline',[int]$Seed=73129)
$ErrorActionPreference='Stop'
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Another Unity instance is already running.' }
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=Join-Path $taskRoot ('docs/chat-social-results/stage-7/'+$Case+'-'+$Seed)
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskHash=(Get-FileHash -LiteralPath (Join-Path $taskRoot 'Assets/Scenes 1/TradeScene.unity')).Hash
$taskSelf=[Diagnostics.Process]::GetCurrentProcess();$taskSelf.ProcessorAffinity=[IntPtr]1;$taskSelf.PriorityClass='BelowNormal'
$env:DONDON_CHAT_MARKET_CASE=$Case;$env:DONDON_CHAT_MARKET_SEED="$Seed"
$taskUnity=Start-Process -FilePath 'C:/Program Files/Unity/6000.5.5f1/Editor/Unity.exe' -ArgumentList @('-batchmode','-nographics','-job-worker-count','1','-assetImportWorkerCount','1','-projectPath',('"'+$taskRoot+'"'),'-executeMethod','ChatSocialMarketValidation.Run','-logFile',('"'+$taskOutput+'/unity.log"')) -WindowStyle Hidden -PassThru
$taskUnity.ProcessorAffinity=[IntPtr]1;$taskUnity.PriorityClass='BelowNormal'
$taskStart=Get-Date;$taskPeak=0L
try {
 while (!$taskUnity.HasExited) {
  Start-Sleep -Seconds 1
  $taskUnity.Refresh()
  if ($taskUnity.HasExited) { break }
  $taskPeak=[Math]::Max($taskPeak,$taskUnity.WorkingSet64)
  if (((Get-Date)-$taskStart).TotalSeconds -gt 960 -or $taskUnity.WorkingSet64 -gt 4GB) { Stop-Process -Id $taskUnity.Id -Force; throw 'Validation stopped at resource budget.' }
 }
 $taskUnity.WaitForExit()
 if ((Get-FileHash -LiteralPath (Join-Path $taskRoot 'Assets/Scenes 1/TradeScene.unity')).Hash -ne $taskHash) { throw 'Scene hash changed.' }
 [pscustomobject]@{affinity=1;priority='BelowNormal';peakMB=[Math]::Round($taskPeak/1MB);wallSeconds=[Math]::Round(((Get-Date)-$taskStart).TotalSeconds,1);exitCode=$taskUnity.ExitCode;sceneHash=$taskHash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'resources.json')
 Get-Content -LiteralPath (Join-Path $taskOutput 'result.txt') -ErrorAction SilentlyContinue
 exit $taskUnity.ExitCode
} finally { if (!$taskUnity.HasExited) { Stop-Process -Id $taskUnity.Id -Force } }
