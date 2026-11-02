# Helpers for checking the native app: start it on a throwaway data folder, find and invoke controls through
# UI Automation (the accessibility API), and capture the app's own window with PrintWindow.
# Usage: . .\scripts\native-ui.ps1 ; $app = Start-Nordlys ; Save-Window $app "C:\temp\shot.png"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class NW {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$script:Exe = Join-Path $PSScriptRoot "..\desktop\Nordlys.App\bin\Debug\net10.0-windows\Nordlys.exe"

function Start-Nordlys([string]$DataDir = (Join-Path $env:TEMP "nordlys-uitest"), [switch]$Fresh, [string]$Exe = $script:Exe) {
  if ($Fresh -and (Test-Path $DataDir)) { Remove-Item -Recurse -Force $DataDir }
  $p = Start-Process -FilePath $Exe -ArgumentList "--data", "`"$DataDir`"" -PassThru
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($p.MainWindowHandle -eq 0 -and $sw.ElapsedMilliseconds -lt 20000) { Start-Sleep -Milliseconds 50; $p.Refresh() }
  Start-Sleep -Milliseconds 800
  $p.Refresh()
  [pscustomobject]@{ Process = $p; Hwnd = $p.MainWindowHandle; Root = [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle); Data = $DataDir; WindowMs = $sw.ElapsedMilliseconds }
}

function Find-El($app, [string]$Name, [string]$Type = $null) {
  $cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, $Name)
  $all = $app.Root.FindAll([Windows.Automation.TreeScope]::Descendants, $cond)
  foreach ($e in $all) { if (-not $Type -or $e.Current.ControlType.ProgrammaticName -eq "ControlType.$Type") { return $e } }
  return $null
}

function Find-Like($app, [string]$Pattern) {
  $all = $app.Root.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)
  foreach ($e in $all) { if ($e.Current.Name -like $Pattern) { return $e } }
  return $null
}

function Invoke-El($el) {
  if (-not $el) { throw "element not found" }
  $pat = $null
  if ($el.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$pat)) { $pat.Invoke() }
  elseif ($el.TryGetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pat)) { $pat.Select() }
  elseif ($el.TryGetCurrentPattern([Windows.Automation.TogglePattern]::Pattern, [ref]$pat)) { $pat.Toggle() }
  else { throw "element is not invokable: $($el.Current.Name)" }
  Start-Sleep -Milliseconds 400
}

function Get-Title($app) { $app.Process.Refresh(); $app.Process.MainWindowTitle }

function Save-Window($app, [string]$Path) {
  $r = New-Object NW+RECT
  [void][NW]::GetWindowRect($app.Hwnd, [ref]$r)
  $bmp = New-Object Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
  $g = [Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  [void][NW]::PrintWindow($app.Hwnd, $hdc, 2)  # PW_RENDERFULLCONTENT: just this window
  $g.ReleaseHdc($hdc)
  $bmp.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  $Path
}

function Stop-Nordlys($app) {
  [void][NW]::PostMessage($app.Hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)  # WM_CLOSE, like the close button
  if (-not $app.Process.WaitForExit(8000)) { $app.Process.Kill() }
}
