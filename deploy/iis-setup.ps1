<#
.SYNOPSIS
  Puts IIS in front of the Archiving service, as a window: press فحص, then تطبيق.

.DESCRIPTION
  IIS cannot host this application directly and cannot share its port — the service is a complete
  web server of its own. So the arrangement is a reverse proxy: the service moves to a loopback-only
  internal port, and IIS takes the public one and forwards to it. That is what this window sets up,
  end to end:

    · checks IIS, URL Rewrite and ARR are present, and turns the ARR proxy on
    · moves the service to the internal port and RELEASES ITS URL RESERVATION on the public one —
      the reservation is what makes IIS fail with "the process cannot access the file because it is
      being used by another process", and no amount of stopping the service alone clears it
    · creates the site, its app pool and a web.config with the rewrite rule
    · raises IIS's 30 MB request limit, which would otherwise break document uploads
    · opens the firewall and checks the whole chain answers

  Everything it does is undoable: تراجع puts the service back on the public port and removes the
  site. Launch it by double-clicking Setup-IIS.cmd, which handles the elevation prompt.

.PARAMETER SelfTest
  Build the window and exit without showing it — a form cannot be driven headlessly, so this is how
  the script is checked after an edit.
#>
[CmdletBinding()]
param([switch] $SelfTest)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$ServiceName = 'Archiving'
$appcmd = Join-Path $env:windir 'system32\inetsrv\appcmd.exe'

# ── where the system is installed ──────────────────────────────────────────────────────────────
# Asked of the service itself rather than assumed to be next to this script: this window is useful
# from anywhere, including a fresh package folder that is not the installation.
function Get-InstallFolder {
  $svc = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'" -ErrorAction SilentlyContinue
  if ($svc -and $svc.PathName) {
    $path = $svc.PathName.Trim('"')
    $dir = Split-Path -Parent $path
    if (Test-Path (Join-Path $dir 'appsettings.Production.json')) { return $dir }
  }
  if (Test-Path (Join-Path $PSScriptRoot 'appsettings.Production.json')) { return $PSScriptRoot }
  return $null
}

function Get-ServiceAccount {
  $svc = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'" -ErrorAction SilentlyContinue
  if ($svc -and $svc.StartName) { return $svc.StartName }
  return 'NT AUTHORITY\NetworkService'
}

function Get-ServicePort {
  param([string] $Folder)
  if (-not $Folder) { return 0 }
  $file = Join-Path $Folder 'appsettings.Production.json'
  if (-not (Test-Path $file)) { return 0 }
  try {
    $s = Get-Content $file -Raw | ConvertFrom-Json
    if ($s.Urls -match ':(\d+)') { return [int]$Matches[1] }
  } catch { }
  return 0
}

$installFolder = Get-InstallFolder
$currentPort = Get-ServicePort $installFolder

# ── prerequisites ──────────────────────────────────────────────────────────────────────────────
function Get-Prereqs {
  $r = [ordered]@{ Iis = $false; Rewrite = $false; Arr = $false; Proxy = $false }
  $r.Iis = Test-Path $appcmd
  if ($r.Iis) {
    $modules = & $appcmd list modules 2>$null
    $r.Rewrite = [bool]($modules | Select-String -SimpleMatch 'RewriteModule' -Quiet)
    $r.Arr = [bool]($modules | Select-String -SimpleMatch 'ApplicationRequestRouting' -Quiet)
    # The proxy is off by default; ARR being installed is not the same as ARR forwarding anything.
    $proxy = & $appcmd list config -section:system.webServer/proxy 2>$null
    $r.Proxy = [bool]($proxy | Select-String -SimpleMatch 'enabled="true"' -Quiet)
  }
  # Registry fallback: a module list can be empty on a locked-down configuration.
  if (-not $r.Rewrite) { $r.Rewrite = Test-Path 'HKLM:\SOFTWARE\Microsoft\IIS Extensions\URL Rewrite' }
  if (-not $r.Arr) { $r.Arr = Test-Path 'HKLM:\SOFTWARE\Microsoft\IIS Extensions\Application Request Routing' }
  [pscustomobject]$r
}

# ── the window ─────────────────────────────────────────────────────────────────────────────────
$form = New-Object System.Windows.Forms.Form
$form.Text = 'إعداد IIS كواجهة لنظام الأرشفة'
$form.Size = New-Object System.Drawing.Size(760, 700)
$form.StartPosition = 'CenterScreen'
$form.RightToLeft = 'Yes'
$form.RightToLeftLayout = $true
$form.Font = New-Object System.Drawing.Font('Segoe UI', 10)
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false

$intro = New-Object System.Windows.Forms.Label
$intro.Text = 'سيأخذ IIS المنفذ العام ويمرّر الطلبات إلى الخدمة على منفذ داخلي مغلق على الخادم نفسه.'
$intro.ForeColor = [System.Drawing.Color]::DimGray
$intro.Location = New-Object System.Drawing.Point(20, 14)
$intro.Size = New-Object System.Drawing.Size(700, 24)
$form.Controls.Add($intro)

function Add-Label { param($text, $y)
  $l = New-Object System.Windows.Forms.Label
  $l.Text = $text; $l.Location = New-Object System.Drawing.Point(20, $y)
  $l.Size = New-Object System.Drawing.Size(200, 24); $l.TextAlign = 'MiddleRight'
  $form.Controls.Add($l); $l
}
function Add-Num { param($y, $value, $max = 65535)
  $n = New-Object System.Windows.Forms.NumericUpDown
  $n.Location = New-Object System.Drawing.Point(230, $y)
  $n.Size = New-Object System.Drawing.Size(110, 24)
  $n.Minimum = 1; $n.Maximum = $max; $n.Value = $value
  $form.Controls.Add($n); $n
}

$y = 48
Add-Label 'المنفذ العام (IIS)' $y | Out-Null
# Defaults to whatever the service is on now: the point of putting IIS in front is almost always to
# keep the address people already use, and hand it to IIS.
$publicDefault = 7070
if ($currentPort) { $publicDefault = $currentPort }
$numPublic = Add-Num $y $publicDefault
$lblPublicHint = New-Object System.Windows.Forms.Label
$lblPublicHint.Text = 'العنوان الذي يفتحه الموظفون'
$lblPublicHint.ForeColor = [System.Drawing.Color]::DimGray
$lblPublicHint.Location = New-Object System.Drawing.Point(350, ($y + 2))
$lblPublicHint.Size = New-Object System.Drawing.Size(370, 22)
$form.Controls.Add($lblPublicHint)

$y += 34
Add-Label 'المنفذ الداخلي للخدمة' $y | Out-Null
$numInternal = Add-Num $y 5080
$lblIntHint = New-Object System.Windows.Forms.Label
$lblIntHint.Text = 'يستمع على 127.0.0.1 فقط — لا يُفتح في جدار الحماية'
$lblIntHint.ForeColor = [System.Drawing.Color]::DimGray
$lblIntHint.Location = New-Object System.Drawing.Point(350, ($y + 2))
$lblIntHint.Size = New-Object System.Drawing.Size(370, 22)
$form.Controls.Add($lblIntHint)

$y += 34
Add-Label 'اسم الموقع في IIS' $y | Out-Null
$txtSite = New-Object System.Windows.Forms.TextBox
$txtSite.Location = New-Object System.Drawing.Point(230, $y)
$txtSite.Size = New-Object System.Drawing.Size(200, 24); $txtSite.Text = 'Archiving'
$txtSite.RightToLeft = 'No'
$form.Controls.Add($txtSite)

$y += 34
Add-Label 'مجلد الموقع' $y | Out-Null
$txtPath = New-Object System.Windows.Forms.TextBox
$txtPath.Location = New-Object System.Drawing.Point(230, $y)
$txtPath.Size = New-Object System.Drawing.Size(380, 24)
$txtPath.Text = 'C:\inetpub\Archiving'
$txtPath.RightToLeft = 'No'
$form.Controls.Add($txtPath)
$lblPathHint = New-Object System.Windows.Forms.Label
$lblPathHint.Text = 'لا يحوي إلا web.config'
$lblPathHint.ForeColor = [System.Drawing.Color]::DimGray
$lblPathHint.Location = New-Object System.Drawing.Point(620, ($y + 2))
$lblPathHint.Size = New-Object System.Drawing.Size(110, 22)
$form.Controls.Add($lblPathHint)

$y += 34
Add-Label 'أقصى حجم ملف مرفوع (م.ب)' $y | Out-Null
$numUpload = Add-Num $y 500 4000
$lblUpHint = New-Object System.Windows.Forms.Label
$lblUpHint.Text = 'حدّ IIS الافتراضي 30 م.ب ويكسر رفع الوثائق الكبيرة'
$lblUpHint.ForeColor = [System.Drawing.Color]::DimGray
$lblUpHint.Location = New-Object System.Drawing.Point(350, ($y + 2))
$lblUpHint.Size = New-Object System.Drawing.Size(370, 22)
$form.Controls.Add($lblUpHint)

$y += 36
$chkStopOthers = New-Object System.Windows.Forms.CheckBox
$chkStopOthers.Text = 'إيقاف أي موقع آخر في IIS يستخدم المنفذ العام (مثل Default Web Site)'
$chkStopOthers.Location = New-Object System.Drawing.Point(230, $y)
$chkStopOthers.Size = New-Object System.Drawing.Size(500, 24)
$chkStopOthers.Checked = $true
$form.Controls.Add($chkStopOthers)

$y += 28
$chkFirewall = New-Object System.Windows.Forms.CheckBox
$chkFirewall.Text = 'فتح المنفذ العام في جدار الحماية'
$chkFirewall.Location = New-Object System.Drawing.Point(230, $y)
$chkFirewall.Size = New-Object System.Drawing.Size(500, 24)
$chkFirewall.Checked = $true
$form.Controls.Add($chkFirewall)

$y += 28
$chkShortcut = New-Object System.Windows.Forms.CheckBox
$chkShortcut.Text = 'تحديث اختصار سطح المكتب ليفتح عنوان IIS'
$chkShortcut.Location = New-Object System.Drawing.Point(230, $y)
$chkShortcut.Size = New-Object System.Drawing.Size(500, 24)
$chkShortcut.Checked = $true
$form.Controls.Add($chkShortcut)

$y += 36
$log = New-Object System.Windows.Forms.TextBox
$log.Location = New-Object System.Drawing.Point(20, $y)
$log.Size = New-Object System.Drawing.Size(700, 280)
$log.Multiline = $true; $log.ScrollBars = 'Vertical'; $log.ReadOnly = $true
$log.RightToLeft = 'No'      # its output is English: paths, netsh and appcmd messages
$log.Font = New-Object System.Drawing.Font('Consolas', 9)
$form.Controls.Add($log)

$y += 292
$btnCheck = New-Object System.Windows.Forms.Button
$btnCheck.Text = 'فحص المتطلبات'
$btnCheck.Location = New-Object System.Drawing.Point(460, $y)
$btnCheck.Size = New-Object System.Drawing.Size(130, 32)
$form.Controls.Add($btnCheck)

$btnApply = New-Object System.Windows.Forms.Button
$btnApply.Text = 'تطبيق'
$btnApply.Location = New-Object System.Drawing.Point(610, $y)
$btnApply.Size = New-Object System.Drawing.Size(110, 32)
$btnApply.BackColor = [System.Drawing.Color]::FromArgb(0, 90, 158)
$btnApply.ForeColor = [System.Drawing.Color]::White
$form.Controls.Add($btnApply)
$form.AcceptButton = $btnApply

$btnRevert = New-Object System.Windows.Forms.Button
$btnRevert.Text = 'تراجع (إزالة IIS من الطريق)'
$btnRevert.Location = New-Object System.Drawing.Point(160, $y)
$btnRevert.Size = New-Object System.Drawing.Size(220, 32)
$form.Controls.Add($btnRevert)

$btnClose = New-Object System.Windows.Forms.Button
$btnClose.Text = 'إغلاق'
$btnClose.Location = New-Object System.Drawing.Point(20, $y)
$btnClose.Size = New-Object System.Drawing.Size(90, 32)
$btnClose.Add_Click({ $form.Close() })
$form.Controls.Add($btnClose)

function Write-Log { param([string] $text)
  $log.AppendText("$text`r`n")
  [System.Windows.Forms.Application]::DoEvents()
}

# ── the pieces of work ─────────────────────────────────────────────────────────────────────────

<#
  Move the service to a new port, in place. Deliberately not done by re-running install-service.ps1:
  that rebuilds the connection string from its own defaults, and a port change must never touch the
  database settings.

  The URL reservation is the part that matters here. Kestrel runs as NetworkService and therefore
  holds http://+:<port>/ in http.sys; until that is deleted, IIS binding the same port fails with
  "used by another process" even with the service stopped.
#>
function Set-ServicePort {
  param([int] $NewPort, [switch] $Public)

  if (-not $installFolder) { throw "The Archiving service is not installed on this machine." }
  $file = Join-Path $installFolder 'appsettings.Production.json'
  $s = Get-Content $file -Raw | ConvertFrom-Json
  $old = 0
  if ($s.Urls -match ':(\d+)') { $old = [int]$Matches[1] }
  $account = Get-ServiceAccount

  # Behind IIS the service must not be reachable from the network: binding loopback closes the door
  # on anyone who would otherwise skip the proxy by naming the internal port.
  $host_ = '127.0.0.1'
  if ($Public) { $host_ = '0.0.0.0' }
  $s.Urls = "http://${host_}:$NewPort"
  $s | ConvertTo-Json -Depth 10 | Set-Content $file -Encoding utf8
  Write-Log "settings: Urls -> $($s.Urls)"

  $svc = Get-Service $ServiceName -ErrorAction SilentlyContinue
  if ($svc -and $svc.Status -ne 'Stopped') {
    Stop-Service $ServiceName -Force
    $svc.WaitForStatus('Stopped', '00:00:30')
    Write-Log "service stopped"
  }

  if ($old -and $old -ne $NewPort) {
    netsh http delete urlacl url="http://+:$old/" 2>&1 | Out-Null
    Write-Log "released urlacl http://+:$old/  (this is what frees the port for IIS)"
  }
  $reserve = "http://+:$NewPort/"
  if (-not $Public) { $reserve = "http://127.0.0.1:$NewPort/" }
  netsh http add urlacl url=$reserve user="$account" 2>&1 | Out-Null
  Write-Log "reserved $reserve for $account"

  Start-Service $ServiceName
  Write-Log "service started"

  $deadline = (Get-Date).AddSeconds(60)
  do {
    Start-Sleep -Seconds 2
    try {
      $r = Invoke-WebRequest "http://127.0.0.1:$NewPort/api/public/stats" -UseBasicParsing -TimeoutSec 5
      if ($r.StatusCode -eq 200) { Write-Log "service answers on $NewPort  [OK]"; return $old }
    } catch { }
  } while ((Get-Date) -lt $deadline)
  throw "The service did not answer on port $NewPort. Check: Get-EventLog -LogName Application -Source Archiving -Newest 20"
}

function Write-ProxyConfig {
  param([string] $Folder, [int] $InternalPort, [int] $UploadMb)
  New-Item -ItemType Directory -Force -Path $Folder | Out-Null
  $bytes = $UploadMb * 1MB
  $xml = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="ArchivingProxy" stopProcessing="true">
          <match url="(.*)" />
          <action type="Rewrite" url="http://127.0.0.1:$InternalPort/{R:1}" />
        </rule>
      </rules>
    </rewrite>
    <security>
      <requestFiltering>
        <requestLimits maxAllowedContentLength="$bytes" />
      </requestFiltering>
    </security>
    <httpProtocol>
      <customHeaders>
        <remove name="X-Powered-By" />
      </customHeaders>
    </httpProtocol>
  </system.webServer>
</configuration>
"@
  $target = Join-Path $Folder 'web.config'
  Set-Content -Path $target -Value $xml -Encoding utf8
  Write-Log "wrote $target  (upload limit $UploadMb MB)"
}

function New-ProxySite {
  param([string] $Name, [int] $Port, [string] $Folder)
  Import-Module WebAdministration -ErrorAction Stop

  if ($chkStopOthers.Checked) {
    foreach ($site in Get-Website) {
      if ($site.Name -eq $Name) { continue }
      $bindings = @($site.Bindings.Collection | ForEach-Object { $_.bindingInformation })
      if ($bindings -match ":$Port`:") {
        if ($site.State -ne 'Stopped') { Stop-Website -Name $site.Name }
        Set-ItemProperty "IIS:\Sites\$($site.Name)" -Name serverAutoStart -Value $false
        Write-Log "stopped conflicting site '$($site.Name)' on port $Port"
      }
    }
  }

  if (-not (Test-Path "IIS:\AppPools\$Name")) {
    New-WebAppPool -Name $Name | Out-Null
    Write-Log "created app pool '$Name'"
  }
  # 'No Managed Code': nothing .NET runs inside IIS here — it only forwards.
  Set-ItemProperty "IIS:\AppPools\$Name" -Name managedRuntimeVersion -Value ''

  <#
    Coming back by itself after a reboot.

    A stock app pool starts its worker process on the first request and shuts it down again after
    20 idle minutes. For a site people browse to that is invisible; for this one it is not, because
    the pool here is a reverse proxy — nothing reaches the API until IIS has a worker, so a server
    that rebooted overnight answers the first person of the morning with a 503 rather than a page.

    AlwaysRunning starts the worker with the service, and the two zeroes stop Windows from taking it
    away again: no idle shutdown, and no scheduled recycle at 02:00 that leaves the site cold until
    somebody knocks.
  #>
  Set-ItemProperty "IIS:\AppPools\$Name" -Name autoStart -Value $true
  Set-ItemProperty "IIS:\AppPools\$Name" -Name startMode -Value 'AlwaysRunning'
  Set-ItemProperty "IIS:\AppPools\$Name" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
  Set-ItemProperty "IIS:\AppPools\$Name" -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
  Write-Log "app pool: AlwaysRunning, no idle shutdown, no timed recycle"

  <#
    Rapid-fail protection is what turns a temporary problem into a permanent one: five failures in
    five minutes and IIS stops the pool and leaves it stopped, which is the classic 'HTTP Error 503.
    The service is unavailable.' Off here, because the thing behind this pool is a Windows service
    with its own restart policy — IIS disabling itself on top of that only hides the real fault.
  #>
  Set-ItemProperty "IIS:\AppPools\$Name" -Name failure.rapidFailProtection -Value $false

  $existing = Get-Website -Name $Name -ErrorAction SilentlyContinue
  if ($existing) {
    Set-ItemProperty "IIS:\Sites\$Name" -Name physicalPath -Value $Folder
    Set-ItemProperty "IIS:\Sites\$Name" -Name applicationPool -Value $Name
    Get-WebBinding -Name $Name | Remove-WebBinding
    New-WebBinding -Name $Name -Protocol http -Port $Port -IPAddress '*'
    Write-Log "updated site '$Name' -> port $Port"
  } else {
    New-Website -Name $Name -Port $Port -PhysicalPath $Folder -ApplicationPool $Name | Out-Null
    Write-Log "created site '$Name' on port $Port"
  }
  <#
    Authentication belongs to the application, not to IIS. A site that inherits Windows
    Authentication (or has Anonymous switched off) puts a browser "Sign in — Username/Password" box
    in front of the system, before its own login page is ever reached — and no account the user
    knows will satisfy it, because it is asking for a Windows account, not an Archiving one.

    Forced explicitly rather than left to whatever the machine's defaults are.
  #>
  $authRoot = 'system.webServer/security/authentication'
  foreach ($pair in @(@("$authRoot/anonymousAuthentication", 'True'),
                      @("$authRoot/windowsAuthentication", 'False'),
                      @("$authRoot/basicAuthentication", 'False'))) {
    # A method that is not installed has no section to set; that is not a failure.
    & $appcmd set config $Name "-section:$($pair[0])" "/enabled:$($pair[1])" /commit:apphost 2>&1 | Out-Null
  }
  Write-Log "authentication: anonymous on, windows/basic off (the app signs users in itself)"

  Set-ItemProperty "IIS:\Sites\$Name" -Name serverAutoStart -Value $true

  # The pool has to be running before the site is started, or the first request still waits for a
  # worker — and a pool left stopped by an earlier failure would otherwise stay that way.
  if ((Get-WebAppPoolState -Name $Name).Value -ne 'Started') { Start-WebAppPool -Name $Name }
  Start-Website -Name $Name
  Write-Log "site and app pool started (both set to start automatically with Windows)"
}

<#
  The desktop shortcut carries the address inside it, so moving the site to IIS without rewriting it
  leaves «الديوان الإلكتروني» on every desktop pointing at a port nothing listens on any more.
#>
function Update-Shortcut {
  param([int] $Port)
  if (-not $installFolder) { return }
  $script = Join-Path $installFolder 'create-shortcut.ps1'
  if (-not (Test-Path $script)) { Write-Log "create-shortcut.ps1 not found — desktop shortcut left as it is."; return }
  try {
    & $script -Url "http://$env:COMPUTERNAME`:$Port/" -AllUsers 2>&1 | ForEach-Object { Write-Log "$_" }
  } catch {
    Write-Log "shortcut not updated: $($_.Exception.Message)"
  }
}

# ── فحص ────────────────────────────────────────────────────────────────────────────────────────
$btnCheck.Add_Click({
  $log.Clear()
  Write-Log "install folder : $installFolder"
  Write-Log "service port   : $currentPort"
  Write-Log ""
  $p = Get-Prereqs
  $mark = { param($b) if ($b) { '[OK]     ' } else { '[MISSING]' } }
  Write-Log "$(& $mark $p.Iis) IIS"
  Write-Log "$(& $mark $p.Rewrite) URL Rewrite"
  Write-Log "$(& $mark $p.Arr) Application Request Routing (ARR)"
  Write-Log "$(& $mark $p.Proxy) ARR proxy enabled$(if (-not $p.Proxy) { '  (تطبيق سيفعّله)' })"
  Write-Log ""
  netsh http show urlacl | Select-String 'Archiving|:(\d+)/' | Out-Null

  if (-not $p.Iis) {
    Write-Log "IIS is not installed. Install-WindowsFeature Web-Server -IncludeManagementTools"
  }
  if ($p.Iis -and (-not $p.Rewrite -or -not $p.Arr)) {
    $missing = @()
    if (-not $p.Rewrite) { $missing += 'URL Rewrite' }
    if (-not $p.Arr) { $missing += 'Application Request Routing' }
    if ([System.Windows.Forms.MessageBox]::Show(
        "ينقص IIS: $($missing -join ' + ').`n`nهاتان إضافتان مجانيتان من مايكروسوفت، وبدونهما لا يستطيع IIS تمرير الطلبات.`n`nهل أفتح صفحة التنزيل؟",
        'متطلبات ناقصة', 'YesNo', 'Warning') -eq 'Yes') {
      if (-not $p.Rewrite) { Start-Process 'https://www.iis.net/downloads/microsoft/url-rewrite' }
      if (-not $p.Arr) { Start-Process 'https://www.iis.net/downloads/microsoft/application-request-routing' }
    }
  } elseif ($p.Iis) {
    Write-Log "المتطلبات مكتملة — اضغط تطبيق."
  }
})

# ── تطبيق ──────────────────────────────────────────────────────────────────────────────────────
$btnApply.Add_Click({
  $public = [int]$numPublic.Value
  $internal = [int]$numInternal.Value
  if ($public -eq $internal) {
    [System.Windows.Forms.MessageBox]::Show(
      'المنفذان متطابقان. لا يستطيع IIS والخدمة الاستماع على المنفذ نفسه — وهذا سبب رسالة "used by another process".',
      'منفذ واحد لا يكفي', 'OK', 'Warning') | Out-Null
    return
  }
  if (-not $installFolder) {
    [System.Windows.Forms.MessageBox]::Show('خدمة الأرشفة غير منصّبة على هذا الجهاز. نصّبها أولًا بـ Install.cmd.',
      'غير منصّب', 'OK', 'Error') | Out-Null
    return
  }
  $p = Get-Prereqs
  if (-not $p.Iis -or -not $p.Rewrite -or -not $p.Arr) {
    [System.Windows.Forms.MessageBox]::Show('المتطلبات ناقصة — اضغط «فحص المتطلبات» أولًا.',
      'متطلبات ناقصة', 'OK', 'Warning') | Out-Null
    return
  }

  $answer = [System.Windows.Forms.MessageBox]::Show(
    "سيتم:`n`n" +
    "· نقل الخدمة إلى المنفذ الداخلي $internal (127.0.0.1)`n" +
    "· تحرير حجز المنفذ $public وتسليمه إلى IIS`n" +
    "· إنشاء موقع '$($txtSite.Text)' على المنفذ $public يمرّر إلى الخدمة`n`n" +
    "سيتوقف الوصول للنظام لدقيقة أثناء ذلك. المتابعة؟",
    'تأكيد', 'YesNo', 'Question')
  if ($answer -ne 'Yes') { return }

  $btnApply.Enabled = $false; $btnCheck.Enabled = $false; $btnRevert.Enabled = $false
  $log.Clear()
  try {
    Write-Log "== 1. ARR proxy =="
    if (-not $p.Proxy) {
      # preserveHostHeader: without it ARR replaces Host with 127.0.0.1:<internal>, and anything
      # the app builds from the request address — the desktop-shortcut installer above all —
      # comes out pointing at loopback, which works on the server and nowhere else.
      & $appcmd set config -section:system.webServer/proxy /preserveHostHeader:"True" /commit:apphost 2>&1 | Out-Null
      & $appcmd set config -section:system.webServer/proxy /enabled:"True" /commit:apphost 2>&1 |
        ForEach-Object { Write-Log "$_" }
      Write-Log "proxy enabled"
    } else { Write-Log "already enabled" }

    Write-Log ""
    Write-Log "== 2. service -> internal port $internal =="
    Set-ServicePort -NewPort $internal | Out-Null

    Write-Log ""
    Write-Log "== 3. site =="
    Write-ProxyConfig -Folder $txtPath.Text.Trim() -InternalPort $internal -UploadMb ([int]$numUpload.Value)
    New-ProxySite -Name $txtSite.Text.Trim() -Port $public -Folder $txtPath.Text.Trim()

    if ($chkFirewall.Checked) {
      $rule = "Archiving IIS (TCP $public)"
      if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $rule -Direction Inbound -Protocol TCP -LocalPort $public -Action Allow | Out-Null
        Write-Log "firewall: opened TCP $public"
      }
      # The service no longer listens on the public port, so its own rule is now a hole to nowhere.
      Get-NetFirewallRule -DisplayName "Archiving (TCP $public)" -ErrorAction SilentlyContinue |
        Remove-NetFirewallRule -ErrorAction SilentlyContinue
    }

    if ($chkShortcut.Checked) {
      Write-Log ""
      Write-Log "== desktop shortcut =="
      Update-Shortcut -Port $public
    }

    Write-Log ""
    Write-Log "== 4. end-to-end =="
    $ok = $false
    $deadline = (Get-Date).AddSeconds(40)
    do {
      Start-Sleep -Seconds 2
      try {
        $r = Invoke-WebRequest "http://127.0.0.1:$public/api/public/stats" -UseBasicParsing -TimeoutSec 5
        if ($r.StatusCode -eq 200) { $ok = $true; break }
      } catch { $lastErr = $_.Exception.Message }
    } while ((Get-Date) -lt $deadline)

    $url = "http://$env:COMPUTERNAME`:$public/"
    if ($ok) {
      Write-Log "IIS -> service : 200 OK"
      Write-Log ""
      Write-Log "open: $url"
      if ([System.Windows.Forms.MessageBox]::Show(
          "تم. IIS يقدّم النظام على المنفذ $public ويمرّره إلى الخدمة على $internal.`n`nالعنوان: $url`n`nهل تريد فتحه الآن؟",
          'اكتمل الإعداد', 'YesNo', 'Information') -eq 'Yes') { Start-Process $url }
    } else {
      Write-Log "FAILED through IIS: $lastErr"
      Write-Log ""
      Write-Log "502.3 -> the service is down or on another port"
      Write-Log "403.14 -> the rewrite rule was not read; ARR proxy is off"
      [System.Windows.Forms.MessageBox]::Show(
        "الخدمة تعمل لكن IIS لم يمرّر الطلب.`n`nراجع السجل في النافذة — الأسباب الشائعة مذكورة فيه.",
        'لم يكتمل', 'OK', 'Warning') | Out-Null
    }
  } catch {
    Write-Log "FAILED: $($_.Exception.Message)"
    [System.Windows.Forms.MessageBox]::Show("فشل الإعداد:`n`n$($_.Exception.Message)", 'خطأ', 'OK', 'Error') | Out-Null
  } finally {
    $btnApply.Enabled = $true; $btnCheck.Enabled = $true; $btnRevert.Enabled = $true
  }
})

# ── تراجع ──────────────────────────────────────────────────────────────────────────────────────
# A way back that does not require knowing what was changed: the site goes, and the service takes
# the public port again. Nothing here deletes data.
$btnRevert.Add_Click({
  $public = [int]$numPublic.Value
  if ([System.Windows.Forms.MessageBox]::Show(
      "سيُزال موقع IIS وتعود الخدمة للاستماع مباشرة على المنفذ $public.`n`nالمتابعة؟",
      'تراجع', 'YesNo', 'Question') -ne 'Yes') { return }

  $btnApply.Enabled = $false; $btnCheck.Enabled = $false; $btnRevert.Enabled = $false
  $log.Clear()
  try {
    $name = $txtSite.Text.Trim()
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    if (Get-Website -Name $name -ErrorAction SilentlyContinue) {
      Stop-Website -Name $name -ErrorAction SilentlyContinue
      Remove-Website -Name $name
      Write-Log "removed site '$name'"
    }
    if (Test-Path "IIS:\AppPools\$name") { Remove-WebAppPool -Name $name; Write-Log "removed app pool '$name'" }

    Set-ServicePort -NewPort $public -Public | Out-Null

    $rule = "Archiving (TCP $public)"
    if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
      New-NetFirewallRule -DisplayName $rule -Direction Inbound -Protocol TCP -LocalPort $public -Action Allow | Out-Null
      Write-Log "firewall: opened TCP $public"
    }
    if ($chkShortcut.Checked) { Update-Shortcut -Port $public }

    Write-Log ""
    Write-Log "open: http://$env:COMPUTERNAME`:$public/"
    [System.Windows.Forms.MessageBox]::Show("تم التراجع. النظام يعمل مباشرة على المنفذ $public.",
      'تراجع', 'OK', 'Information') | Out-Null
  } catch {
    Write-Log "FAILED: $($_.Exception.Message)"
    [System.Windows.Forms.MessageBox]::Show("فشل التراجع:`n`n$($_.Exception.Message)", 'خطأ', 'OK', 'Error') | Out-Null
  } finally {
    $btnApply.Enabled = $true; $btnCheck.Enabled = $true; $btnRevert.Enabled = $true
  }
})

if ($SelfTest) {
  $p = Get-Prereqs
  Write-Host "form built: $($form.Controls.Count) controls"
  Write-Host "install='$installFolder' servicePort=$currentPort"
  Write-Host "fields: public=$($numPublic.Value) internal=$($numInternal.Value) site='$($txtSite.Text)' path='$($txtPath.Text)' uploadMb=$($numUpload.Value)"
  Write-Host "prereqs: iis=$($p.Iis) rewrite=$($p.Rewrite) arr=$($p.Arr) proxy=$($p.Proxy)"
  $form.Dispose()
  return
}

[void]$form.ShowDialog()
