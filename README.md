> Hint: Made with AI! `ChatGPT 6.1 SOL High`
![Preview](./assets/Logitech%20G15%20LCD-Display.png)

# Logitech G15 System Monitor

![Application icon](assets/G15SystemMonitor.png)



A Windows x64 dashboard for the monochrome **160 × 43 Logitech G15 LCD**. Shows date, time, CPU/GPU usage and temperatures, RAM/VRAM usage, and FPS for the foreground application. Updates once per second and provides a four-times-size preview when no LCD is available.

Version **1.0.0**. Built with **.NET 8**, **LibreHardwareMonitorLib 0.9.6** and **System.Management 10.0.2**. This is an independent project, not an official Logitech product.

## Install

Use `G15-System-Monitor-Setup.exe` from the project's release assets. The self-contained installer includes the .NET runtime and installs under Program Files. It requires administrator privileges and is currently unsigned.

- Windows 10/11 x64 is required.
- The physical LCD requires Logitech Gaming Software (LGS) with LCD/GamePanel support and a matching x64 `LogitechLcd.dll`. The app searches the existing LGS installation. If necessary, a lawfully obtained x64 SDK DLL can be placed next to `G15Dashboard.exe`. Logitech software and this DLL are not bundled.
- Some sensors require administrator privileges and PawnIO. Missing drivers are not installed automatically. See the [LibreHardwareMonitor project](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) for information.
- PresentMon 2.6.0 is included for FPS capture through Windows ETW.

Setup offers **English by default**, plus German, French, Spanish, Italian, Portuguese (Portugal), Dutch, Danish, Swedish, Norwegian (Bokmål), Finnish and Russian. The selection covers setup, its custom messages and notes, and uninstallation. The application's existing tray and diagnostic interface remains German. Native-speaker and visual review of the additional installer translations is pending.

For a direct language selection, use `/LANG=` followed by `english`, `german`, `french`, `spanish`, `italian`, `portuguese`, `dutch`, `danish`, `swedish`, `norwegian`, `finnish` or `russian`. Example:

```powershell
.\G15-System-Monitor-Setup.exe /LANG=french
```

A dedicated scheduled task, `G15SystemMonitor-Installed`, starts the app with elevated rights. **Autostart is optional and disabled by default.** The task and its shortcuts apply to the administrator account running setup; if another administrator's credentials are supplied, they apply to that account. Autostart uses a 15-second delay after sign-in. Other accounts can launch the executable directly with UAC confirmation.

Close older dashboard versions through their tray menus before starting the installed version. Running setup again updates the installation. Uninstallation removes setup-owned files, shortcuts and its task; LGS, PawnIO and manually added files are preserved. Existing development tasks are not migrated or removed.

## Display and controls

The top row shows the local date as **YYYY.MM.DD**, a centered clock and the FPS label when valid. Below it are **RAM, VRAM, CPU and GPU**, each with an equally sized 0–100% bar. Large FPS digits occupy the upper right area; CPU/GPU temperatures appear as numbers in their respective rows.

- Missing sensor values appear as `--`; a valid zero remains visible.
- Without a valid FPS value, both the FPS number and label disappear.
- There are no temperature warning or shutdown functions.
- Right-click the tray icon to show/hide the preview, open sensor diagnostics, or quit. **F2** in the preview also opens diagnostics.
- The app retries LCD connection every five seconds. Quitting stops its own PresentMon session and closes sensor access.
- No frame-capture files are written; PresentMon output is read through a pipe.

## Build from source

Run these commands from this repository's root on Windows with the **.NET 8 SDK**:

```powershell
dotnet restore tests/G15Dashboard.Tests.csproj
dotnet build G15Dashboard.csproj --no-restore -c Release -o bin/CodeReview
dotnet run --project tests/G15Dashboard.Tests.csproj -c Release --no-restore
```

`tools/PresentMon.exe`, its original notice, and the application icon are included. NuGet restores managed dependencies. The proprietary Logitech SDK is optional for compilation: when a local `LCD-SDK/Lib/x64/LogitechLcd.dll` exists, the build copies it; otherwise the app uses the installed LGS library at runtime or falls back to its preview.

To launch the actual dashboard after building:

```powershell
.\bin\CodeReview\G15Dashboard.exe
```

Launching the app can request UAC, access sensors/drivers and start an ETW session. Framework-dependent development builds require the .NET 8 Desktop Runtime. To distribute one, copy the complete output directory, not just the EXE.

## Build the installer

Install Inno Setup separately; the prepared setup was compiled with **Inno Setup 7.1.0**. Pass the path to its compiler:

```powershell
.\installer\Build-Installer.ps1 -Compiler 'C:\Path\To\Inno Setup\ISCC.exe'
```

The script publishes a self-contained win-x64 payload, collects third-party notices, and writes `dist/G15-System-Monitor-Setup.exe`. It does not install the program. Without `-Compiler`, it looks for `tools/installer-cache/InnoSetup/ISCC.exe`; the compiler cache is not included. Observe the build tool's own license terms.

English/German installation notes are in `installer/Information.txt` and `installer/Hinweise.txt`. The ten additional languages have custom `.isl` messages and `.txt` notes in `installer/languages/`. Both standard and project-specific texts are included in each offered installer language. Original third-party licenses are not translated.

`installer/Test-Installer.ps1` is a separate **administrative installation test that changes the system**. It refuses an existing installation/task, creates a separate QA installation, checks update/autostart behavior and uninstalls it. It does not run the dashboard. Do not treat it as a side-effect-free test.

## Tests and known limits

The 20-check console runner tests synthetic sensor mapping, VRAM units, missing/zero values, GPU isolation, rolling FPS windows, malformed CSV handling, rendering and LCD byte conversion, plus a read-only Windows RAM query. It does not start the dashboard, PresentMon or sensor drivers.

The prepared source copy was restored, built and tested successfully without a local Logitech SDK, and its installer was compiled using an external Inno Setup installation. NuGet reported NU1900 because vulnerability data could not be retrieved in the preparation environment; this does not constitute a successful package vulnerability audit.

The project was originally used on AMD hardware. Intel/NVIDIA/hybrid systems and every sensor model have not been validated on real hardware. On multi-GPU systems, the first GPU with valid non-negative data is selected; values are not mixed across devices, and there is no automatic association with the foreground game's GPU. Shared iGPU RAM is not reported as dedicated VRAM without suitable capacity data.

FPS uses displayed-frame intervals over the latest second and the most active valid swapchain of the foreground process. Availability depends on Windows permissions, the application and presentation mode. FPS is not the monitor's refresh rate. The capture process does not restart automatically after exit.

The source and installer have automated build/test evidence. Physical LCD reconnection, complete UAC/ETW lifecycle, real sign-out/sign-in autostart and all installation account scenarios still require target-system testing. The additional installer languages have not been installed or visually checked in every language.

## Source map

| File / directory | Purpose |
| --- | --- |
| `Program.cs` | Startup, instance guard, tray, preview and shutdown |
| `DashboardRenderer.cs` | LCD/preview drawing and pixel font |
| `HardwareReader.cs`, `PhysicalMemory.cs` | Hardware sensor mapping and Windows RAM |
| `FpsReader.cs`, `PresentMonCsvReader.cs`, `FpsWindow.cs` | PresentMon capture and FPS processing |
| `LcdSdk.cs` | Native Logitech integration and reconnection |
| `tests/` | Standalone console regression runner |
| `installer/` | Installer, task integration, translations and license notices |
| `assets/`, `tools/Build-Icon.py` | Icon assets and optional Python/Pillow regeneration |

## License status

A project license for the original source/artwork has not yet been selected. See [LICENSE-STATUS.md](LICENSE-STATUS.md). Third-party license texts and source references are preserved in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and `installer/licenses/`.
