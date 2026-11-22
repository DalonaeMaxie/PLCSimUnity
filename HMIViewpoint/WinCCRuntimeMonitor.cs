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
                int frameSequence = view.ReadInt32(SharedMemoryConstants.Offset_FrameSequence) & ~1;

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

                            // Capture a fixed-size frame so its dimensions and row stride match Unity's texture.
                            byte[] frameBuffer = WinCCScreenCapturer.CaptureWindowFrame(hWnd, 1920, 1080, out int width, out int height);
                            if (frameBuffer != null && frameBuffer.Length == width * height * 4)
                            {
                                int pixelDataOffset = SharedMemoryConstants.HeaderSize + 128; // Offset 256
                                int writingSequence = unchecked(frameSequence + 1);
                                int completedSequence = unchecked(frameSequence + 2);
                                view.Write(SharedMemoryConstants.Offset_FrameSequence, writingSequence);
                                view.WriteArray(pixelDataOffset, frameBuffer, 0, frameBuffer.Length);
                                view.Write(SharedMemoryConstants.Offset_FrameSequence, completedSequence);
                                frameSequence = completedSequence;
                                view.Write(SharedMemoryConstants.Offset_LastFrameTimestamp, Environment.TickCount);
                                frameCounter++;
                                view.Write(SharedMemoryConstants.Offset_TotalFramesCaptured, frameCounter);
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