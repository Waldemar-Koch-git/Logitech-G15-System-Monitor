# Requires elevation. Creates and removes a separate installation; never runs the dashboard.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$setup = Join-Path $root 'dist\G15-System-Monitor-Setup.exe'
$testDirectory = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'G15 System Monitor Installer QA'
$taskName = 'G15SystemMonitor-Installed'
$uninstallKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{A9D834F0-6998-4788-AD00-851CB961A965}_is1'
$report = Join-Path $root 'dist\installer-test-result.json'
$checks = [Collections.Generic.List[string]]::new()
$ownsInstallation = $false

function Run-Setup([string]$tasks, [string]$log) {
    $process = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',
        ('/DIR="' + $testDirectory + '"'), ('/TASKS="' + $tasks + '"'), ('/LOG="' + (Join-Path $root $log) + '"')) -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Setup exit code: $($process.ExitCode). See $log" }
    $process.Dispose()
}

try {
    if ((Test-Path -LiteralPath $testDirectory) -or (Test-Path $uninstallKey) -or
        (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue)) {
        throw 'Test abgebrochen: Zielinstallation oder Aufgabe existiert bereits.'
    }
    $ownsInstallation = $true
    Run-Setup '' 'dist\installer-install-test.log'
    $task = Get-ScheduledTask -TaskName $taskName
    if ($task.Actions.Execute -ne (Join-Path $testDirectory 'G15Dashboard.exe') -or @($task.Triggers | Where-Object { $null -ne $_ }).Count -ne 0) { throw 'Falsche Startaufgabe.' }
    $checks.Add('Fresh install: correct executable, no autostart')
    foreach ($file in @('G15Dashboard.exe','G15Dashboard.dll','coreclr.dll','hostfxr.dll','System.Windows.Forms.dll','PresentMon.exe')) {
        if ((Get-FileHash (Join-Path $testDirectory $file)).Hash -ne (Get-FileHash (Join-Path $root ('bin\InstallerPayload\' + $file))).Hash) { throw "File mismatch: $file" }
    }
    if (Test-Path (Join-Path $testDirectory 'LogitechLcd.dll')) { throw 'Excluded Logitech DLL was bundled.' }
    $checks.Add('Payload hashes match, .NET included, Logitech DLL excluded')
    $settingsFile = Join-Path $testDirectory 'qa-user-settings.txt'
    Set-Content -LiteralPath $settingsFile -Value 'synthetic-preservation-check'
    Run-Setup 'autostart' 'dist\installer-update-test.log'
    if (@((Get-ScheduledTask -TaskName $taskName).Triggers | Where-Object { $null -ne $_ }).Count -ne 1) { throw 'Autostart trigger missing.' }
    if ((Get-Content -LiteralPath $settingsFile) -ne 'synthetic-preservation-check') { throw 'User file changed.' }
    $checks.Add('Update enables autostart and preserves unrelated file')
    Run-Setup '' 'dist\installer-disable-autostart-test.log'
    if (@((Get-ScheduledTask -TaskName $taskName).Triggers | Where-Object { $null -ne $_ }).Count -ne 0) { throw 'Autostart not removed.' }
    $checks.Add('Update can disable autostart')
} catch {
    $failure = $_.Exception.Message
} finally {
    if ($ownsInstallation -and (Test-Path (Join-Path $testDirectory 'unins000.exe'))) {
        $uninstaller = Start-Process -FilePath (Join-Path $testDirectory 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $root 'dist\installer-uninstall-test.log') + '"')) -WindowStyle Hidden -PassThru -Wait
        if ($uninstaller.ExitCode -ne 0) { $failure = "Deinstallation fehlgeschlagen: $($uninstaller.ExitCode)" }
        $uninstaller.Dispose()
        Start-Sleep -Seconds 2
        if ((Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) -or (Test-Path $uninstallKey) -or
            (Test-Path (Join-Path $testDirectory 'G15Dashboard.exe'))) { $failure = 'Deinstallation unvollständig.' }
        else { $checks.Add('Uninstall removes task, registration and installed executable') }
        if ($settingsFile -and (Test-Path -LiteralPath $settingsFile)) {
            $checks.Add('Uninstall preserves unrelated user file')
            # Delete only this test-owned sentinel; non-recursive removal of the verified QA directory.
            $expected = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'G15 System Monitor Installer QA'
            if ([IO.Path]::GetFullPath($testDirectory) -ne $expected) { throw 'Unexpected cleanup path.' }
            Remove-Item -LiteralPath $settingsFile
            if (-not (Get-ChildItem -LiteralPath $testDirectory -Force)) { Remove-Item -LiteralPath $testDirectory }
        }
    }
    [pscustomobject]@{ Success = -not [bool]$failure; Checks = @($checks.ToArray()); Error = $failure } |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $report -Encoding UTF8
}
if ($failure) { throw $failure }
