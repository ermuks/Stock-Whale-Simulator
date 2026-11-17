param([int]$Stage = 1, [string]$Scenario = "all")
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOut = Join-Path $taskRoot 'Validation/ChatSocial'
$taskProcess = [Diagnostics.Process]::GetCurrentProcess()
$taskProcess.ProcessorAffinity = [IntPtr]1
$taskProcess.PriorityClass = 'BelowNormal'
$taskRuntime = 'C:/Program Files/Unity/6000.5.5f1/Editor/Data/NetCoreRuntime/dotnet.exe'
$taskCompiler = 'C:/Program Files/Unity/6000.5.5f1/Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
$taskPython = 'C:/Program Files/WindowsApps/OpenAI.CodexPrimaryRuntime.v26-1112-611-0_26.1112.611.0_x64__3k8sg7r9htsxt/dependencies/python/python.exe'
& $taskPython (Join-Path $PSScriptRoot 'Prepare.py')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$taskResponse = @('-nologo','-target:exe','-parallel-',('-out:"' + $taskOut + '/ChatSocial.dll"'))
Get-ChildItem -LiteralPath 'C:/Program Files/Unity/6000.5.5f1/Editor/Data/DotNetSdk/packs/Microsoft.NETCore.App.Ref/8.0.21/ref/net8.0' -Filter '*.dll' | ForEach-Object { $taskResponse += '-r:"' + $_.FullName + '"' }
$taskSources = @('Tests/ChatSocial/Host.cs','Validation/ChatSocial/LegacyChatProduction.cs','Assets/Scripts/MarketDay/BotChatData.cs','Assets/Scripts/MarketDay/MarketConversationFocus.cs','Assets/Scripts/NightShift/InvestorPsychology.cs','Assets/Scripts/NightShift/BotMind.cs','Assets/Scripts/NightShift/InvestorConversation.cs')
$taskSources += Get-ChildItem -LiteralPath (Join-Path $taskRoot 'Tests/ChatSocial') -Filter 'Tests*.cs' | ForEach-Object { $_.FullName }
$taskSources += Get-ChildItem -LiteralPath (Join-Path $taskRoot 'Assets/Scripts/NightShift') -Filter 'ChatSocial*.cs' | Where-Object { $_.Name -notlike '*Unity.cs' -and $_.Name -ne 'ChatSocialHype.cs' } | ForEach-Object { $_.FullName }
$taskSources += 'Validation/ChatSocial/ProgressionProduction.cs'
$taskSources += 'Assets/Scripts/NightShift/InvestorBelief.cs','Assets/Scripts/MarketDay/BotNicknames.cs'
$taskSources += 'Assets/Scripts/NightShift/LeadingRoomOrders.cs'
$taskSources += 'Assets/Scripts/NightShift/DailyIncomeLedger.cs'
$taskSources += 'Assets/Scripts/Settings/SessionClock.cs'
$taskSources += 'Assets/Scripts/News/NewsArticle.cs','Assets/Scripts/News/CompanyOpinionBook.cs'
foreach ($taskSource in $taskSources) {
    $taskPath = if ([IO.Path]::IsPathRooted($taskSource)) { $taskSource } else { Join-Path $taskRoot $taskSource }
    $taskResponse += '"' + $taskPath + '"'
}
$taskResponsePath = Join-Path $taskOut 'ChatSocial.rsp'
Set-Content -LiteralPath $taskResponsePath -Value $taskResponse -Encoding utf8
& $taskRuntime $taskCompiler ('@' + $taskResponsePath)
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Set-Content -LiteralPath (Join-Path $taskOut 'ChatSocial.runtimeconfig.json') -Value '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.21"}}}' -Encoding utf8
& $taskRuntime (Join-Path $taskOut 'ChatSocial.dll') $taskRoot $Stage $Scenario | Tee-Object -FilePath (Join-Path $taskOut ('stage-' + $Stage + '.txt'))
exit $LASTEXITCODE
