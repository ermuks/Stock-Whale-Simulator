$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOut = Join-Path $taskRoot 'Validation/Corporate/Core'
New-Item -ItemType Directory -Path $taskOut -Force | Out-Null
$taskSelf = [Diagnostics.Process]::GetCurrentProcess()
$taskSelf.ProcessorAffinity = [IntPtr]1
$taskSelf.PriorityClass = 'BelowNormal'
$taskRuntime = 'C:/Program Files/Unity/6000.5.5f1/Editor/Data/NetCoreRuntime/dotnet.exe'
$taskCompiler = 'C:/Program Files/Unity/6000.5.5f1/Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
$taskRsp = @('-nologo','-target:exe','-parallel-',('-out:"' + $taskOut + '/Corporate.dll"'))
Get-ChildItem -LiteralPath 'C:/Program Files/Unity/6000.5.5f1/Editor/Data/DotNetSdk/packs/Microsoft.NETCore.App.Ref/8.0.21/ref/net8.0' -Filter '*.dll' | ForEach-Object { $taskRsp += '-r:"' + $_.FullName + '"' }
$taskRsp += '"' + $taskRoot + '/Assets/Scripts/NightShift/DailyIncomeLedger.cs"'
$taskRsp += '"' + $taskRoot + '/Tests/Corporate/Program.cs"'
$taskRsp += '"' + $taskRoot + '/Assets/Scripts/NightShift/CompanyBusiness.cs"'
$taskRsp += '"' + $taskRoot + '/Assets/Scripts/NightShift/CompanyGrowth.cs"'
Set-Content -LiteralPath (Join-Path $taskOut 'Corporate.rsp') -Value $taskRsp -Encoding utf8
& $taskRuntime $taskCompiler ('@' + $taskOut + '/Corporate.rsp')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Set-Content -LiteralPath (Join-Path $taskOut 'Corporate.runtimeconfig.json') -Value '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.21"}}}' -Encoding utf8
& $taskRuntime (Join-Path $taskOut 'Corporate.dll') | Tee-Object -FilePath (Join-Path $taskOut 'results.txt')
exit $LASTEXITCODE

