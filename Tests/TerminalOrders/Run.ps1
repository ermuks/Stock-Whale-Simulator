$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOut = Join-Path $taskRoot 'Validation/TerminalOrders'
New-Item -ItemType Directory -Path $taskOut -Force | Out-Null
$taskProcess = [Diagnostics.Process]::GetCurrentProcess()
$taskProcess.ProcessorAffinity = [IntPtr]1
$taskProcess.PriorityClass = 'BelowNormal'
& (Join-Path $taskRoot 'Tests/ChatSocial/Market/Run.ps1') -CompileOnly
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$taskHost = Get-Content (Join-Path $taskRoot 'Tests/ChatSocial/Market/Host.cs') -Raw
$taskHost = $taskHost.Replace('public sealed class StockManager','public sealed partial class StockManager')
$taskHost = $taskHost.Replace('public static class CandleHistoryManager','public sealed class CandleHistoryManager')
$taskHost = $taskHost.Replace('{ public static int StableSeed','{ public static CandleHistoryManager Instance; public float candleDuration = 5f; public static int StableSeed')
Set-Content (Join-Path $taskOut 'Host.cs') $taskHost -Encoding utf8
$taskRsp = Get-Content (Join-Path $taskRoot 'Validation/ChatSocial/Market/Market.rsp') | ForEach-Object {
    if ($_ -match '^-out:') { '-out:"' + $taskOut + '/TerminalOrders.dll"' }
    elseif ($_.Replace('\','/') -match 'Tests/ChatSocial/Market/Program.cs') { '"' + $PSScriptRoot + '/Program.cs"' }
    elseif ($_.Replace('\','/') -match 'Tests/ChatSocial/Market/Host.cs') { '"' + $taskOut + '/Host.cs"' }
    else { $_ }
}
foreach ($taskSource in @('Assets/Scripts/NightShift/DeviceOrderSession.cs','Assets/Scripts/StockStatistics.cs','Assets/Scripts/CandleData.cs','Tests/TerminalOrders/Host.cs')) {
    $taskRsp += '"' + $taskRoot + '/' + $taskSource + '"'
}
Set-Content (Join-Path $taskOut 'TerminalOrders.rsp') $taskRsp -Encoding utf8
$taskRuntime = 'C:/Program Files/Unity/6000.5.5f1/Editor/Data/NetCoreRuntime/dotnet.exe'
$taskCompiler = 'C:/Program Files/Unity/6000.5.5f1/Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
& $taskRuntime $taskCompiler ('@' + $taskOut + '/TerminalOrders.rsp')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Set-Content (Join-Path $taskOut 'TerminalOrders.runtimeconfig.json') '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.21"}}}' -Encoding utf8
& $taskRuntime (Join-Path $taskOut 'TerminalOrders.dll') $taskRoot | Tee-Object -FilePath (Join-Path $taskOut 'results.txt')
exit $LASTEXITCODE
