# Opens context menus in the running app the keyboard way (the Menu key, posted only to Nordlys's own window)
# and reads their items through UI Automation. Dot-source native-ui.ps1 first.
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class NK {
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
}
"@

function Send-Key($app, [int]$vk) {
  [void][NK]::PostMessage($app.Hwnd, 0x0100, [IntPtr]$vk, [IntPtr]0)   # WM_KEYDOWN
  [void][NK]::PostMessage($app.Hwnd, 0x0101, [IntPtr]$vk, [IntPtr]0xC0000001)  # WM_KEYUP
  Start-Sleep -Milliseconds 350
}

function Get-OpenMenu($app) {
  $pid0 = $app.Process.Id
  $cond = New-Object Windows.Automation.AndCondition(
    (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty, $pid0)),
    (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Menu)))
  [Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Descendants, $cond)
}

function Read-Menu($app, $focusEl) {
  if ($focusEl) { $focusEl.SetFocus(); Start-Sleep -Milliseconds 200 }
  Send-Key $app 0x5D   # VK_APPS (the Menu key)
  $items = $null
  for ($i = 0; $i -lt 30; $i++) {
    $menu = Get-OpenMenu $app
    if ($menu) { $items = $menu.FindAll([Windows.Automation.TreeScope]::Children, [Windows.Automation.Condition]::TrueCondition); if ($items.Count -gt 0) { break } }
    Start-Sleep -Milliseconds 100
  }
  if (-not $items -or $items.Count -eq 0) { if (Get-OpenMenu $app) { Send-Key $app 0x1B }; return @('(no menu)') }
  $out = foreach ($i in $items) {
    $t = $i.Current.ControlType.ProgrammaticName -replace 'ControlType.', ''
    if ($t -eq 'Separator') { '---' } else { "$($i.Current.Name)$(if (-not $i.Current.IsEnabled) { ' [disabled]' })" }
  }
  Send-Key $app 0x1B   # Esc closes it
  for ($i = 0; $i -lt 10 -and (Get-OpenMenu $app); $i++) { Start-Sleep -Milliseconds 100 }
  $out
}

function Invoke-MenuItem($app, $focusEl, [string]$name) {
  if ($focusEl) { $focusEl.SetFocus(); Start-Sleep -Milliseconds 200 }
  Send-Key $app 0x5D
  $menu = Get-OpenMenu $app
  $cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, $name)
  $item = $menu.FindFirst([Windows.Automation.TreeScope]::Children, $cond)
  if (-not $item) { Send-Key $app 0x1B; throw "menu item not found: $name" }
  ($item.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke()
  Start-Sleep -Milliseconds 500
}

function Select-DocText($docEl, [string]$text) {
  $tp = $docEl.GetCurrentPattern([Windows.Automation.TextPattern]::Pattern)
  $range = $tp.DocumentRange.FindText($text, $false, $false)
  if (-not $range) { throw "text not found: $text" }
  $range.Select()
}
