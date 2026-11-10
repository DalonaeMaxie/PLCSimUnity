# PLCSimUnity Interactive UI - User Guide

## Quick Start

### Launch the Application
```bash
PLCSimUnity.exe
```

On startup, you'll see:
```
[PLCSimUnity] Initializing Communication Engine
 Config → Mode: Auto-Rescan | I-Bytes: 64 | Q-Bytes: 64 | SHM: PLCSimUnity_SharedMemory
[Tip] Press 'M' during operation to open interactive instance selector menu

[PLCSimUnity UI Mode] - Press 'M' to open menu anytime
```

The service is now running in the background with the interactive menu available.

---

## Using the Interactive Menu

### Opening the Menu
Press **`M`** anytime during operation to open the menu.

The menu appears as:
```
╔════════════════════════════════════════╗
║   PLCSimUnity - Instance Selector     ║
╚════════════════════════════════════════╝

Current Instance: [Auto-Discover]

1. List Available Instances
2. Connect to Instance
3. Enable Auto-Discover
4. Connection Status
5. Exit Menu

Select option (1-5):
```

### Closing the Menu
Press **5** at any time to close the menu and return to background operation.

---

## Menu Operations

### 1️⃣ List Available Instances

Shows all registered PLC instances discovered on your system.

```
╔════════════════════════════════════════╗
║   PLCSimUnity - Instance Selector     ║
╚════════════════════════════════════════╝

Scanning for available PLC instances...

Found 2 instance(s):

  [1] S7-1200_Line_1
	  State: Run
	  Type:  S7-1200

  [2] S7-1500_Lab
	  State: Stop
	  Type:  S7-1500

  [3] S7200_Legacy  
	  State: Run
	  Type:  S7-200

Press any key to continue...
```

**What This Shows:**
- `Name` - Instance identifier in PLC SIM
- `State` - Operating state (Run/Stop/Off)
- `Type` - PLC CPU model

**Use Case:** See what PLCs are available before connecting.

---

### 2️⃣ Connect to Instance

Manually select a specific PLC instance to connect to.

```
╔════════════════════════════════════════╗
║   PLCSimUnity - Instance Selector     ║
╚════════════════════════════════════════╝

Select Instance to Connect

[1] S7-1200_Line_1 (Run)
[2] S7-1500_Lab (Stop)
[3] S7200_Legacy (Run)

Select instance number (1-3): 1

✓ Connecting to: S7-1200_Line_1
```

**Steps:**
1. Menu displays all available instances with current state
2. Enter the number of the instance you want
3. Service immediately switches to that instance
4. Status updates in real-time

**Important Notes:**
- You can only connect to instances recognized by PLC SIM
- Selecting a stopped PLC will connect but show "InstanceNotRunning" status
- Auto-Discover mode is disabled when you select a specific instance
- Press 'M' again to see the updated "Current Instance:"

---

### 3️⃣ Enable Auto-Discover

Switches service to automatic instance discovery mode.

```
╔════════════════════════════════════════╗
║   PLCSimUnity - Instance Selector     ║
╚════════════════════════════════════════╝

Auto-Discover Mode

In Auto-Discover mode, the service will:
  • Automatically scan for running PLC instances
  • Connect to the first instance in RUN state
  • Auto-reconnect if the PLC stops and another starts

✓ Auto-Discover mode enabled

Press any key to continue...
```

**What It Does:**
- Scans for PLC instances every 2 seconds
- Automatically connects to any instance in RUN state
- Prioritizes running instances over stopped ones
- If connected PLC stops, searches for alternative running instance
- No manual intervention needed

**Use Cases:**
- Development environment with multiple test instances
- Redundant PLC setup where any PLC can take load
- Automated testing scenarios

---

### 4️⃣ Connection Status

Displays real-time status from the service.

```
╔════════════════════════════════════════╗
║   PLCSimUnity - Instance Selector     ║
╚════════════════════════════════════════╝

Connection Status

Service Status:    Connected
Current Instance:  S7-1200_Line_1
State Code:        0
Heartbeat:         127
Input Bytes:       64
Output Bytes:      64

Press any key to continue...
```

**Status Meanings:**

| Service Status | Description |
|---|---|
| Connected | Currently connected and exchanging I/O |
| PlcSimNotRunning | PLC SIM service not available |
| InstanceNotFound | Selected instance not registered |
| InstanceNotRunning | Instance exists but in STOP state |
| Error | Connection error occurred |

**Diagnostics:**
- `Heartbeat` - Counter increments each cycle (proves service running)
- If heartbeat stops increasing = service stalled
- Real-time check of input/output buffer configuration

---

### 5️⃣ Exit Menu

Close the interactive menu and return to background operation.

Menu closes and service continues running.

---

## Workflow Examples

### Scenario 1: Connect to Running PLC

```
Press 'M' at any time
↓
Select option: 2
↓
View available instances: List shows "Line_1" in Run state
↓
Select instance: 1
↓
Press 'M' again → "Current Instance: Line_1"
↓
Service now communicating with Line_1
```

### Scenario 2: Switch Between PLCs

```
Currently connected to: Controller_1
↓
Press 'M'
↓
Select option: 2
↓
Select instance: 2 (instead of 1)
↓
Service suddenly switches from Controller_1 to Controller_2
↓
No service restart, no downtime, seamless switching
```

### Scenario 3: Auto-Detection Setup

```
Multiple PLCs available, but don't care which
↓
Press 'M'
↓
Select option: 3 (Auto-Discover)
↓
Service automatically connects to first running PLC
↓
If that PLC stops, auto-connect to next one
↓
Press 'M' anytime to see which one is active
```

### Scenario 4: Troubleshooting Connection

```
Unity reports no I/O received
↓
Press 'M'
↓
Select option: 4 (Status)
↓
Status shows: "InstanceNotRunning"
↓
Issue identified: PLC is in STOP state
↓
Fix: Start PLC in SIMruntime or select different running PLC
```

---

## Keyboard Shortcuts

| Key | Action |
|---|---|
| **M** | Open interactive menu |
| **Escape** | Exit application |
| **1-5** | Select menu option |
| **Any** | Continue from status screens |

---

## Tips & Best Practices

✅ **DO**
- Press 'M' periodically to monitor connection status
- Use auto-discover in development environments
- Check "Connection Status" if I/O stops
- Keep menu open to watch heartbeat counter

❌ **DON'T**
- Change instances without closing previous one (use menu option 2)
- Close console window accidentally (use Escape key for clean exit)
- Ignore connection errors - check status option
- Leave multiple PLCSimUnity instances running with same SHM name

---

## Troubleshooting

### "No PLC instances found"
**Problem:** Menu shows no instances available

**Solutions:**
1. Verify PLC SIM runtime is installed and running
2. Create a test instance in PLC SIM
3. Ensure PLC instance is registered (not just open)

### Heartbeat not incrementing
**Problem:** Status shows frozen counter

**Solution:** Service is stalled - restart application

### Cannot connect to specific instance
**Problem:** Selected instance but still shows "Auto-Discover"

**Solutions:**
1. Verify instance name is spelled correctly
2. Confirm instance is active in PLC SIM
3. Check if instance is in STOP state (connect anyway, will show status)

### Menu unresponsive
**Problem:** Pressing 'M' doesn't open menu

**Solutions:**
1. Wait a moment and try again
2. Check that console window has focus
3. Try holding 'M' key for longer

### Instance changed unexpectedly
**Problem:** Service switched instances without user action

**Explanation:** Auto-Discover mode is active. To fix:
1. Press 'M'
2. Select option 2
3. Choose specific instance
4. Service will stop auto-switching

---

## Advanced: Custom Configuration

### Multiple Service Instances

Run multiple PLCSimUnity instances with different shared memories:

```bash
# Instance 1 - Default SHM
PLCSimUnity.exe --instance "Controller_A"

# Instance 2 - Custom SHM (in different console)
PLCSimUnity.exe --instance "Controller_B" --shm "Custom_SHM_2"
```

Each has its own interactive menu since they communicate via different shared memory regions.

### Custom I/O Buffer Sizes

```bash
PLCSimUnity.exe --inputs 128 --outputs 256
```

Opens interactive menu with custom buffer configuration.

---

## Technical Note

The interactive menu:
- Runs in a background thread
- Does NOT block the 50 Hz I/O cycle
- Communicates via shared memory
- Updates real-time without service restart

Performance impact: **<0.1ms** per menu operation.

---

## Need Help?

1. **Check Connection Status** (Menu option 4) - diagnoses 80% of issues
2. **List Available Instances** (Menu option 1) - verify PLCs are discoverable  
3. **Review the console output** - service logs are displayed on startup
4. **Try Auto-Discover** (Menu option 3) - automatic mode sometimes works better

---

**Happy simulating! 🤖**
