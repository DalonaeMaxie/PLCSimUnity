using System;
using System.Diagnostics;
using System.IO;

namespace PLCSimUnity.UI
{
    /// <summary>
    /// Manages the lifecycle of the background PLCSimUnity service process
    /// </summary>
    public class ProcessManager
    {
        private Process _serviceProcess;
        private readonly string _serviceName = "PLCSimUnity.exe";
        private DateTime _lastStartAttempt = DateTime.MinValue;
        private const int StartAttemptCooldownMs = 2000; // Wait 2 seconds between restart attempts

        public bool IsServiceRunning => _serviceProcess != null && !_serviceProcess.HasExited;

        public event EventHandler ServiceStarted;
        public event EventHandler ServiceStopped;
        public event EventHandler<string> ServiceError;

        /// <summary>
        /// Finds and returns the path to the background service executable
        /// </summary>
        private string FindServiceExecutable()
        {
            // Try common locations
            string[] possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _serviceName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", _serviceName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", _serviceName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PLCSimUnity", _serviceName)
            };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            return null;
        }

        /// <summary>
        /// Starts the background service process with optional instance name
        /// </summary>
        public bool StartService(string instanceName = null, int inputBytes = 64, int outputBytes = 64)
        {
            // Check cooldown period to avoid spam
            if ((DateTime.UtcNow - _lastStartAttempt).TotalMilliseconds < StartAttemptCooldownMs)
            {
                return IsServiceRunning;
            }
            _lastStartAttempt = DateTime.UtcNow;

            if (IsServiceRunning)
            {
                return true; // Already running
            }

            try
            {
                string servicePath = FindServiceExecutable();
                if (string.IsNullOrEmpty(servicePath))
                {
                    ServiceError?.Invoke(this, $"Could not find {_serviceName}. Make sure it's in the application directory.");
                    return false;
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = servicePath,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    CreateNoWindow = true,
                    Arguments = BuildServiceArgumentString(instanceName, inputBytes, outputBytes)
                };

                _serviceProcess = new Process { StartInfo = psi };
                _serviceProcess.EnableRaisingEvents = true;
                _serviceProcess.Exited += (s, e) =>
                {
                    ServiceStopped?.Invoke(this, EventArgs.Empty);
                };

                _serviceProcess.Start();
                ServiceStarted?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                ServiceError?.Invoke(this, $"Failed to start service: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stops the background service process gracefully
        /// </summary>
        public void StopService()
        {
            if (_serviceProcess != null && !_serviceProcess.HasExited)
            {
                try
                {
                    _serviceProcess.Kill();
                    _serviceProcess.WaitForExit(5000);
                    ServiceStopped?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    ServiceError?.Invoke(this, $"Failed to stop service: {ex.Message}");
                }
                finally
                {
                    _serviceProcess?.Dispose();
                    _serviceProcess = null;
                }
            }
        }

        /// <summary>
        /// Restarts the background service
        /// </summary>
        public bool RestartService(string instanceName = null, int inputBytes = 64, int outputBytes = 64)
        {
            StopService();
            System.Threading.Thread.Sleep(500); // Brief pause before restart
            return StartService(instanceName, inputBytes, outputBytes);
        }

        /// <summary>
        /// Builds command-line arguments for the service
        /// </summary>
        private string BuildServiceArgumentString(string instanceName, int inputBytes, int outputBytes)
        {
            string args = "";

            if (!string.IsNullOrEmpty(instanceName))
            {
                args += $"--instance \"{instanceName}\" ";
            }

            if (inputBytes != 64)
            {
                args += $"--inputs {inputBytes} ";
            }

            if (outputBytes != 64)
            {
                args += $"--outputs {outputBytes} ";
            }

            return args.Trim();
        }

        /// <summary>
        /// Checks if the service process is still running by looking for it by name
        /// </summary>
        public bool ServiceExistsInTaskManager()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("PLCSimUnity");
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
