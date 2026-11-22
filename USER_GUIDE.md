# PLCSimUnity User Guide

## Start the Application

1. Install and start Siemens PLCSIM Advanced.
2. Register at least one PLC simulation instance.
3. Launch `PLCSimUnity.exe`.
4. The **PLCSimUnity - Control Center & Telemetry** window opens.

## Dashboard

### PLC Telemetry

The top panel shows:

- PLCSIM connection status
- Current or requested instance
- PLC heartbeat
- Unity client status
- WinCC HMI runtime status

The heartbeat increments as the worker runs. It is reset to zero only when the byte wraps.

### Discover Instances

The instance list refreshes at startup and every three seconds. Use **Refresh** for an immediate rescan.

The grid displays:

- Instance name
- Operating state
- CPU type

Use **Search / Filter** to narrow the list. Filtering does not change the connection target.

### Connect to an Instance

1. Select an instance in the grid.
2. Choose **Connect to Selected Instance**.
   - Double-clicking a row performs the same action.
3. The PLC worker reads the target name and reconnects on its next cycle.

Stopped instances appear in the grid, but remain in `InstanceNotRunning` until started.

### Enable Auto-Discovery

Choose **Enable Auto-Discover Mode** to clear the selected instance name.

The worker then:

1. Requests a scan of registered instances.
2. Selects the first instance in RUN state.
3. Falls back to the first discovered instance if none is running.

Auto-discovery is useful when the exact instance name may change.

## Command-Line Startup

```powershell
PLCSimUnity.exe --shm "PLCSimUnity_SharedMemory" --instance "S7-1500-Lab" --inputs 64 --outputs 64
```

- `--instance ""` or `--instance "AutoDiscover"` enables auto-discovery.
- `--shm` changes the named shared-memory region.
- `--inputs` and `--outputs` configure the dashboard and memory region arguments. The current PLC worker uses 64-byte I/O areas.

Every Unity client and bridge process must use the same shared-memory name.

## Unity Integration

Unity should open the same named memory mapped file and use these offsets:

- 128: 64 bytes written from Unity to the PLC `InputArea`
- 192: 64 bytes read from the PLC `OutputArea`
- 256 onward: optional HMI frame data

Unity activity telemetry is read from offset 100. Update that 4-byte little-endian value frequently with `Environment.TickCount` so the dashboard reports Unity as connected for up to two seconds.

## WinCC HMI Monitoring

The monitor recognizes these process names:

- `HmiRTm`
- `Siemens.Simatic.Hmi.Runtime`
- `HmiKpa`

When a process is running, the dashboard reports its PID and frame count. A web-based WinCC Unified runtime can instead be rendered in Unity with the included CDP WebView package.

## Status and Troubleshooting

| Dashboard status | Meaning |
|---|---|
| PLCSim: Uninitialized | Shared worker has not established a connection |
| PLCSim: PlcSimNotRunning | Runtime Manager is unavailable |
| PLCSim: InstanceNotFound | Requested instance is not registered |
| PLCSim: InstanceNotRunning | Instance exists but is not RUN |
| PLCSim: Connected | PLC I/O transfer is active |
| Unity 3D Engine: Disconnected | No recent Unity heartbeat |
| HMI: Disconnected | No supported runtime process was found |

### Common Problems

**No instances appear**

- Confirm the Runtime Manager is running.
- Confirm instances are registered in PLCSIM Advanced.
- Run a refresh and wait for the scan to finish.

**The instance stays disconnected**

- Start the instance in PLCSIM Advanced.
- Verify the exact instance name.
- Restart PLCSimUnity if the Runtime Manager was unavailable during startup.

**Unity I/O is not updating**

- Confirm the memory name matches.
- Confirm Unity reads at 192 and writes at 128.
- Verify the Unity heartbeat at offset 100.

**HMI frames are not displayed**

- Confirm one of the supported WinCC runtime processes is running.
- The native capture monitor does not launch or control the HMI runtime.
