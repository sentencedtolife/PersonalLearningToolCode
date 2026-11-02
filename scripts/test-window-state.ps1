# Checks that the release app reopens where it was closed: moves/resizes its own window with the Win32 API,
# closes it normally (WM_CLOSE, like the close button), relaunches, and reads the window rectangle back.
# Usage: powershell -ExecutionPolicy Bypass -File scripts/test-window-state.ps1
param([string]$Exe = "$PSScriptRoot\..\src-tauri\target\release\nordlys.exe")

Add-Type @"
using System; using System.Runtime.InteropServices;
public struct RECT { public int Left, Top, Right, Bottom; }
public static class W {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
}
"@

function Start-App {
  $p = Start-Process -FilePath $Exe -PassThru
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($p.MainWindowHandle -eq 0 -and $sw.ElapsedMilliseconds -lt 15000) { Start-Sleep -Milliseconds 50; $p.Refresh() }
  Start-Sleep -Seconds 2
  $p.Refresh()
  return $p
}
function Get-Rect($p) { $r = New-Object RECT; [void][W]::GetWindowRect($p.MainWindowHandle, [ref]$r); "$($r.Left),$($r.Top) $($r.Right - $r.Left)x$($r.Bottom - $r.Top)" }
function Close-App($p) { [void][W]::PostMessage($p.MainWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero); $p.WaitForExit(10000) | Out-Null }

# 1. Size and position.
$p = Start-App
"opened at:        $(Get-Rect $p)"
[void][W]::SetWindowPos($p.MainWindowHandle, [IntPtr]::Zero, 140, 90, 1000, 700, 0x0014)  # NOZORDER | NOACTIVATE
Start-Sleep -Milliseconds 500
$want = Get-Rect $p
"moved to:         $want"
Close-App $p
$p = Start-App
$got = Get-Rect $p
"reopened at:      $got"
if ($got -eq $want) { "PASS size and position restored" } else { "FAIL expected $want" }

# 2. Maximised state.
[void][W]::ShowWindow($p.MainWindowHandle, 3)  # SW_MAXIMIZE
Start-Sleep -Milliseconds 500
Close-App $p
$p = Start-App
$max = [W]::IsZoomed($p.MainWindowHandle)
if ($max) { "PASS reopened maximised" } else { "FAIL not maximised after reopening" }
[void][W]::ShowWindow($p.MainWindowHandle, 9)  # SW_RESTORE
Start-Sleep -Milliseconds 500
"restored to:      $(Get-Rect $p)"
Close-App $p
