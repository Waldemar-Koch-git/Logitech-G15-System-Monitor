param([string]$Compiler = (Join-Path $PSScriptRoot '..\tools\installer-cache\InnoSetup\ISCC.exe'))
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$payload = Join-Path $root 'bin\InstallerPayload'
if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Inno Setup ISCC.exe fehlt; mit -Compiler den Pfad angeben.' }
& dotnet publish (Join-Path $root 'G15Dashboard.csproj') -c Release -r win-x64 --self-contained true -o $payload
if ($LASTEXITCODE -ne 0) { throw 'Publish fehlgeschlagen.' }
# Include package metadata, source references and available original license texts.
$assets = Get-Content (Join-Path $root 'obj\project.assets.json') -Raw | ConvertFrom-Json
$packageRoot = @($assets.packageFolders.PSObject.Properties)[0].Name
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('Unmodified third-party components and package graph (including platform variants not bundled). Licenses and source repositories according to NuGet metadata:')
foreach ($entry in $assets.libraries.PSObject.Properties) {
    if ($entry.Value.type -ne 'package') { continue }
    $directory = Join-Path $packageRoot $entry.Value.path
    $nuspec = Get-ChildItem -LiteralPath $directory -Filter '*.nuspec' | Select-Object -First 1
    if (-not $nuspec) { throw "Paketmetadaten fehlen: $($entry.Name)" }
    [xml]$metadata = Get-Content -LiteralPath $nuspec.FullName
    $package = $metadata.package.metadata
    $licenseDirectory = Join-Path $PSScriptRoot ('licenses\packages\' + $entry.Value.path.Replace('/','-'))
    $null = New-Item -ItemType Directory -Path $licenseDirectory -Force
    Copy-Item -LiteralPath $nuspec.FullName -Destination $licenseDirectory -Force
    Get-ChildItem -LiteralPath $directory -File | Where-Object { $_.Name -match '^(LICENSE|COPYING|NOTICE|THIRD-PARTY)' } |
        Copy-Item -Destination $licenseDirectory -Force
    $lines.Add("$($entry.Name): $($package.license.InnerText) $($package.licenseUrl)")
    if ($package.repository.url) { $lines.Add("Source: $($package.repository.url) commit $($package.repository.commit)") }
    elseif ($package.projectUrl) { $lines.Add("Project/source: $($package.projectUrl)") }
}
$runtimeConfig = Get-Content (Join-Path $payload 'G15Dashboard.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
    $packId = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $packDirectory = Join-Path $packageRoot ($packId + '\' + $framework.version)
    $destination = Join-Path $PSScriptRoot ('licenses\runtime\' + $packId + '-' + $framework.version)
    $null = New-Item -ItemType Directory -Path $destination -Force
    $licenseFiles = @(Get-ChildItem -LiteralPath $packDirectory -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY)' })
    if (-not $licenseFiles) { throw "Laufzeit-Lizenzen fehlen: $packId" }
    $licenseFiles | Copy-Item -Destination $destination -Force
    $lines.Add("Bundled runtime: $($framework.name) $($framework.version); source https://github.com/dotnet")
}
$lines.Add('LibreHardwareMonitorLib source: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/3d331e3370efb858411f19511373eff65a218701')
$lines.Add('PresentMon 2.6.0 source: https://github.com/GameTechDev/PresentMon/tree/v2.6.0')
$lines.Add('LogitechLcd.dll is not redistributed in this installer; the existing Logitech installation supplies it.')
$lines | Set-Content (Join-Path $PSScriptRoot 'licenses\COMPONENTS.txt') -Encoding UTF8
& $Compiler ('/DPayloadDir=' + $payload) (Join-Path $PSScriptRoot 'G15SystemMonitor.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer-Kompilierung fehlgeschlagen.' }
$setup = Join-Path $root 'dist\G15-System-Monitor-Setup.exe'
Get-FileHash -LiteralPath $setup -Algorithm SHA256 | Format-List
