# Project instructions

Windows x64 / .NET 8 WinForms dashboard for the monochrome Logitech G15 LCD (160 × 43). Read README.md before changes. Fixed package versions: LibreHardwareMonitorLib 0.9.6 and System.Management 10.0.2. App/setup version: 1.0.0.

- Preserve the display layout, YYYY.MM.DD date format, four equally sized percentage bars, separate temperature numbers and hidden FPS number/label when unavailable. Valid zeros remain visible; other missing sensors use --.
- CPU temperatures must come from CPU hardware. Keep all selected GPU metrics on one device; do not invent VRAM capacity or report shared iGPU memory as dedicated VRAM.
- Keep setup English-first with all twelve documented languages. Preserve AppId, task name, protected install directory and optional autostart disabled by default. Do not bundle proprietary Logitech software or drivers. Do not overwrite unrelated tasks or delete unrelated shortcuts/files.
- Keep third-party notices and source references. Do not assign a project license without the owner's decision.
- Use the documented build and console tests. Starting the dashboard can trigger UAC, sensors/drivers and ETW; the administrative installer test changes the system. Neither is a routine build verification command.
- Do not change the user's installation, scheduled tasks, LGS settings or running app without a corresponding request. Do not publish local logs or machine/account-specific information.
- Document changed behavior and actual verification in README.md and CHANGELOG.md. Distinguish synthetic tests from physical hardware and installation checks; do not claim native-speaker review or tests not performed.
