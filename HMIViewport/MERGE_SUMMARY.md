# HMIViewport Integration Summary

## Overview
The HMIViewport CDP WebView components have been integrated into the PLCSimUnity application. This integration provides access to the Chrome DevTools Protocol (CDP) for browser automation and remote rendering capabilities.

## What Was Merged

### Successfully Integrated ✓
The following core components were integrated into `HMIViewport/` directory:

1. **BrowserProcess.cs** (122 lines)
   - Launches and manages headless Chrome, Edge, or Chromium browsers
   - Handles browser process lifecycle and cleanup
   - Finds available browser installations on Windows, macOS, and Linux
   - No external dependencies beyond .NET Framework 4.8

2. **CdpClient.cs** (145 lines)
   - Minimal Chrome DevTools Protocol (CDP) client implementation
   - Communicates with browser via WebSocket
   - Handles async command/response pairs
   - Thread-safe event queue for browser events
   - Dependency: Newtonsoft.Json (added to project)

### Not Integrated (Framework Incompatibility) ✗
The following component remains in the original `HMIViewpoint/` folder but is excluded from the build:

3. **WebViewPanel.cs** (328 lines)
   - Unity-specific MonoBehaviour component for UI rendering
   - Requires: UnityEngine, UnityEngine.UI, UnityEngine.EventSystems
   - Not compatible with .NET Framework 4.8 console application
   - Status: Available in source form but excluded from compilation

## Project Structure

```
PLCSimUnity/
├── HMIViewport/
│   ├── BrowserProcess.cs          [Compiled]
│   ├── CdpClient.cs               [Compiled]
│   ├── WebViewPanel.cs            [Excluded - Unity-only]
│   ├── MERGE_INFO.cs              [Reference]
│   └── MERGE_SUMMARY.md           [This file]
├── HMIViewpoint/                  [Original source - excluded from build]
│   └── com.HMIViewport.cdpwebview/
│       └── Runtime/
│           ├── BrowserProcess.cs
│           ├── CdpClient.cs
│           └── WebViewPanel.cs
├── PLCSimUnity.csproj             [Updated with HMIViewport references]
├── packages.config                [Added Newtonsoft.Json dependency]
└── ...
```

## How to Use the Integrated Components

### Example: Browser Automation with CDP Client

```csharp
using CdpWebView;
using System.Threading.Tasks;

// 1. Start a browser process
var browser = new BrowserProcess();
string wsUrl = await browser.StartAsync(
	browserPath: BrowserProcess.FindBrowser(""),  // Auto-detect
	port: 9222,
	width: 1280,
	height: 800,
	ignoreCertErrors: true
);

// 2. Connect CDP client
var cdp = new CdpClient();
await cdp.ConnectAsync(wsUrl);

// 3. Send commands to browser
await cdp.Send("Page.enable");
await cdp.Send("Page.navigate", new { url = "https://example.com" });

// 4. Handle events
cdp.DrainEvents(ev => {
	if (ev.Method == "Page.screencastFrame") {
		// Handle frame update
	}
});

// 5. Cleanup
cdp.Dispose();
browser.Dispose();
```

## Dependencies Added

### NuGet Package
- **Newtonsoft.Json v13.0.3**: JSON serialization for CDP protocol messages
  - Location: `packages/Newtonsoft.Json.13.0.3/`
  - Added to: `PLCSimUnity.csproj` and `packages.config`

## Build Status
✓ **Build Successful**
- 0 Errors
- 0 Warnings
- Output: `bin/Debug/PLCSimUnity.exe`

## Build Configuration
- **Target Framework**: .NET Framework 4.8
- **Platform**: x64
- **Namespace**: CdpWebView (for core components)
- **Configuration**: Debug (default)

## Important Notes

### Unity Components
The WebViewPanel.cs component is a Unity MonoBehaviour that provides:
- Texture-based web page rendering
- Mouse, keyboard, and scroll input handling
- Event system integration
- Screen capture streaming via CDP

This component **cannot be used in the PLCSimUnity console application** without:
1. A full Unity environment, OR
2. Creating an adapter/shim layer to replace Unity-specific types, OR
3. Refactoring the code to use standard .NET types

Currently, it remains available in source form (`HMIViewpoint/com.HMIViewport.cdpwebview/`) for reference.

### Platform Support
The integrated components support:
- **Windows**: Chrome, Edge, Chromium (auto-detected)
- **macOS**: Chrome, Chromium, Edge
- **Linux**: google-chrome, chromium, microsoft-edge, snapcraft chromium

### Browser Requirements
- Chrome/Edge/Chromium must be installed on the machine
- DevTools protocol port (default: 9222) must be available
- Optional: Self-signed certificates can be ignored via `ignoreCertErrors` parameter

## Future Integration Options

If you need the WebViewPanel UI component in a different context, consider:

1. **Refactoring for WPF** - Create a WPF-compatible version using `System.Windows.Controls`
2. **Creating a Browser Wrapper** - Build a .NET-based browser window that uses the CDP client
3. **REST API Wrapper** - Expose CDP functionality through a web service
4. **Migrating to Unity** - Use the component in a actual Unity project

## File Reference

| File | Purpose | Status |
|------|---------|--------|
| HMIViewport/BrowserProcess.cs | Browser lifecycle management | ✓ Compiled |
| HMIViewport/CdpClient.cs | CDP protocol implementation | ✓ Compiled |
| HMIViewport/WebViewPanel.cs | Unity UI component | ✗ Excluded |
| HMIViewpoint/com.HMIViewport.cdpwebview/... | Original source | Reference Only |
| PLCSimUnity.csproj | Project file with updated references | Updated |
| packages.config | NuGet dependencies | Created |

## Build Output
```
HMIViewport\BrowserProcess.cs compiled successfully
HMIViewport\CdpClient.cs compiled successfully
WebViewPanel.cs excluded (Unity incompatibility)
Target: bin\Debug\PLCSimUnity.exe
```

---
*Merge completed successfully. Core CDP protocol components are ready for use.*
