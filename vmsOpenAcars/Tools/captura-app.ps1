# Captura la ventana principal de vmsOpenACars a un PNG, sin depender del foco ni del orden de ventanas.
# Uso:  pwsh -File Tools\captura-app.ps1 [-Salida ruta.png]
param([string]$Salida = "$env:TEMP\mcdu-ref\app-real.png")

Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class CapturaApp {
  public delegate bool EnumProc(IntPtr h, IntPtr p);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public static List<IntPtr> Ventanas(uint pid) {
    var l = new List<IntPtr>();
    EnumWindows((h, p) => { uint w; GetWindowThreadProcessId(h, out w); if (w == pid && IsWindowVisible(h)) l.Add(h); return true; }, IntPtr.Zero);
    return l;
  }
  public static string Titulo(IntPtr h) { var sb = new StringBuilder(300); GetWindowText(h, sb, 300); return sb.ToString(); }
  public static int Area(IntPtr h) { RECT r; GetWindowRect(h, out r); return (r.R - r.L) * (r.B - r.T); }
  public static int[] Caja(IntPtr h) { RECT r; GetWindowRect(h, out r); return new int[] { r.R - r.L, r.B - r.T }; }
}
'@

$exe = Join-Path $PSScriptRoot '..\bin\Debug\vmsOpenACars.exe'
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 9

function Buscar([uint32]$proceso) {
  [CapturaApp]::Ventanas($proceso) | Sort-Object { [CapturaApp]::Area($_) } -Descending |
    Where-Object { ([CapturaApp]::Caja($_))[0] -gt 600 } | Select-Object -First 1
}

$grande = Buscar $proc.Id
if (-not $grande) {
  # Sin configuración la app abre un diálogo antes de la ventana: se cierra y sigue.
  $dialogo = [CapturaApp]::Ventanas([uint32]$proc.Id) | Sort-Object { [CapturaApp]::Area($_) } -Descending | Select-Object -First 1
  if ($dialogo) {
    [CapturaApp]::SetForegroundWindow($dialogo) | Out-Null
    Start-Sleep -Milliseconds 400
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
  }
  Start-Sleep -Seconds 10
  $grande = Buscar $proc.Id
}

if ($grande) {
  $c = [CapturaApp]::Caja($grande)
  $bmp = New-Object System.Drawing.Bitmap $c[0], $c[1]
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  [CapturaApp]::PrintWindow($grande, $hdc, 0) | Out-Null
  $g.ReleaseHdc($hdc); $g.Dispose()
  New-Item -ItemType Directory -Force -Path (Split-Path $Salida) | Out-Null
  $bmp.Save($Salida, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  "captura -> $Salida  ($($c[0])x$($c[1]))"
} else {
  "no se encontro la ventana principal"
}

Get-Process vmsOpenACars -ErrorAction SilentlyContinue | Stop-Process -Force
