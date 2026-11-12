using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using PLCSimUnity.Shared;

namespace PLCSimUnity.HMIViewport
{
    public static class WinCCRuntimeMonitor
    {
        private static readonly string[] WinCCProcessNames = new string[]
        {
            "HmiRTm",                       // Standard WinCC Runtime Advanced executable
            "Siemens.Simatic.Hmi.Runtime",  // Extended TIA Portal RT host
            "HmiKpa"                        // Legacy WinCC RT
        };

        public static void StartMonitoring(MemoryMappedViewAccessor view, CancellationToken token)
        {
            Thread monitorThread = new Thread(() =>
            {
                int frameCounter = 0;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        Process winCcProc = GetWinCCProcess();

                        if (winCcProc != null && !winCcProc.HasExited)
                        {
                            IntPtr hWnd = winCcProc.MainWindowHandle;

                            // Write connection telemetry
                            view.Write(SharedMemoryConstants.Offset_RuntimeScreenStatus, (byte)RuntimeScreenStatus.Connected);
                            view.Write(SharedMemoryConstants.Offset_BrowserProcessId, winCcProc.Id);
                            view.Write(SharedMemoryConstants.Offset_BrowserDebugPort, 0); // Native app
                            view.Write(SharedMemoryConstants.Offset_ScreenCaptureFrameRate, 30);

                            // Capture live frame
                            byte[] frameBuffer = WinCCScreenCapturer.CaptureWindowFrame(hWnd, out int width, out int height);
                            if (frameBuffer != null && frameBuffer.Length > 0)
                            {
                                frameCounter++;
                                view.Write(SharedMemoryConstants.Offset_TotalFramesCaptured, frameCounter);
                                view.Write(SharedMemoryConstants.Offset_LastFrameTimestamp, Environment.TickCount);

                                int pixelDataOffset = SharedMemoryConstants.HeaderSize + 128; // Offset 256
                                view.WriteArray(pixelDataOffset, frameBuffer, 0, Math.Min(frameBuffer.Length, 1920 * 1080 * 4));
                            }
                        }
                        else
                        {
                            view.Write(SharedMemoryConstants.Offset_RuntimeScreenStatus, (byte)RuntimeScreenStatus.Disconnected);
                            view.Write(SharedMemoryConstants.Offset_BrowserProcessId, 0);
                        }
                    }
                    catch
                    {
                        view.Write(SharedMemoryConstants.Offset_RuntimeScreenStatus, (byte)RuntimeScreenStatus.Error);
                    }

                    Thread.Sleep(33); // Stream at ~30 FPS (33ms per frame)
                }
            })
            { IsBackground = true };

            monitorThread.Start();
        }

        private static Process GetWinCCProcess()
        {
            foreach (string procName in WinCCProcessNames)
            {
                Process proc = Process.GetProcessesByName(procName).FirstOrDefault(p => !p.HasExited);
                if (proc != null) return proc;
            }
            return null;
        }
    }
}