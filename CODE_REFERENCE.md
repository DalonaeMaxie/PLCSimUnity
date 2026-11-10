# Code Reference Guide

## Quick Reference - Key Implementations

### 1. Opening the Interactive Menu
**File:** `Properties/InteractiveInstanceSelector.cs`

```csharp
// Called from Program.cs on startup
InteractiveInstanceSelector.LaunchInteractiveMode();
```

This launches a background thread that:
- Monitors for 'M' key press
- Shows interactive menu without blocking I/O
- Reads/writes to shared memory for instance selection

### 2. Instance Selection in Background Service
**File:** `Properties/Program.cs`

```csharp
// Called in main I/O loop (Stage 2)
CheckForUIInstanceSelection();

private static void CheckForUIInstanceSelection()
{
	if (_view == null) return;

	try
	{
		int nameLength = _view.ReadInt32(20);
		if (nameLength > 0)
		{
			byte[] nameBuffer = new byte[nameLength];
			_view.ReadArray(24, nameBuffer, 0, nameLength);
			string selectedName = System.Text.Encoding.UTF8.GetString(nameBuffer);

			if (!string.IsNullOrEmpty(selectedName) && selectedName != _instanceName)
			{
				_instanceName = selectedName;
				_isAutoDiscover = false;
				Console.WriteLine($"[PLCSimUnity] UI selected instance: {_instanceName}");
			}
		}
	}
	catch { }
}
```

**How it works:**
- Reads shared memory at offset 20 (name length)
- Reads instance name at offset 24 (max 44 bytes)
- Updates service's current instance if different
- Disables auto-discover when instance is manually selected

### 3. Shared Memory Communication
**File:** `Properties/SharedMemoryProtocol.cs`

```csharp
// Memory layout offsets
public static class SharedMemoryConstants
{
	public const int Offset_Status = 0;
	public const int Offset_State = 1;
	public const int Offset_Heartbeat = 2;
	public const int Offset_CurrentStage = 3;
	public const int Offset_InputBytesCount = 4;
	public const int Offset_OutputBytesCount = 8;
	public const int Offset_SelectedInstanceNameLength = 16;
	public const int Offset_SelectedInstanceName = 20;
	public const int Offset_InputData = 64;
}

// Status codes
public enum ServiceStatus : byte
{
	Uninitialized = 0,
	PlcSimNotRunning = 1,
	InstanceNotFound = 2,
	InstanceNotRunning = 3,
	Connected = 4,
	Error = 5
}
```

### 4. Reading Instance Name from Menu
**File:** `Properties/InteractiveInstanceSelector.cs`

```csharp
private static string ReadSelectedInstanceName()
{
	if (_view == null)
		TryConnectToSharedMemory();

	if (_view == null)
		return "";

	try
	{
		int length = _view.ReadInt32(SharedMemoryConstants.Offset_SelectedInstanceNameLength);
		if (length <= 0)
			return "";

		byte[] nameBuffer = new byte[length];
		_view.ReadArray(SharedMemoryConstants.Offset_SelectedInstanceName, 
					   nameBuffer, 0, length);
		return System.Text.Encoding.UTF8.GetString(nameBuffer);
	}
	catch
	{
		return "";
	}
}
```

### 5. Writing Instance Name from Menu
**File:** `Properties/InteractiveInstanceSelector.cs`

```csharp
private static void WriteSelectedInstanceName(string instanceName)
{
	if (_view == null)
		TryConnectToSharedMemory();

	if (_view == null)
		throw new InvalidOperationException("Cannot connect to shared memory.");

	byte[] nameBytes = string.IsNullOrEmpty(instanceName)
		? new byte[0]
		: System.Text.Encoding.UTF8.GetBytes(instanceName);

	int lengthToWrite = Math.Min(nameBytes.Length, 44);

	// Write length
	_view.Write(SharedMemoryConstants.Offset_SelectedInstanceNameLength, lengthToWrite);

	// Clear buffer
	byte[] clearBuffer = new byte[44];
	_view.WriteArray(SharedMemoryConstants.Offset_SelectedInstanceName, 
					 clearBuffer, 0, clearBuffer.Length);

	// Write new name
	if (lengthToWrite > 0)
	{
		_view.WriteArray(SharedMemoryConstants.Offset_SelectedInstanceName, 
						 nameBytes, 0, lengthToWrite);
	}
}
```

### 6. Interactive Menu Loop
**File:** `Properties/InteractiveInstanceSelector.cs`

```csharp
private static void InteractiveMenuLoop()
{
	Console.WriteLine("\n[PLCSimUnity UI Mode] - Press 'M' to open menu anytime");

	while (true)
	{
		try
		{
			if (Console.KeyAvailable)
			{
				var key = Console.ReadKey(true);
				if (key.Key == ConsoleKey.M || key.Key == ConsoleKey.H)
				{
					ShowMainMenu();
				}
			}
			Thread.Sleep(200);  // Check every 200ms - non-blocking
		}
		catch
		{
			Thread.Sleep(1000);
		}
	}
}
```

**Key Points:**
- Console.KeyAvailable doesn't block
- Runs in separate background thread
- Checks every 200ms for key press
- Main I/O loop unaffected

### 7. Medical Menu Implementation
**File:** `Properties/InteractiveInstanceSelector.cs`

```csharp
private static void SelectInstance()
{
	Console.Clear();
	Console.WriteLine("Select Instance to Connect\n");

	try
	{
		var instances = PlcScanner.ScanForInstances(verbose: false);

		if (instances.Count == 0)
		{
			Console.WriteLine("No PLC instances available.");
			Thread.Sleep(2000);
			return;
		}

		for (int i = 0; i < instances.Count; i++)
		{
			Console.WriteLine($"[{i + 1}] {instances[i].Name} ({instances[i].OperatingState})");
		}

		Console.WriteLine();
		Console.Write("Select instance number (1-{0}): ", instances.Count);

		if (int.TryParse(Console.ReadLine(), out int choice) && 
			choice > 0 && choice <= instances.Count)
		{
			string selectedName = instances[choice - 1].Name;
			WriteSelectedInstanceName(selectedName);
			Console.WriteLine($"\n✓ Connecting to: {selectedName}");
			Thread.Sleep(2000);
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine($"Error: {ex.Message}");
		Thread.Sleep(2000);
	}
}
```

### 8. Connection Status Display
**File:** `Properties/InteractiveInstanceSelector.cs`

```csharp
private static void ShowConnectionStatus()
{
	Console.Clear();
	Console.WriteLine("Connection Status\n");

	if (_view == null)
		TryConnectToSharedMemory();

	if (_view != null)
	{
		try
		{
			byte status = _view.ReadByte(SharedMemoryConstants.Offset_Status);
			byte state = _view.ReadByte(SharedMemoryConstants.Offset_State);
			byte heartbeat = _view.ReadByte(SharedMemoryConstants.Offset_Heartbeat);
			int inputCount = _view.ReadInt32(SharedMemoryConstants.Offset_InputBytesCount);
			int outputCount = _view.ReadInt32(SharedMemoryConstants.Offset_OutputBytesCount);
			string instanceName = ReadSelectedInstanceName();

			Console.WriteLine($"Service Status:    {(ServiceStatus)status}");
			Console.WriteLine($"Current Instance:  {(string.IsNullOrEmpty(instanceName) ? "[Auto-Discover]" : instanceName)}");
			Console.WriteLine($"State Code:        {state}");
			Console.WriteLine($"Heartbeat:         {heartbeat}");
			Console.WriteLine($"Input Bytes:       {inputCount}");
			Console.WriteLine($"Output Bytes:      {outputCount}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error reading status: {ex.Message}");
		}
	}
	else
	{
		Console.WriteLine("Cannot connect to service shared memory.");
	}

	Console.WriteLine("\nPress any key to continue...");
	Console.ReadKey(true);
}
```

### 9. Auto-Discover Mode
**File:** `Properties/InteractiveInstanceSelector.cs`

```csharp
private static void EnableAutoDiscover()
{
	Console.Clear();
	Console.WriteLine("Auto-Discover Mode\n");
	Console.WriteLine("In Auto-Discover mode, the service will:");
	Console.WriteLine("  • Automatically scan for running PLC instances");
	Console.WriteLine("  • Connect to the first instance in RUN state");
	Console.WriteLine("  • Auto-reconnect if the PLC stops and another starts");
	Console.WriteLine();

	try
	{
		WriteSelectedInstanceName("");  // Empty = auto-discover
		Console.WriteLine("✓ Auto-Discover mode enabled");
	}
	catch (Exception ex)
	{
		Console.WriteLine($"Error: {ex.Message}");
	}

	Console.WriteLine("\nPress any key to continue...");
	Console.ReadKey(true);
}
```

---

## Integration Points

### In Program.cs Main Loop
```csharp
// After opening shared memory (line ~50)
InteractiveInstanceSelector.LaunchInteractiveMode();

// In main I/O loop, Stage 2 (line ~80)
CheckForUIInstanceSelection();
```

### Shared Memory Header Layout
```
Bytes 0-19:   Service Status, Heartbeat, Stage, Counters
Bytes 20-63:  Selected Instance Name (variable length)
Bytes 64+:    Input/Output Data Buffers
```

---

## Thread Architecture

```
┌──────────────────────────────────────┐
│  Main Thread (50 Hz I/O Cycle)       │
│  ├─ Stage 1: Runtime check          │
│  ├─ Stage 2: CheckForUIInstanceSelection() ← Reads SHM
│  ├─ Stage 3-6: PLC I/O Exchange     │
│  └─ Stage 8: Sleep 20ms              │
└──────────────────────────────────────┘
		 │
		 │ Shared Memory
		 │
┌──────────────────────────────────────┐
│  Menu Thread (Background)             │
│  ├─ Check for 'M' key (200ms loop)   │
│  ├─ Show menu if 'M' pressed         │
│  └─ WriteSelectedInstanceName() → SHM │
└──────────────────────────────────────┘
```

**Benefit:** Zero blocking between threads

---

## Shared Memory Write/Read Example

### Writing from Menu
```csharp
// Menu thread - User selects "Controller_1"
WriteSelectedInstanceName("Controller_1");
```

### Reading in Service
```csharp
// Main thread - Service reads selection
CheckForUIInstanceSelection();
// Internally reads same bytes, updates _instanceName
```

**Result:** Instance changes instantly without restart

---

## Error Handling Patterns

### Graceful SHM Connection Loss
```csharp
private static void TryConnectToSharedMemory()
{
	try
	{
		_mmf = MemoryMappedFile.OpenExisting(SharedMemoryConstants.DefaultShmName);
		_view = _mmf.CreateViewAccessor();
	}
	catch
	{
		_view = null;  // Silently fail and retry next check
	}
}
```

### Safe Instance Name Reading
```csharp
try
{
	// Try to read
}
catch
{
	return "";  // Return empty string on any error
}
```

### Protected Menu Key Checking
```csharp
try
{
	if (Console.KeyAvailable)
	{
		var key = Console.ReadKey(true);
		// Process key
	}
}
catch
{
	Thread.Sleep(1000);  // Back off on error
}
```

---

## Building Custom Extensions

### Add New Menu Option
```csharp
private static void ShowMainMenu()
{
	// ... existing code ...
	Console.WriteLine("6. Your Custom Option");  // Add this

	case "6":
		YourCustomFunction();
		break;
}

private static void YourCustomFunction()
{
	Console.Clear();
	Console.WriteLine("Your Custom Function\n");

	// Access shared memory here
	if (_view != null)
	{
		// Read from or write to SHM
	}

	Console.WriteLine("\nPress any key to continue...");
	Console.ReadKey(true);
}
```

### Access PLC Instance List
```csharp
var instances = PlcScanner.ScanForInstances(verbose: false);
foreach (var inst in instances)
{
	Console.WriteLine($"Instance: {inst.Name}");
	Console.WriteLine($"  State: {inst.OperatingState}");
	Console.WriteLine($"  Type: {inst.CpuType}");
}
```

---

This code reference provides all the key implementations for understanding and extending the PLCSimUnity UI system.
