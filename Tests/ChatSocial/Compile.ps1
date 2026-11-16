$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOut = Join-Path $taskRoot 'Validation/ChatSocial/Compile'
New-Item -ItemType Directory -Path $taskOut -Force | Out-Null
$taskProcess = [Diagnostics.Process]::GetCurrentProcess()
$taskProcess.ProcessorAffinity = [IntPtr]1
$taskProcess.PriorityClass = 'BelowNormal'
$taskRuntime = 'C:/Program Files/Unity/6000.5.5f1/Editor/Data/NetCoreRuntime/dotnet.exe'
$taskCompiler = 'C:/Program Files/Unity/6000.5.5f1/Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
$taskBase = 'Library/Bee/artifacts/1900b0aEDbg.dag/'
foreach ($taskAssembly in @('Assembly-CSharp','Assembly-CSharp-Editor')) {
    $taskResponse = Get-Content -LiteralPath (Join-Path $taskRoot ($taskBase + $taskAssembly + '.rsp')) | ForEach-Object {
        if ($_ -match '^-out:') { '-out:"' + $taskOut + '/' + $taskAssembly + '.dll"' }
        elseif ($_ -match '^-refout:') { '-refout:"' + $taskOut + '/' + $taskAssembly + '.ref.dll"' }
        elseif ($_ -eq ('-r:"' + $taskBase + 'Assembly-CSharp.ref.dll"')) { '-r:"' + $taskOut + '/Assembly-CSharp.ref.dll"' }
        elseif ($_ -match '^"(Assets/(?:Scripts|Editor)/.+\.cs)"$' -and !(Test-Path -LiteralPath (Join-Path $taskRoot $Matches[1]))) {
            Write-Host ('Removed stale Unity source entry: ' + $Matches[1])
        }
        else { $_ }
    }
    $taskDirectory = if ($taskAssembly -eq 'Assembly-CSharp') { 'Assets/Scripts' } else { 'Assets/Editor' }
    Get-ChildItem -LiteralPath (Join-Path $taskRoot $taskDirectory) -Recurse -Filter '*.cs' | ForEach-Object {
        $taskRelative = $_.FullName.Substring($taskRoot.Length + 1).Replace('\','/')
        if (!($taskResponse -contains ('"' + $taskRelative + '"'))) { $taskResponse += '"' + $taskRelative + '"' }
    }
    $taskResponse += '-parallel-'
    $taskPath = Join-Path $taskOut ($taskAssembly + '.rsp')
    Set-Content -LiteralPath $taskPath -Value $taskResponse -Encoding utf8
    & $taskRuntime $taskCompiler ('@' + $taskPath) 2>&1 | Tee-Object -FilePath (Join-Path $taskOut ($taskAssembly + '.log'))
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
