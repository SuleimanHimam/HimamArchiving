<#
.SYNOPSIS
  The install wizard, as a window: fill in the SQL Server details, press تنصيب.

.DESCRIPTION
  A form over install-service.ps1 for people who do not want to type a command line. Everything it
  does, that script does — this only collects the answers, checks them before committing, and shows
  what happened. Launch it by double-clicking Install.cmd, which handles the elevation prompt.

  Windows ships no other scripting or installer engine that is present on every server, so the
  engine underneath is still PowerShell; the point is that nobody has to open a console or type a
  parameter name.

.PARAMETER SelfTest
  Build the window and exit without showing it. Used to check the script still constructs after an
  edit, since a form cannot be driven headlessly.
#>
[CmdletBinding()]
param([switch] $SelfTest)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$here = $PSScriptRoot
$installer = Join-Path $here 'install-service.ps1'

# ── local SQL Server instances, so the commonest field is a pick and not a guess ───────────────
function Get-LocalSqlInstances {
  $key = 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL'
  if (-not (Test-Path $key)) { return @() }
  $names = (Get-ItemProperty $key).PSObject.Properties |
    Where-Object { $_.Name -notlike 'PS*' } | Select-Object -ExpandProperty Name
  # MSSQLSERVER is the default instance and is addressed as '.', not '.\MSSQLSERVER'.
  $names | ForEach-Object { if ($_ -eq 'MSSQLSERVER') { '.' } else { ".\$_" } }
}

# Is the database somewhere other than this machine? Everything before a '\' (instance) or ',' (port)
# is the host, and only a handful of spellings mean "here".
function Test-IsRemoteServer {
  param([string] $Server)
  $s = $Server.Trim()
  if (-not $s) { return $false }
  $hostPart = ($s -split '[\\,]')[0].Trim()
  return $hostPart -notin @('.', '(local)', 'localhost', '127.0.0.1', '::1', $env:COMPUTERNAME)
}

function New-ConnectionString {
  param([string] $Server, [string] $Database, [bool] $WindowsAuth, [string] $User, [string] $Password)
  $auth = if ($WindowsAuth) { 'Integrated Security=true' } else { "User ID=$User;Password=$Password" }
  "Server=$Server;Database=$Database;$auth;TrustServerCertificate=true;MultipleActiveResultSets=true;"
}

# ── the window ────────────────────────────────────────────────────────────────────────────────
$form = New-Object System.Windows.Forms.Form
$form.Text = 'تنصيب نظام الأرشفة — الديوان الإلكتروني'
$form.Size = New-Object System.Drawing.Size(720, 672)
$form.StartPosition = 'CenterScreen'
$form.RightToLeft = 'Yes'
$form.RightToLeftLayout = $true
$form.Font = New-Object System.Drawing.Font('Segoe UI', 10)
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false

function Add-Label { param($text, $y)
  $l = New-Object System.Windows.Forms.Label
  $l.Text = $text; $l.Location = New-Object System.Drawing.Point(20, $y)
  $l.Size = New-Object System.Drawing.Size(180, 24); $l.TextAlign = 'MiddleRight'
  $form.Controls.Add($l); $l
}
function Add-Box { param($y, $text = '', $width = 440)
  $t = New-Object System.Windows.Forms.TextBox
  $t.Location = New-Object System.Drawing.Point(210, $y)
  $t.Size = New-Object System.Drawing.Size($width, 24); $t.Text = $text
  $form.Controls.Add($t); $t
}

$y = 20
Add-Label 'خادم SQL Server' $y | Out-Null
$cboServer = New-Object System.Windows.Forms.ComboBox
$cboServer.Location = New-Object System.Drawing.Point(210, $y)
$cboServer.Size = New-Object System.Drawing.Size(440, 24)
$cboServer.DropDownStyle = 'DropDown'      # editable: a remote server is typed, not listed
foreach ($i in Get-LocalSqlInstances) { [void]$cboServer.Items.Add($i) }
if ($cboServer.Items.Count) { $cboServer.SelectedIndex = 0 } else { $cboServer.Text = '.\SQLEXPRESS' }
$form.Controls.Add($cboServer)

# The list only knows about instances on this machine. A database server elsewhere on the network is
# typed in — by name or by address — and the field is editable for exactly that, so say so rather
# than leaving people to guess whether the dropdown is the only choice.
$y += 26
$lblServerHint = New-Object System.Windows.Forms.Label
$lblServerHint.Text = 'القائمة تعرض نسخ هذا الجهاز — ولخادم على الشبكة اكتب عنوانه: 192.168.1.10 أو 192.168.1.10,1433 أو SQL01\PROD'
$lblServerHint.ForeColor = [System.Drawing.Color]::DimGray
$lblServerHint.Font = New-Object System.Drawing.Font('Segoe UI', 8)
$lblServerHint.Location = New-Object System.Drawing.Point(210, $y)
$lblServerHint.Size = New-Object System.Drawing.Size(440, 18)
$form.Controls.Add($lblServerHint)

$y += 34
Add-Label 'اسم قاعدة البيانات' $y | Out-Null
$txtDb = Add-Box $y 'Archiving'

$y += 40
$grpAuth = New-Object System.Windows.Forms.GroupBox
$grpAuth.Text = 'نوع المصادقة'
$grpAuth.Location = New-Object System.Drawing.Point(20, $y)
$grpAuth.Size = New-Object System.Drawing.Size(630, 120)
$grpAuth.RightToLeft = 'Yes'
$form.Controls.Add($grpAuth)

$rbWin = New-Object System.Windows.Forms.RadioButton
$rbWin.Text = 'مصادقة ويندوز (مستحسن — بلا كلمة مرور)'
$rbWin.Location = New-Object System.Drawing.Point(20, 26)
$rbWin.Size = New-Object System.Drawing.Size(380, 24); $rbWin.Checked = $true
$grpAuth.Controls.Add($rbWin)

$rbSql = New-Object System.Windows.Forms.RadioButton
$rbSql.Text = 'حساب SQL'
$rbSql.Location = New-Object System.Drawing.Point(20, 54)
$rbSql.Size = New-Object System.Drawing.Size(140, 24)
$grpAuth.Controls.Add($rbSql)

$lblUser = New-Object System.Windows.Forms.Label
$lblUser.Text = 'المستخدم'; $lblUser.Location = New-Object System.Drawing.Point(170, 56)
$lblUser.Size = New-Object System.Drawing.Size(70, 22); $lblUser.TextAlign = 'MiddleRight'
$grpAuth.Controls.Add($lblUser)
$txtUser = New-Object System.Windows.Forms.TextBox
$txtUser.Location = New-Object System.Drawing.Point(245, 54)
$txtUser.Size = New-Object System.Drawing.Size(150, 24); $txtUser.Enabled = $false
$grpAuth.Controls.Add($txtUser)

$lblPwd = New-Object System.Windows.Forms.Label
$lblPwd.Text = 'كلمة المرور'; $lblPwd.Location = New-Object System.Drawing.Point(405, 56)
$lblPwd.Size = New-Object System.Drawing.Size(80, 22); $lblPwd.TextAlign = 'MiddleRight'
$grpAuth.Controls.Add($lblPwd)
$txtPwd = New-Object System.Windows.Forms.TextBox
$txtPwd.Location = New-Object System.Drawing.Point(490, 54)
$txtPwd.Size = New-Object System.Drawing.Size(120, 24)
$txtPwd.UseSystemPasswordChar = $true; $txtPwd.Enabled = $false
$grpAuth.Controls.Add($txtPwd)

$lblAcct = New-Object System.Windows.Forms.Label
$lblAcct.Text = 'حساب الخدمة'; $lblAcct.Location = New-Object System.Drawing.Point(20, 88)
$lblAcct.Size = New-Object System.Drawing.Size(100, 22); $lblAcct.TextAlign = 'MiddleRight'
$grpAuth.Controls.Add($lblAcct)
$txtAcct = New-Object System.Windows.Forms.TextBox
$txtAcct.Location = New-Object System.Drawing.Point(130, 86)
$txtAcct.Size = New-Object System.Drawing.Size(480, 24)
$txtAcct.Text = 'NT AUTHORITY\NetworkService'
$grpAuth.Controls.Add($txtAcct)

$toggleAuth = {
  $txtUser.Enabled = $rbSql.Checked
  $txtPwd.Enabled  = $rbSql.Checked
}
$rbWin.Add_CheckedChanged($toggleAuth)
$rbSql.Add_CheckedChanged($toggleAuth)

$y += 132
Add-Label 'مجلد حفظ الوثائق' $y | Out-Null
$txtStorage = Add-Box $y 'D:\ArchivingData\storage' 350
$btnBrowse = New-Object System.Windows.Forms.Button
$btnBrowse.Text = 'استعراض…'
$btnBrowse.Location = New-Object System.Drawing.Point(565, ($y - 1))
$btnBrowse.Size = New-Object System.Drawing.Size(85, 26)
$btnBrowse.Add_Click({
  $d = New-Object System.Windows.Forms.FolderBrowserDialog
  $d.Description = 'اختر مجلد حفظ الوثائق — على قرص فيه سعة للنمو'
  if ($d.ShowDialog() -eq 'OK') { $txtStorage.Text = $d.SelectedPath }
})
$form.Controls.Add($btnBrowse)

$y += 34
Add-Label 'منفذ الموقع' $y | Out-Null
$numPort = New-Object System.Windows.Forms.NumericUpDown
$numPort.Location = New-Object System.Drawing.Point(210, $y)
$numPort.Size = New-Object System.Drawing.Size(100, 24)
$numPort.Minimum = 1; $numPort.Maximum = 65535; $numPort.Value = 7070
$form.Controls.Add($numPort)

<#
  Re-running the wizard on a machine that already has this installed must start from what that
  install actually uses, not from the defaults. Otherwise someone who chose port 7070, opened the
  wizard again to change one unrelated thing, and pressed تنصيب would silently be moved back to
  8080 — and the installer would take the 7070 firewall rule down on the way out.
#>
$existingSettings = Join-Path $here 'appsettings.Production.json'
if (Test-Path $existingSettings) {
  try {
    $prev = Get-Content $existingSettings -Raw | ConvertFrom-Json
    if ($prev.Urls -match ':(\d+)') { $numPort.Value = [int]$Matches[1] }
    if ($prev.Storage.RootPath) { $txtStorage.Text = $prev.Storage.RootPath }
    $pcs = $prev.ConnectionStrings.Default
    if ($pcs -match '(?i)(?:database|initial catalog)\s*=\s*([^;]+)') { $txtDb.Text = $Matches[1].Trim() }
    if ($pcs -match '(?i)(?:server|data source)\s*=\s*([^;]+)')       { $cboServer.Text = $Matches[1].Trim() }
    if ($pcs -match '(?i)user\s*id\s*=\s*([^;]+)') {
      $rbSql.Checked = $true
      $txtUser.Text = $Matches[1].Trim()
    }
    $form.Text += ' — تحديث تنصيب قائم'
  } catch { }   # a settings file we cannot read is not a reason to refuse to install
}

$y += 40
$log = New-Object System.Windows.Forms.TextBox
$log.Location = New-Object System.Drawing.Point(20, $y)
$log.Size = New-Object System.Drawing.Size(630, 230)
$log.Multiline = $true; $log.ScrollBars = 'Vertical'; $log.ReadOnly = $true
$log.RightToLeft = 'No'      # the script's own output is English paths and SQL messages
$log.Font = New-Object System.Drawing.Font('Consolas', 9)
$form.Controls.Add($log)

$y += 240
$btnTest = New-Object System.Windows.Forms.Button
$btnTest.Text = 'اختبار الاتصال'
$btnTest.Location = New-Object System.Drawing.Point(430, $y)
$btnTest.Size = New-Object System.Drawing.Size(120, 32)
$form.Controls.Add($btnTest)

$btnInstall = New-Object System.Windows.Forms.Button
$btnInstall.Text = 'تنصيب'
$btnInstall.Location = New-Object System.Drawing.Point(560, $y)
$btnInstall.Size = New-Object System.Drawing.Size(90, 32)
$btnInstall.BackColor = [System.Drawing.Color]::FromArgb(0, 90, 158)
$btnInstall.ForeColor = [System.Drawing.Color]::White
$form.Controls.Add($btnInstall)
$form.AcceptButton = $btnInstall

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

# ── test the connection before committing to anything ─────────────────────────────────────────
$btnTest.Add_Click({
  $log.Clear()
  # 'master', not the target: the point of the test is whether the server answers, and the
  # database itself may not exist yet — that is the installer's job.
  $cs = New-ConnectionString -Server $cboServer.Text -Database 'master' `
        -WindowsAuth $rbWin.Checked -User $txtUser.Text -Password $txtPwd.Text
  Write-Log "testing: $($cboServer.Text) ..."
  try {
    $conn = New-Object System.Data.SqlClient.SqlConnection ("$cs;Connect Timeout=8;")
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = 'SELECT @@VERSION'
    $ver = [string]$cmd.ExecuteScalar()
    $conn.Close()
    Write-Log 'OK'
    Write-Log ($ver -split "`n")[0]
    [System.Windows.Forms.MessageBox]::Show('تم الاتصال بخادم قاعدة البيانات بنجاح.', 'اختبار الاتصال',
      'OK', 'Information') | Out-Null
  } catch {
    Write-Log "FAILED: $($_.Exception.Message)"
    [System.Windows.Forms.MessageBox]::Show(
      "تعذّر الاتصال:`n`n$($_.Exception.Message)", 'اختبار الاتصال', 'OK', 'Error') | Out-Null
  }
})

# ── install ───────────────────────────────────────────────────────────────────────────────────
$btnInstall.Add_Click({
  if ($rbSql.Checked -and -not $txtUser.Text.Trim()) {
    [System.Windows.Forms.MessageBox]::Show('أدخل اسم مستخدم SQL.', 'بيانات ناقصة', 'OK', 'Warning') | Out-Null
    return
  }
  <#
    Windows authentication against a database server on another machine does not connect as "the
    service" — it connects as the machine account, DOMAIN\THISSERVER$. That works only inside one
    domain and only once someone has granted that account access, which is not what people expect
    when they pick the option labelled "recommended". Said before the install, not after it fails.
  #>
  if ($rbWin.Checked -and (Test-IsRemoteServer $cboServer.Text) -and
      $txtAcct.Text -match '(?i)NetworkService|LocalSystem') {
    $go = [System.Windows.Forms.MessageBox]::Show(
      "خادم قاعدة البيانات على جهاز آخر ($($cboServer.Text))، ومصادقة ويندوز مختارة مع حساب $($txtAcct.Text).`n`n" +
      "في هذه الحالة يتصل النظام بهوية حساب الجهاز ‏($env:COMPUTERNAME`$)‎ — ولن ينجح إلا داخل نطاق " +
      "واحد وبعد منح ذلك الحساب صلاحية على القاعدة.`n`n" +
      "الأنسب لخادم بعيد: «حساب SQL» باسم مستخدم وكلمة مرور، أو حساب نطاق في خانة «حساب الخدمة».`n`n" +
      "هل تريد المتابعة رغم ذلك؟",
      'مصادقة عبر الشبكة', 'YesNo', 'Warning')
    if ($go -ne 'Yes') { return }
  }

  $answer = [System.Windows.Forms.MessageBox]::Show(
    "سيتم تنصيب الخدمة بالإعدادات التالية:`n`n" +
    "الخادم: $($cboServer.Text)`n" +
    "قاعدة البيانات: $($txtDb.Text)`n" +
    "المنفذ: $($numPort.Value)`n" +
    "مجلد الوثائق: $($txtStorage.Text)`n`nهل تريد المتابعة؟",
    'تأكيد التنصيب', 'YesNo', 'Question')
  if ($answer -ne 'Yes') { return }

  $btnInstall.Enabled = $false; $btnTest.Enabled = $false
  $log.Clear()
  Write-Log 'installing ...'
  try {
    $p = @{
      Provider       = 'SqlServer'
      SqlServer      = $cboServer.Text
      Database       = $txtDb.Text
      Port           = [int]$numPort.Value
      ServiceAccount = $txtAcct.Text
    }
    if ($txtStorage.Text.Trim()) { $p.StoragePath = $txtStorage.Text.Trim() }
    if ($rbSql.Checked) { $p.SqlUser = $txtUser.Text; $p.SqlPassword = $txtPwd.Text }

    # The installer writes to the host; capture every stream so nothing is lost behind the window.
    & $installer @p *>&1 | ForEach-Object { Write-Log "$_" }

    $url = "http://$env:COMPUTERNAME`:$($numPort.Value)/"
    Write-Log ''
    Write-Log "done -> $url"
    if ([System.Windows.Forms.MessageBox]::Show(
        "تم التنصيب.`n`nالعنوان: $url`nاسم المستخدم: admin`nكلمة المرور: Admin@12345`n`n" +
        "غيّر كلمة المرور فورًا.`n`nهل تريد فتح الموقع الآن؟",
        'اكتمل التنصيب', 'YesNo', 'Information') -eq 'Yes') {
      Start-Process $url
    }
  } catch {
    Write-Log "FAILED: $($_.Exception.Message)"
    [System.Windows.Forms.MessageBox]::Show(
      "فشل التنصيب:`n`n$($_.Exception.Message)`n`nراجع السجل في النافذة.",
      'خطأ', 'OK', 'Error') | Out-Null
  } finally {
    $btnInstall.Enabled = $true; $btnTest.Enabled = $true
  }
})

if ($SelfTest) {
  Write-Host "form built: $($form.Controls.Count) controls, SQL instances found: $((Get-LocalSqlInstances) -join ', ')"
  Write-Host "fields: server='$($cboServer.Text)' db='$($txtDb.Text)' port=$($numPort.Value) storage='$($txtStorage.Text)' sqlAuth=$($rbSql.Checked)"
  $form.Dispose()
  return
}

if (-not (Test-Path $installer)) {
  [System.Windows.Forms.MessageBox]::Show(
    "install-service.ps1 غير موجود بجانب هذا الملف.`nشغّل الأداة من داخل مجلد الحزمة.",
    'ملف ناقص', 'OK', 'Error') | Out-Null
  return
}

[void]$form.ShowDialog()
