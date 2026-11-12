using System;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Windows.Forms;
using PLCSimUnity.HMIViewport;
using PLCSimUnity.PLCSimUnityUI;
using PLCSimUnity.Shared;

namespace PLCSimUnity
{
    static class Program
    {
        private static MemoryMappedFile _mmf;
        private static MemoryMappedViewAccessor _view;
        private static CancellationTokenSource _cts = new CancellationTokenSource();

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) => ShowErrorDialog("UI Thread Exception", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => ShowErrorDialog("Unhandled Exception", e.ExceptionObject as Exception);

            try
            {
                string shmName = "PLCSimUnity_SharedMemory";
                string instanceName = "";
                int inputBytesCount = 64;
                int outputBytesCount = 64;

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].Equals("--shm", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        shmName = args[++i];
                    else if (args[i].Equals("--instance", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        instanceName = args[++i];
                    else if (args[i].Equals("--inputs", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out int inputs))
                    {
                        inputBytesCount = inputs;
                        i++;
                    }
                    else if (args[i].Equals("--outputs", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out int outputs))
                    {
                        outputBytesCount = outputs;
                        i++;
                    }
                }

                int frameBufferSize = 1920 * 1080 * 4;
                int totalShmSize = SharedMemoryConstants.HeaderSize + inputBytesCount + outputBytesCount + frameBufferSize;

                _mmf = MemoryMappedFile.CreateOrOpen(shmName, totalShmSize);
                _view = _mmf.CreateViewAccessor();

                if (!string.IsNullOrEmpty(instanceName) && !instanceName.Equals("AutoDiscover", StringComparison.OrdinalIgnoreCase))
                {
                    byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(instanceName);
                    int len = Math.Min(nameBytes.Length, SharedMemoryConstants.MaxInstanceNameLength);
                    _view.Write(SharedMemoryConstants.Offset_SelectedInstanceNameLength, len);
                    _view.WriteArray(SharedMemoryConstants.Offset_SelectedInstanceName, nameBytes, 0, len);
                }

                // Call the worker directly from InteractiveInstanceSelector.cs
                InteractiveInstanceSelector.StartWorker(_view, _cts.Token);

                // Start WinCC Runtime Monitoring
                WinCCRuntimeMonitor.StartMonitoring(_view, _cts.Token);

                // Start WinForms UI Event Loop
                Application.Run(new DashboardForm(_view));
            }
            catch (Exception ex)
            {
                ShowErrorDialog("PLCSimUnity Startup Error", ex);
            }
            finally
            {
                _cts.Cancel();
                _view?.Dispose();
                _mmf?.Dispose();
            }
        }

        private static void ShowErrorDialog(string title, Exception ex)
        {
            string message = ex != null
                ? $"An error occurred during runtime execution:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}"
                : "An unexpected error occurred.";

            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}