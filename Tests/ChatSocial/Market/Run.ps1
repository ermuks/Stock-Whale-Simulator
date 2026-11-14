param([int]$Seed=73129,[int]$Days=1)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$taskOut=Join-Path $taskRoot 'Validation/ChatSocial/Market'
$taskProcess=[Diagnostics.Process]::GetCurrentProcess();$taskProcess.ProcessorAffinity=[IntPtr]1;$taskProcess.PriorityClass='BelowNormal'
& 'C:/Program Files/WindowsApps/OpenAI.CodexPrimaryRuntime.v26-1112-611-0_26.1112.611.0_x64__3k8sg7r9htsxt/dependencies/python/python.exe' (Join-Path $PSScriptRoot 'Prepare.py')
if($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$taskRuntime='C:/Program Files/Unity/6000.5.5f1/Editor/Data/NetCoreRuntime/dotnet.exe'
$taskCompiler='C:/Program Files/Unity/6000.5.5f1/Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
$taskResponse=@('-nologo','-parallel-','-target:exe',('-out:"'+$taskOut+'/Market.dll"'))
Get-ChildItem 'C:/Program Files/Unity/6000.5.5f1/Editor/Data/DotNetSdk/packs/Microsoft.NETCore.App.Ref/8.0.21/ref/net8.0' -Filter '*.dll' | ForEach-Object {$taskResponse+='-r:"'+$_.FullName+'"'}
$taskSources=@('Validation/ChatSocial/Market/HostCommon.cs','Validation/ChatSocial/Market/FillAdapter.cs','Tests/ChatSocial/Market/Host.cs','Tests/ChatSocial/Market/Program.cs',
'Assets/Scripts/Stock.cs','Assets/Scripts/Trader.cs','Assets/Scripts/Order.cs','Assets/Scripts/OrderBook.cs','Assets/Scripts/PriceTickPolicy.cs','Assets/Scripts/EconomySettings.cs',
'Assets/Scripts/TradeBot/TradeBot.cs','Assets/Scripts/TradeBot/TradeBot.Risk.cs','Assets/Scripts/TradeBot/RandomBot.cs','Assets/Scripts/StockInterestManager.cs',
'Assets/Scripts/MarketDay/BotChatData.cs','Assets/Scripts/MarketDay/MarketConversationFocus.cs','Assets/Scripts/NightShift/InvestorPsychology.cs','Assets/Scripts/NightShift/InvestorConversation.cs','Assets/Scripts/NightShift/LeadingRoomOrders.cs','Assets/Scripts/NightShift/BotMind.cs')
$taskSources+=Get-ChildItem (Join-Path $taskRoot 'Assets/Scripts/NightShift') -Filter 'ChatSocial*.cs' | Where-Object {$_.Name -notlike '*Unity.cs'} | ForEach-Object {$_.FullName}
foreach($taskSource in $taskSources) { $taskResponse+='"'+$(if([IO.Path]::IsPathRooted($taskSource)) {$taskSource} else {Join-Path $taskRoot $taskSource})+'"' }
$taskRsp=Join-Path $taskOut 'Market.rsp';Set-Content $taskRsp $taskResponse -Encoding utf8
& $taskRuntime $taskCompiler ('@'+$taskRsp)
if($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Set-Content (Join-Path $taskOut 'Market.runtimeconfig.json') '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.21"}}}' -Encoding utf8
& $taskRuntime (Join-Path $taskOut 'Market.dll') $taskRoot $Seed $Days
exit $LASTEXITCODE
