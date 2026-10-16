param(
    [Parameter(Mandatory)][ValidateSet('Install','Stop','Remove')][string]$Mode,
    [Parameter(Mandatory)][string]$AppDirectory,
    [ValidateSet('yes','no')][string]$Autostart = 'no',
    [string]$LogPath
)
$ErrorActionPreference = 'Stop'
trap {
    if ($LogPath) { $_ | Out-String | Set-Content -LiteralPath $LogPath -Encoding UTF8 }
    exit 1
}
$appPath = Join-Path ([IO.Path]::GetFullPath($AppDirectory)) 'G15Dashboard.exe'
$taskName = 'G15SystemMonitor-Installed'
$marker = 'G15 System Monitor Setup {A9D834F0-6998-4788-AD00-851CB961A965}'
$scheduler = New-Object -ComObject Schedule.Service
$scheduler.Connect()
$folder = $scheduler.GetFolder('\')
$existing = $null
try { $existing = $folder.GetTask($taskName) }
catch {
    if ($_.Exception.HResult -ne -2147024894 -and $_.Exception.InnerException.HResult -ne -2147024894) { throw }
}
if ($existing -and ($existing.Definition.RegistrationInfo.Description -ne $marker -or
    $existing.Definition.Actions.Count -ne 1 -or $existing.Definition.Actions.Item(1).Path -ne $appPath)) {
    throw 'Eine fremde Aufgabe mit dem Setup-Namen existiert; sie wird nicht verändert.'
}

if ($Mode -in @('Stop','Remove')) {
    $running = @(Get-Process G15Dashboard -ErrorAction SilentlyContinue | Where-Object Path -eq $appPath)
    if ($running.Count -gt 0) {
        $stop = Start-Process -FilePath $appPath -ArgumentList '--shutdown' -WindowStyle Hidden -PassThru
        if (-not $stop.WaitForExit(10000)) { throw 'Der Beenden-Aufruf antwortet nicht.' }
        $stop.Dispose()
        foreach ($app in $running) {
            if (-not $app.WaitForExit(15000)) { throw 'Bitte die installierte App über das Tray-Menü beenden und erneut versuchen.' }
            $app.Dispose()
        }
    }
    if ($Mode -eq 'Remove' -and $existing) { $folder.DeleteTask($taskName, 0) }
    exit 0
}

# Elevated tasks must launch only from the protected installation tree.
$programFilesRoot = [Environment]::GetEnvironmentVariable('ProgramW6432')
if (-not $programFilesRoot) { $programFilesRoot = [Environment]::GetFolderPath('ProgramFiles') }
$programFilesRoot = $programFilesRoot.TrimEnd('\') + '\'
if (-not $appPath.StartsWith($programFilesRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Für den erhöhten Start muss die Anwendung unter Programme installiert sein.'
}
if (-not (Test-Path -LiteralPath $appPath)) { throw 'Installierte Anwendung fehlt.' }
$definition = $scheduler.NewTask(0)
$definition.RegistrationInfo.Description = $marker
$definition.Principal.UserId = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$definition.Principal.LogonType = 3
$definition.Principal.RunLevel = 1
$definition.Settings.Enabled = $true
$definition.Settings.AllowDemandStart = $true
$definition.Settings.DisallowStartIfOnBatteries = $false
$definition.Settings.StopIfGoingOnBatteries = $false
$definition.Settings.ExecutionTimeLimit = 'PT0S'
$definition.Settings.MultipleInstances = 2
if ($Autostart -eq 'yes') {
    $trigger = $definition.Triggers.Create(9)
    $trigger.UserId = $definition.Principal.UserId
    $trigger.Delay = 'PT15S'
}
$action = $definition.Actions.Create(0)
$action.Path = $appPath
$action.WorkingDirectory = $AppDirectory
$null = $folder.RegisterTaskDefinition($taskName, $definition, 6, $definition.Principal.UserId, $null, 3, $null)
$verified = $folder.GetTask($taskName)
if ($verified.Definition.Actions.Item(1).Path -ne $appPath -or
    $verified.Definition.Triggers.Count -ne [int]($Autostart -eq 'yes')) { throw 'Aufgabenprüfung fehlgeschlagen.' }
