using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Siemens.Simatic.Simulation.Runtime;
//#*D@l0n13_16*#
namespace PLCSimUnity
{
    internal class Program
    {
        private const string DefaultShmName = "PLCSimUnity_SharedMemory";
        private const int ApiTimeoutMs = 5000; // 5 second timeout for PLC API calls

        private static MemoryMappedFile _mmf;
        private static MemoryMappedViewAccessor _view;
        private static IInstance _plcInstance;

        private static string _instanceName = "AutoDiscover";
        private static bool _isAutoDiscover = true;
        private static int _inputBytesCount = 64;
        private static int _outputBytesCount = 64;
        private static string _shmName = DefaultShmName;

        private static byte _heartbeat = 0;
        private static byte _currentStage = 0;
        private static DateTime _lastLoopTime = DateTime.UtcNow;
        private static DateTime _lastScanTime = DateTime.MinValue;
        private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(2.0);
        private static CancellationTokenSource _exitTokenSource = new CancellationTokenSource();

        public static void Main(string[] args)
        {
            ParseArguments(args);

            _isAutoDiscover = _instanceName.Equals("AutoDiscover", StringComparison.OrdinalIgnoreCase);
            Console.Title = $"PLCSimUnity - Mode: {(_isAutoDiscover ? "AutoDiscover" : _instanceName)}";

            Console.WriteLine("[PLCSimUnity] Initializing Communication Engine");
            Console.WriteLine($" Config -> Mode: {(_isAutoDiscover ? "Auto-Rescan" : _instanceName)} | I-Bytes: {_inputBytesCount} | Q-Bytes: {_outputBytesCount} | SHM: {_shmName}");

            int totalShmSize = 64 + _inputBytesCount + _outputBytesCount;
            _mmf = MemoryMappedFile.CreateOrOpen(_shmName, totalShmSize);
            _view = _mmf.CreateViewAccessor();

            // Store buffer sizes in Shared Memory header
            _view.Write(4, _inputBytesCount);
            _view.Write(8, _outputBytesCount);

            // Launch interactive UI menu
            InteractiveInstanceSelector.LaunchInteractiveMode();

            Thread watchdog = new Thread(WatchdogLoop) { IsBackground = true };
            watchdog.Start();

            // Start keyboard listener thread (non-blocking)
            Thread keyboardListener = new Thread(KeyboardListenerThread) { IsBackground = true };
            keyboardListener.Start();

            byte[] inputBuffer = new byte[_inputBytesCount];
            byte[] outputBuffer = new byte[_outputBytesCount];

            while (!_exitTokenSource.Token.IsCancellationRequested)
            {
                _lastLoopTime = DateTime.UtcNow;
                _heartbeat++;

                try
                {
                    // Stage 1: Runtime Manager Availability Check
                    SetStage(1);
                    if (!SimulationRuntimeManager.IsRuntimeManagerAvailable)
                    {
                        UpdateStatus(1, 0); // PlcSimNotRunning
                        ClearOutputs();
                        CleanupInstance();
                        Thread.Sleep(1000);
                        continue;
                    }

                    // Stage 2: Continuous Auto-Rescan when disconnected or in AutoDiscover mode
                    SetStage(2);

                    // Check if UI has requested a specific instance
                    CheckForUIInstanceSelection();

                    if (_plcInstance == null && _isAutoDiscover && (DateTime.UtcNow - _lastScanTime) > ScanInterval)
                    {
                        _lastScanTime = DateTime.UtcNow;
                        List<InstanceSummary> available = PlcScanner.ScanForInstances(verbose: true);

                        // Prioritize running instance; fall back to any active instance
                        InstanceSummary selected = available.Find(x => x.OperatingState == EOperatingState.Run)
                                                 ?? available.Find(x => x.IsActive);

                        if (selected != null)
                        {
                            _instanceName = selected.Name;
                            Console.WriteLine($"[PLCSimUnity] Target acquired: {_instanceName}");
                        }
                    }

                    // Stage 3: Validate Target Info
                    SetStage(3);
                    SInstanceInfo[] registeredInstances = SimulationRuntimeManager.RegisteredInstanceInfo;
                    if (registeredInstances == null || registeredInstances.Length == 0)
                    {
                        UpdateStatus(1, 0);
                        ClearOutputs();
                        CleanupInstance();
                        Thread.Sleep(1000);
                        continue;
                    }

                    SInstanceInfo targetInfo = registeredInstances.FirstOrDefault(x => x.Name != null && x.Name.Equals(_instanceName, StringComparison.OrdinalIgnoreCase));

                    if (string.IsNullOrEmpty(targetInfo.Name))
                    {
                        UpdateStatus(2, 0); // InstanceNotFound
                        ClearOutputs();
                        CleanupInstance();
                        Thread.Sleep(1000);
                        continue;
                    }

                    // Stage 4: Connect or Reconnect Interface
                    SetStage(4);
                    if (_plcInstance == null || !_plcInstance.Name.Equals(targetInfo.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        CleanupInstance();
                        _plcInstance = SimulationRuntimeManager.CreateInterface(targetInfo.Name);
                        Console.WriteLine($"[PLCSimUnity] Connected handle to interface: {targetInfo.Name}");
                    }

                    // Stage 5: Check Active PLC Operating State
                    SetStage(5);
                    EOperatingState state = _plcInstance.OperatingState;
                    byte stateByte = (byte)state;

                    if (state != EOperatingState.Run)
                    {
                        UpdateStatus(3, stateByte); // InstanceNotRunning (e.g. STOP mode)
                        ClearOutputs();

                        // If in AutoDiscover mode and current PLC went into STOP, release handle to rescan for another running PLC
                        if (_isAutoDiscover)
                        {
                            CleanupInstance();
                        }

                        Thread.Sleep(500);
                        continue;
                    }

                    // Stage 6: High-Speed I/O Swap with Unity Shared Memory (50 Hz)
                    SetStage(6);
                    UpdateStatus(4, stateByte); // Connected & Running

                    // Read Unity Inputs from SHM (Offset 64) -> Write to PLC Input Area
                    _view.ReadArray(64, inputBuffer, 0, _inputBytesCount);
                    _plcInstance.InputArea.WriteBytes(0, inputBuffer);

                    // Read PLC Output Area -> Write to Unity SHM (Offset 64 + InputBytesCount)
                    outputBuffer = _plcInstance.OutputArea.ReadBytes(0, (uint)_outputBytesCount);
                    _view.WriteArray(64 + _inputBytesCount, outputBuffer, 0, _outputBytesCount);

                    SetStage(7);
                    Thread.Sleep(20); // 50 Hz polling rate
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PLCSimUnity Error] {ex.Message}");
                    UpdateStatus(5, 0); // Error
                    ClearOutputs();
                    CleanupInstance();
                    Thread.Sleep(1000);
                }
            }
        }

        private static void ParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--instance", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    _instanceName = args[++i];
                else if (args[i].Equals("--inputs", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    int.TryParse(args[++i], out _inputBytesCount);
                else if (args[i].Equals("--outputs", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    int.TryParse(args[++i], out _outputBytesCount);
                else if (args[i].Equals("--shm", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    _shmName = args[++i];
            }
        }

        private static void SetStage(byte stage)
        {
            _currentStage = stage;
            _view?.Write(3, _currentStage);
        }

        private static void UpdateStatus(byte status, byte state)
        {
            _view?.Write(0, status);
            _view?.Write(1, state);
            _view?.Write(2, _heartbeat);
        }

        private static void ClearOutputs()
        {
            if (_view == null) return;
            byte[] zeros = new byte[_outputBytesCount];
            _view.WriteArray(64 + _inputBytesCount, zeros, 0, _outputBytesCount);
        }

        private static void CleanupInstance()
        {
            if (_plcInstance != null)
            {
                try { _plcInstance.Dispose(); } catch { }
                _plcInstance = null;
            }
        }

        private static void KeyboardListenerThread()
        {
            // Background thread to listen for Escape key without blocking main loop
            while (!_exitTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Escape)
                        {
                            Console.WriteLine("[PLCSimUnity] Escape key pressed. Shutting down...");
                            _exitTokenSource.Cancel();
                            break;
                        }
                    }
                    Thread.Sleep(100); // Check every 100ms
                }
                catch
                {
                    // Ignore console input errors
                }
            }
        }

        private static void WatchdogLoop()
        {
            while (true)
            {
                Thread.Sleep(2000);
                if ((DateTime.UtcNow - _lastLoopTime).TotalSeconds > 8)
                {
                    Console.WriteLine($"[Watchdog Failure] Execution stalled at Stage {_currentStage}. Terminating...");
                    Environment.Exit(99);
                }
            }
        }

        private static void CheckForUIInstanceSelection()
        {
            // Read selected instance name from shared memory (written by UI controller)
            if (_view == null)
                return;

            try
            {
                int nameLength = _view.ReadInt32(20); // Offset for SelectedInstanceNameLength
                if (nameLength > 0)
                {
                    byte[] nameBuffer = new byte[nameLength];
                    _view.ReadArray(24, nameBuffer, 0, nameLength); // Offset for SelectedInstanceName
                    string selectedName = System.Text.Encoding.UTF8.GetString(nameBuffer);

                    if (!string.IsNullOrEmpty(selectedName) && selectedName != _instanceName)
                    {
                        _instanceName = selectedName;
                        _isAutoDiscover = false;
                        Console.WriteLine($"[PLCSimUnity] UI selected instance: {_instanceName}");
                    }
                }
            }
            catch
            {
                // Ignore read errors
            }
        }
    }
}