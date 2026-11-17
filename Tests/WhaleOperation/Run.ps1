$ErrorActionPreference = 'Stop'
# 세력 작전 판단부(WhaleOperationPlan)만 따로 컴파일해 장난감 시장에서 200번 돌린다. Unity는 띄우지 않는다.
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOut = Join-Path $taskRoot 'Validation/WhaleOperation'
New-Item -ItemType Directory -Path $taskOut -Force | Out-Null
$taskData = 'C:/Program Files/Unity/6000.5.5f1/Editor/Data'
$taskRuntime = "$taskData/NetCoreRuntime/dotnet.exe"
$taskFramework = (Get-ChildItem "$taskData/NetCoreRuntime/shared/Microsoft.NETCore.App" | Select-Object -First 1).FullName
$taskResponse = @('-nologo', ('-out:"' + $taskOut + '/WhaleOperation.dll"'))
foreach ($taskName in @('System.Private.CoreLib','System.Runtime','System.Console','System.Collections','System.Linq')) { $taskResponse += '-r:"' + $taskFramework + '/' + $taskName + '.dll"' }
$taskResponse += '"' + (Join-Path $PSScriptRoot 'Test.cs') + '"'
$taskResponse += '"' + (Join-Path $taskRoot 'Assets/Scripts/TradeBot/WhaleOperationPlan.cs') + '"'
Set-Content -LiteralPath (Join-Path $taskOut 'compile.rsp') -Value $taskResponse -Encoding utf8
& $taskRuntime "$taskData/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" ('@' + (Join-Path $taskOut 'compile.rsp'))
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Set-Content -LiteralPath (Join-Path $taskOut 'WhaleOperation.runtimeconfig.json') -Value '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' -Encoding ascii
& $taskRuntime (Join-Path $taskOut 'WhaleOperation.dll')
exit $LASTEXITCODE
