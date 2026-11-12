using System;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PLCSimUnity.Shared;
using Siemens.Simatic.Simulation.Runtime;

namespace PLCSimUnity
{
    public static class InteractiveInstanceSelector
    {
        private static byte _heartbeat = 0;
        private static IInstance _activeInstance = null;
        private static string _connectedInstanceName = "";

        public static void StartWorker(MemoryMappedViewAccessor view, CancellationToken token)
        {
            Task.Run(() => WorkerLoop(view, token), token);
        }

        private static async Task WorkerLoop(MemoryMappedViewAccessor view, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 1. Increment Heartbeat Counter (Offset 2)
                    unchecked { _heartbeat++; }
                    view.Write(SharedMemoryConstants.Offset_Heartbeat, _heartbeat);

                    // 2. Check if PLCSIM Advanced Runtime Manager is active
                    if (!SimulationRuntimeManager.IsRuntimeManagerAvailable)
                    {
                        ResetActiveInstance();
                        view.Write(SharedMemoryConstants.Offset_Status, (byte)ServiceStatus.PlcSimNotRunning);
                        view.Write(SharedMemoryConstants.Offset_State, (byte)EOperatingState.Off);
                        await Task.Delay(100, token);
                        continue;
                    }

                    // 3. Read Target Instance Name from Shared Memory
                    int nameLen = view.ReadInt32(SharedMemoryConstants.Offset_SelectedInstanceNameLength);
                    string targetInstanceName = "";
                    if (nameLen > 0)
                    {
                        byte[] nameBuffer = new byte[nameLen];
                        view.ReadArray(SharedMemoryConstants.Offset_SelectedInstanceName, nameBuffer, 0, nameLen);
                        targetInstanceName = Encoding.UTF8.GetString(nameBuffer);
                    }

                    // Reconnect if user switched target instance in the dashboard
                    if (_activeInstance != null &&
                        !string.IsNullOrEmpty(targetInstanceName) &&
                        !targetInstanceName.Equals("[Auto-Discover]", StringComparison.OrdinalIgnoreCase) &&
                        !targetInstanceName.Equals(_connectedInstanceName, StringComparison.OrdinalIgnoreCase))
                    {
                        ResetActiveInstance();
                    }

                    // 4. Connect to PLCSIM Advanced Instance if not currently connected
                    if (_activeInstance == null)
                    {
                        _activeInstance = TryConnectPlcInstance(targetInstanceName, out _connectedInstanceName);
                    }

                    // 5. Transfer Telemetry & I/O Buffers via InputArea / OutputArea
                    if (_activeInstance != null)
                    {
                        EOperatingState state = _activeInstance.OperatingState;
                        view.Write(SharedMemoryConstants.Offset_State, (byte)state);

                        if (state == EOperatingState.Run)
                        {
                            view.Write(SharedMemoryConstants.Offset_Status, (byte)ServiceStatus.Connected);

                            // Sync Inputs from Unity (Offset 128) -> PLCSIM InputArea
                            byte[] inBuffer = new byte[64];
                            view.ReadArray(SharedMemoryConstants.Offset_InputData, inBuffer, 0, 64);
                            _activeInstance.InputArea.WriteBytes(0U, inBuffer);

                            // Sync Outputs from PLCSIM OutputArea -> Unity (Offset 192)
                            byte[] outBuffer = _activeInstance.OutputArea.ReadBytes(0U, 64U);
                            if (outBuffer != null && outBuffer.Length > 0)
                            {
                                view.WriteArray(SharedMemoryConstants.Offset_InputData + 64, outBuffer, 0, Math.Min(outBuffer.Length, 64));
                            }
                        }
                        else
                        {
                            view.Write(SharedMemoryConstants.Offset_Status, (byte)ServiceStatus.InstanceNotRunning);
                        }
                    }
                    else
                    {
                        view.Write(SharedMemoryConstants.Offset_Status, (byte)ServiceStatus.InstanceNotFound);
                        view.Write(SharedMemoryConstants.Offset_State, (byte)EOperatingState.Off);
                    }
                }
                catch
                {
                    ResetActiveInstance();
                    view.Write(SharedMemoryConstants.Offset_Status, (byte)ServiceStatus.PlcSimNotRunning);
                    view.Write(SharedMemoryConstants.Offset_State, (byte)EOperatingState.Off);
                }

                await Task.Delay(20, token); // 50 Hz Coupler Loop
            }
        }

        private static IInstance TryConnectPlcInstance(string targetName, out string resolvedName)
        {
            resolvedName = "";
            try
            {
                var instances = PlcScanner.ScanForInstances(verbose: false);
                if (instances == null || instances.Count == 0) return null;

                InstanceSummary target = string.IsNullOrEmpty(targetName) || targetName.Equals("[Auto-Discover]", StringComparison.OrdinalIgnoreCase)
                    ? instances.Find(i => i.OperatingState == EOperatingState.Run) ?? instances[0]
                    : instances.Find(inst => inst.Name != null && inst.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

                if (target != null && !string.IsNullOrEmpty(target.Name))
                {
                    IInstance instance = SimulationRuntimeManager.CreateInterface(target.Name);
                    resolvedName = target.Name;
                    return instance;
                }
            }
            catch { }

            return null;
        }

        private static void ResetActiveInstance()
        {
            try
            {
                _activeInstance?.Dispose();
            }
            catch { }
            finally
            {
                _activeInstance = null;
                _connectedInstanceName = "";
            }
        }
    }
}