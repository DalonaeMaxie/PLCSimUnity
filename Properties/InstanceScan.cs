using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Siemens.Simatic.Simulation.Runtime;

namespace PLCSimUnity
{
    public class InstanceSummary
    {
        public string Name { get; set; }
        public int ID { get; set; }
        public string CpuType { get; set; }
        public EOperatingState OperatingState { get; set; }
        public bool IsActive { get; set; }
    }

    internal class PlcScanner
    {
        private const int ScanTimeoutMs = 3000; // 3 second timeout per instance scan

        /// <summary>
        /// Scans for all registered PLCSIM Advanced instances on the local runtime manager.
        /// </summary>
        public static List<InstanceSummary> ScanForInstances(bool verbose = true)
        {
            List<InstanceSummary> discoveredInstances = new List<InstanceSummary>();

            if (!SimulationRuntimeManager.IsRuntimeManagerAvailable)
            {
                if (verbose) Console.WriteLine("[PLCSim Scanner] Runtime Manager service is not available.");
                return discoveredInstances;
            }

            SInstanceInfo[] registered = SimulationRuntimeManager.RegisteredInstanceInfo;
            if (registered == null || registered.Length == 0)
            {
                if (verbose) Console.WriteLine("[PLCSim Scanner] No registered instances found. Rescanning...");
                return discoveredInstances;
            }

            if (verbose)
            {
                Console.WriteLine($"[PLCSim Scanner] Discovered {registered.Length} instance(s):");
                Console.WriteLine(new string('-', 65));
            }

            foreach (SInstanceInfo info in registered)
            {
                InstanceSummary summary = new InstanceSummary
                {
                    Name = info.Name,
                    ID = info.ID,
                    CpuType = "Unknown",
                    OperatingState = EOperatingState.Off,
                    IsActive = false
                };

                // Use task with timeout to prevent hanging on interface creation
                try
                {
                    var inspectTask = Task.Run(() =>
                    {
                        using (IInstance tempInterface = SimulationRuntimeManager.CreateInterface(info.Name))
                        {
                            summary.OperatingState = tempInterface.OperatingState;
                            summary.CpuType = tempInterface.CPUType.ToString();
                            summary.IsActive = true;
                        }
                    });

                    // Wait for task with timeout
                    if (!inspectTask.Wait(TimeSpan.FromMilliseconds(ScanTimeoutMs)))
                    {
                        if (verbose) Console.WriteLine($" [Warning] Timeout scanning '{info.Name}': PLC interface not responding.");
                    }
                }
                catch (Exception ex)
                {
                    if (verbose) Console.WriteLine($" [Warning] Could not inspect '{info.Name}': {ex.Message}");
                }

                discoveredInstances.Add(summary);

                if (verbose)
                {
                    Console.WriteLine($" -> Name: {summary.Name,-15} | ID: {summary.ID,-3} | Type: {summary.CpuType,-12} | State: {summary.OperatingState}");
                }
            }

            if (verbose) Console.WriteLine(new string('-', 65));

            return discoveredInstances;
        }
    }
}