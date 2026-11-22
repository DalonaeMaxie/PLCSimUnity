# PLCSimUnity Code Reference

## Entry Point

`Properties/Program.cs` is responsible for startup:

- Parses command-line arguments.
- Creates or opens the named memory-mapped file.
- Writes an initial instance selection if `--instance` is present.
- Starts the PLC and WinCC worker threads.
- Opens `DashboardForm`.
- Cancels background work and disposes memory-mapped resources on exit.

## PLC Worker

`Properties/InteractiveInstanceSelector.cs` runs as an asynchronous background loop.

Every 20 ms, it:

1. Increments the heartbeat at offset 2.
2. Checks `SimulationRuntimeManager.IsRuntimeManagerAvailable`.
3. Reads the target instance name at offsets 16/20.
4. Creates or reconnects the Siemens `IInstance` interface.
5. Writes status and state telemetry.
6. Copies Unity input bytes to `InputArea`.
7. Copies PLC output bytes from `OutputArea` into shared memory.

The worker is a 50 Hz coupler. It does not block the WinForms UI thread.

## Instance Discovery

`Properties/InstanceScan.cs` uses `SimulationRuntimeManager.RegisteredInstanceInfo` and `CreateInterface(name)` to inspect instances.

Each scan has a three-second timeout intended to prevent a non-responsive runtime interface from blocking the dashboard. The dashboard scan and worker each invoke discovery as appropriate.

## Shared Memory Protocol

`Properties/SharedMemoryProtocol.cs` defines:

- `ServiceStatus`
- `RuntimeScreenStatus`
- `SharedMemoryHeader`
- `SharedMemoryConstants`

Use the constants in `SharedMemoryConstants` instead of hard-coding offsets in new code.

The region layout is:

```text
0x000-0x07F  Header (128 bytes)
0x080-0x0BF  Unity input area (64 bytes)
0x0C0-0x0FF  PLCSIM output area (64 bytes)
0x100-0x?    HMI frame and future data
```

The HMI monitor currently allocates the frame buffer after offset 256.

## Dashboard

`PLCSimUnityUI/DashboardForm.cs` provides:

- 10 Hz PLC/Unity/HMI telemetry updates
- Three-second instance refresh
- Search and filtering
- Manual instance selection
- Auto-discovery selection

Instance selection writes a UTF-8 name to offsets 16/20. An empty name means auto-discovery.

## WinCC Monitor

`HMIViewpoint/WinCCRuntimeMonitor.cs` searches for supported WinCC processes every 33 ms.

On detection it writes:

- Runtime screen status
- Process PID
- Debug port (0 for native capture)
- Capture frame rate
- Frame timestamp
- Total frame count

`HMIViewpoint/WinCCScreenCapturer.cs` captures the runtime window and writes RGB/BGRA frame bytes to offset 256.

## Unity CDP Package

The package under `HMIViewpoint/com.HMIViewport.cdpwebview/` contains:

- `Runtime/WebViewPanel.cs` - Unity MonoBehaviour
- `Runtime/CdpClient.cs` - Chrome DevTools Protocol WebSocket client
- `Runtime/BrowserProcess.cs` - browser discovery and headless launch
- `package.json` - Unity package metadata

This source is not compiled by the WinForms project unless copied into a Unity project package folder.

## Threading Notes

- Shared memory access must remain thread-safe from a viewpoint perspective; each accessor performs direct reads/writes.
- Instance selection is communicated by polling, not events.
- The WinCC capture loop is separate from the PLC loop.
- WinForms timers and controls execute on the UI thread.
- Cleanup is centralized in `Program.Main`'s `finally` block.

## Extending the Protocol

When changing the shared-memory protocol:

1. Update `SharedMemoryHeader`.
2. Update `SharedMemoryConstants` with matching offsets.
3. Update `Program.cs` memory allocation if header or buffer sizes change.
4. Update this reference and `README.md`.
5. Update every Unity client using the protocol.

Do not reuse or shift existing offsets in a running integration without coordinating an application and Unity redeploy.
