# PLCSimUnity

PLCSimUnity is a Windows .NET 4.8 bridge application for Siemens PLCSIM Advanced. It provides a WinForms control center, maintains a 50 Hz shared-memory I/O cycle, exposes live PLC/HMI telemetry, and includes a WinCC Runtime screen capture monitor.

## Components

- **WinForms dashboard** - discover PLC instances, select a target, enable auto-discovery, and view system telemetry.
- **PLCSIM Advanced worker** - connects to the selected runtime instance and transfers 64-byte input/output buffers.
- **Shared memory** - provides a Unity-facing named memory region for I/O and connection state.
- **WinCC Runtime monitor** - detects supported HMI Runtime processes and captures a 1920x1080 RGBA frame stream.
- **CDP WebView sources** - optional Unity package sources for rendering a web-based HMI with Chrome, Edge, or Chromium.

## Requirements

- Windows x64
- .NET Framework 4.8
- Visual Studio 2022 or later with .NET desktop development tools
- Siemens PLCSIM Advanced Runtime/API 5.0 or compatible installation
- NuGet package `Newtonsoft.Json 13.0.3`

The project references the Siemens API DLL through an absolute path in `PLCSimUnity.csproj`:

```text
C:\Program Files (x86)\Common Files\Siemens\PLCSIMADV\API\5.0\Siemens.Simatic.Simulation.Runtime.Api.x64.dll
```

Update that path if your PLCSIM Advanced API is installed elsewhere.

## Build and Run

1. Restore packages and solution dependencies in Visual Studio.
2. Open `PLCSimUnity.slnx`.
3. Set `PLCSimUnity` as the startup project.
4. Build as **Debug|x64**.
5. Run as a normal user or administrator as required by the local PLCSIM Runtime Manager.

Command-line options:

```text
--shm <name>       Named shared-memory region (default: PLCSimUnity_SharedMemory)
--instance <name>  Initial PLC instance name
--inputs <count>   Input buffer size; default and current runtime logic use 64 bytes
--outputs <count>  Output buffer size; default and current runtime logic use 64 bytes
```

Examples:

```powershell
PLCSimUnity.exe
PLCSimUnity.exe --instance "S7-1500-Lab"
PLCSimUnity.exe --instance "S7-1200-Line-1" --shm "Unity_PLC_A" --inputs 64 --outputs 64
PLCSimUnity.exe --instance "AutoDiscover"
```

An instance name that is empty or `AutoDiscover` starts auto-discovery behavior.

## Dashboard

The control center has three areas:

1. **PLC telemetry** - service status, active instance, heartbeat, and operating state.
2. **Unity telemetry** - detects activity at Unity heartbeat offset 100 within a two-second window.
3. **WinCC HMI telemetry** - reports runtime process PID and captured frame count.

Instance operations:

- Use **Search / Filter** to filter discovered instances by name or CPU type.
- Select a row and choose **Connect to Selected Instance**.
- Double-click a row to connect to it.
- Choose **Enable Auto-Discover Mode** to let the worker select a running instance or its first discovered fallback.
- **Refresh** or the three-second scan timer reloads the instance list.

## Shared-Memory Layout

The default region name is `PLCSimUnity_SharedMemory`. The header is 128 bytes.

| Offset | Size | Name | Description |
|---:|---:|---|---|
| 0 | 1 | Status | `ServiceStatus` enum |
| 1 | 1 | State | PLC operating state/error state |
| 2 | 1 | Heartbeat | Incremented by the 50 Hz worker |
| 4 | 4 | InputBytesCount | Configured input buffer size |
| 8 | 4 | OutputBytesCount | Configured output buffer size |
| 16 | 4 | Instance name length | 0 for auto-discovery |
| 20 | 44 | Instance name | UTF-8 target instance name |
| 64 | 1 | Runtime screen status | `RuntimeScreenStatus` enum |
| 66 | 4 | Browser/process PID | WinCC process ID |
| 74 | 4 | Screen capture frame rate | Reported capture rate |
| 82 | 4 | Total frames captured | Capture counter |
| 128 | 64 | Unity inputs | Unity-to-PLCSIM `InputArea` |
| 192 | 64 | PLCSIM outputs | PLCSIM `OutputArea`-to-Unity |
| 256 | up to 8,294,400 | HMI frame | 1920x1080 BGRA/RGBA capture data |

For custom shared-memory names, Unity and PLCSimUnity must use the same `--shm` name.

## PLC Status Values

| Value | Status |
|---:|---|
| 0 | Uninitialized |
| 1 | PLCSIM Runtime Manager not available |
| 2 | Selected instance not found |
| 3 | Selected instance not in RUN |
| 4 | Connected |
| 5 | Error |

## HMI Integration

The desktop application monitors `HmiRTm`, `Siemens.Simatic.Hmi.Runtime`, and `HmiKpa` processes. It reports a connected runtime and writes captured frames at approximately 30 Hz into shared memory.

The Unity CDP WebView package is maintained under `HMIViewpoint/com.HMIViewport.cdpwebview/` and can render a live web URL on a UI panel or 3D object. It is separate from the native WinCC process/frame-capture monitor.

See `HMIViewpoint/com.HMIViewport.cdpwebview/README.md` for package installation and Unity usage.

## Troubleshooting

- **No instances displayed:** verify the PLCSIM Advanced Runtime Manager service and registered instances.
- **Selected instance reports not running:** start it in PLCSIM Advanced.
- **Unity telemetry disconnected:** verify Unity writes or updates the heartbeat at offset 100.
- **HMI disconnected:** confirm a supported WinCC Runtime process is running.
- **Shared-memory connection failure:** keep exactly one application instance per custom memory name and close stale processes.
- **Missing Siemens API:** update the project reference or install the matching PLCSIM Advanced API SDK.

## Source Map

- `Properties/Program.cs` - startup, arguments, shared memory, and dashboard lifecycle
- `Properties/InteractiveInstanceSelector.cs` - 50 Hz PLC worker
- `Properties/InstanceScan.cs` - runtime instance discovery
- `Properties/SharedMemoryProtocol.cs` - enums, offsets, and protocol constants
- `PLCSimUnityUI/DashboardForm.cs` - WinForms control center
- `HMIViewpoint/WinCCRuntimeMonitor.cs` - WinCC process and frame monitoring
- `HMIViewpoint/WinCCScreenCapturer.cs` - window capture implementation
- `HMIViewport/` - desktop CDP components
- `HMIViewpoint/com.HMIViewport.cdpwebview/` - Unity WebView package source
