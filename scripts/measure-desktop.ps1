# Measures a desktop build: time to main window, memory (the app and any child processes), idle CPU.
# Uses a throwaway data folder. Usage:
#   powershell -ExecutionPolicy Bypass -File scripts/measure-desktop.ps1 [-Exe path\to\Nordlys.exe] [-Runs 5] [-NoDataArg]
# -NoDataArg: for builds without the --data option (the old Tauri build); they use their normal data folder.
param(
  [int]$Runs = 5,
  [string]$Exe = "$PSScriptRoot\..\desktop\publish\r2r\Nordlys.exe",
  [string]$DataDir = (Join-Path $env:TEMP "nordlys-measure"),
  [switch]$NoDataArg
)

function Get-Tree([int]$rootId) {
  $all = Get-CimInstance Win32_Process | Select-Object ProcessId, ParentProcessId
  $ids = @($rootId)
  $queue = @($rootId)
  while ($queue.Count) {
    $p = $queue[0]; $queue = @($queue | Select-Object -Skip 1)
    foreach ($c in $all | Where-Object { $_.ParentProcessId -eq $p }) { $ids += $c.ProcessId; $queue += $c.ProcessId }
  }
  Get-Process -Id $ids -ErrorAction SilentlyContinue
}

$results = @()
for ($i = 1; $i -le $Runs; $i++) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $p = if ($NoDataArg) { Start-Process -FilePath $Exe -PassThru } else { Start-Process -FilePath $Exe -ArgumentList "--data", "`"$DataDir`"" -PassThru }
  while ($p.MainWindowHandle -eq 0 -and $sw.ElapsedMilliseconds -lt 30000) { Start-Sleep -Milliseconds 5; $p.Refresh() }
  $windowMs = $sw.ElapsedMilliseconds
  Start-Sleep -Seconds 8                      # let it load and settle
  $tree = Get-Tree $p.Id
  $cpuStart = ($tree | ForEach-Object { $_.TotalProcessorTime.TotalMilliseconds } | Measure-Object -Sum).Sum
  Start-Sleep -Seconds 10                     # idle window
  $tree = Get-Tree $p.Id
  $cpuEnd = ($tree | ForEach-Object { $_.TotalProcessorTime.TotalMilliseconds } | Measure-Object -Sum).Sum
  $ws = ($tree | Measure-Object -Property WorkingSet64 -Sum).Sum / 1MB
  $priv = ($tree | Measure-Object -Property PrivateMemorySize64 -Sum).Sum / 1MB
  $results += [pscustomobject]@{
    run = $i; windowMs = $windowMs; processes = @($tree).Count
    workingSetMB = [math]::Round($ws, 1); privateMB = [math]::Round($priv, 1)
    idleCpuMsPer10s = [math]::Round($cpuEnd - $cpuStart, 0)
  }
  $tree | Stop-Process -Force -ErrorAction SilentlyContinue
  Start-Sleep -Seconds 2
}
$results | Format-Table -AutoSize
$median = { param($k) ($results | Sort-Object $k)[[math]::Floor($results.Count / 2)].$k }
"median windowMs=$(& $median windowMs) workingSetMB=$(& $median workingSetMB) privateMB=$(& $median privateMB) idleCpuMsPer10s=$(& $median idleCpuMsPer10s)"
