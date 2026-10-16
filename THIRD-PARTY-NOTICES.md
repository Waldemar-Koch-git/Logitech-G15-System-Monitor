# Third-party components

The original license texts, package metadata and source references are preserved in [installer/licenses](installer/licenses/). The installer copies that directory into the installed application.

| Component | Included form | License / source |
| --- | --- | --- |
| PresentMon 2.6.0 | Unmodified `tools/PresentMon.exe` | [MIT notice](installer/licenses/PresentMon-LICENSE.txt), [source tag](https://github.com/GameTechDev/PresentMon/tree/v2.6.0) |
| LibreHardwareMonitorLib 0.9.6 | NuGet reference; included in installer | [MPL 2.0](installer/licenses/MPL-2.0.txt), [source commit](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/3d331e3370efb858411f19511373eff65a218701), [additional notices](installer/licenses/LibreHardwareMonitor-THIRD-PARTY-NOTICES.txt) |
| System.Management 10.0.2 and transitive packages | NuGet references; applicable binaries in installer | Package metadata/notices in `installer/licenses/packages/` |
| .NET / Windows Desktop runtime | Included in the self-contained installer | Original notices in `installer/licenses/runtime/` |

[COMPONENTS.txt](installer/licenses/COMPONENTS.txt) records the package graph and source references from the prepared installer build, including package platform variants that are not all shipped. The installer build refreshes this inventory and the available package/runtime notices.

The proprietary LogitechLcd.dll, Logitech Gaming Software and PawnIO are not distributed here. Obtain the required software through its original sources. LogitechLcd.dll must come from an existing suitable LGS installation or a lawfully obtained Logitech SDK.

Inno Setup is an external build tool and is not bundled with the source tree. Python and Pillow are needed only to regenerate the icon and are not bundled. This notice does not assign a license to the project's original code or artwork; see [LICENSE-STATUS.md](LICENSE-STATUS.md).
