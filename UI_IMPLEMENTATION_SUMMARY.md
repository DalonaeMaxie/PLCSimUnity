# UI Implementation Summary

## Current Implementation

PLCSimUnity uses a WinForms control center rather than a console keyboard menu.

The UI is implemented in `PLCSimUnityUI/DashboardForm.cs` and provides:

- System connection telemetry
- PLC instance discovery
- Instance search and selection
- Auto-discovery mode
- WinCC runtime status
- Unity heartbeat visibility

## Telemetry

A 100 ms timer refreshes dashboard values from shared memory:

- PLC status and heartbeat
- Selected or active instance
- Unity activity using the 4-byte tick at offset 100
- HMI status, PID, and frame count

A separate three-second timer scans registered PLC instances.

## Instance Selection

Selecting an instance writes its UTF-8 name to offsets 16/20. Clearing the name enables auto-discovery.

The PLC worker reads the target on the 50 Hz loop and reconnects when the target changes.

## HMI Integration

The UI reports native WinCC Runtime state monitored by `WinCCRuntimeMonitor`. The optional CDP WebView package is a Unity integration and is not a WinForms control.

## Known Constraints

- The PLC worker currently always copies 64-byte I/O buffers.
- Instance names are limited to 44 bytes in the protocol.
- Browser URL fields in the protocol are reserved/limited and are not used by the native WinCC monitor.
- Native HMI capture requires a supported WinCC Runtime process on Windows.
- The Unity and desktop applications must agree on the shared-memory name and layout.
