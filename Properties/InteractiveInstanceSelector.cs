using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Threading;
using PLCSimUnity.Shared;

namespace PLCSimUnity
{
    /// <summary>
    /// Interactive console UI for selecting and switching between PLC instances
    /// Runs in separate thread from the main P/O communication loop
    /// </summary>
    public class InteractiveInstanceSelector
    {
        private static MemoryMappedFile _mmf;
        private static MemoryMappedViewAccessor _view;
        private static List<InstanceSummary> _cachedInstances = new List<InstanceSummary>();
        private static DateTime _lastRefresh = DateTime.MinValue;
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(3);

        public static void LaunchInteractiveMode()
        {
            Thread uiThread = new Thread(InteractiveMenuLoop) { IsBackground = true };
            uiThread.Start();
        }

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
                    Thread.Sleep(200);
                }
                catch
                {
                    Thread.Sleep(1000);
                }
            }
        }

        private static void ShowMainMenu()
        {
            bool inMenu = true;
            while (inMenu)
            {
                Console.Clear();
                Console.WriteLine("╔════════════════════════════════════════╗");
                Console.WriteLine("║   PLCSimUnity - Instance Selector     ║");
                Console.WriteLine("╚════════════════════════════════════════╝");
                Console.WriteLine();

                // Show current instance
                if (_view != null)
                {
                    try
                    {
                        string currentInstance = ReadSelectedInstanceName();
                        Console.WriteLine($"Current Instance: {(string.IsNullOrEmpty(currentInstance) ? "[Auto-Discover]" : currentInstance)}");
                    }
                    catch { }
                }

                Console.WriteLine();
                Console.WriteLine("1. List Available Instances");
                Console.WriteLine("2. Connect to Instance");
                Console.WriteLine("3. Enable Auto-Discover");
                Console.WriteLine("4. Connection Status");
                Console.WriteLine("5. Exit Menu");
                Console.WriteLine();
                Console.Write("Select option (1-5): ");

                string input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        ShowAvailableInstances();
                        break;
                    case "2":
                        SelectInstance();
                        break;
                    case "3":
                        EnableAutoDiscover();
                        break;
                    case "4":
                        ShowConnectionStatus();
                        break;
                    case "5":
                        inMenu = false;
                        break;
                }
            }

            Console.WriteLine("\n[Menu closed - press 'M' to reopen]\n");
        }

        private static void ShowAvailableInstances()
        {
            Console.Clear();
            Console.WriteLine("Scanning for available PLC instances...\n");

            try
            {
                var instances = PlcScanner.ScanForInstances(verbose: false);

                if (instances.Count == 0)
                {
                    Console.WriteLine("No PLC instances found.");
                }
                else
                {
                    Console.WriteLine($"Found {instances.Count} instance(s):\n");
                    for (int i = 0; i < instances.Count; i++)
                    {
                        var inst = instances[i];
                        Console.WriteLine($"  [{i + 1}] {inst.Name}");
                        Console.WriteLine($"      State: {inst.OperatingState}");
                        Console.WriteLine($"      Type:  {inst.CpuType}");
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning instances: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

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

                if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= instances.Count)
                {
                    string selectedName = instances[choice - 1].Name;
                    WriteSelectedInstanceName(selectedName);
                    Console.WriteLine($"\n✓ Connecting to: {selectedName}");
                    Thread.Sleep(2000);
                }
                else
                {
                    Console.WriteLine("\nInvalid selection.");
                    Thread.Sleep(1500);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Thread.Sleep(2000);
            }
        }

        private static void EnableAutoDiscover()
        {
            Console.Clear();
            Console.WriteLine("Auto-Discover Mode");
            Console.WriteLine();
            Console.WriteLine("In Auto-Discover mode, the service will:");
            Console.WriteLine("  • Automatically scan for running PLC instances");
            Console.WriteLine("  • Connect to the first instance in RUN state");
            Console.WriteLine("  • Auto-reconnect if the PLC stops and another starts");
            Console.WriteLine();

            try
            {
                WriteSelectedInstanceName(""); // Empty = auto-discover
                Console.WriteLine("✓ Auto-Discover mode enabled");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static void ShowConnectionStatus()
        {
            Console.Clear();
            Console.WriteLine("Connection Status\n");

            if (_view == null)
            {
                TryConnectToSharedMemory();
            }

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
                Console.WriteLine("Make sure the background service is running.");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static void TryConnectToSharedMemory()
        {
            try
            {
                _mmf = MemoryMappedFile.OpenExisting(SharedMemoryConstants.DefaultShmName);
                _view = _mmf.CreateViewAccessor();
            }
            catch
            {
                _view = null;
            }
        }

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
                _view.ReadArray(SharedMemoryConstants.Offset_SelectedInstanceName, nameBuffer, 0, length);
                return System.Text.Encoding.UTF8.GetString(nameBuffer);
            }
            catch
            {
                return "";
            }
        }

        private static void WriteSelectedInstanceName(string instanceName)
        {
            if (_view == null)
                TryConnectToSharedMemory();

            if (_view == null)
                throw new InvalidOperationException("Cannot connect to shared memory.");

            byte[] nameBytes = string.IsNullOrEmpty(instanceName)
                ? new byte[0]
                : System.Text.Encoding.UTF8.GetBytes(instanceName);

            int lengthToWrite = Math.Min(nameBytes.Length, SharedMemoryConstants.MaxInstanceNameLength);

            // Write length
            _view.Write(SharedMemoryConstants.Offset_SelectedInstanceNameLength, lengthToWrite);

            // Clear buffer
            byte[] clearBuffer = new byte[SharedMemoryConstants.MaxInstanceNameLength];
            _view.WriteArray(SharedMemoryConstants.Offset_SelectedInstanceName, clearBuffer, 0, clearBuffer.Length);

            // Write new name
            if (lengthToWrite > 0)
            {
                _view.WriteArray(SharedMemoryConstants.Offset_SelectedInstanceName, nameBytes, 0, lengthToWrite);
            }
        }
    }
}
