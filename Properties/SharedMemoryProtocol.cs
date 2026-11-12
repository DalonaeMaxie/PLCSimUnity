using System;
using System.Runtime.InteropServices;

namespace PLCSimUnity.Shared
{
    /// <summary>
    /// Shared status codes written by background service to shared memory (Offset 0)
    /// </summary>
    public enum ServiceStatus : byte
    {
        Uninitialized = 0,
        PlcSimNotRunning = 1,
        InstanceNotFound = 2,
        InstanceNotRunning = 3,
        Connected = 4,
        Error = 5
    }

    /// <summary>
    /// Runtime Screen (HMI/WebView) connection states
    /// </summary>
    public enum RuntimeScreenStatus : byte
    {
        Disconnected = 0,
        BrowserStarting = 1,
        BrowserConnecting = 2,
        Connected = 3,
        Error = 4
    }

    /// <summary>
    /// Shared memory header structure (128 bytes - expanded from 64 for dual connection tracking)
    /// Offset 0-63: Header information (PLC connection)
    /// Offset 64-127: Runtime screen connection info
    /// Offset 128+: Input/Output data buffers
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SharedMemoryHeader
    {
        // ========== PLC Connection Section (Bytes 0-63) ==========
        // Byte 0: Current status (ServiceStatus enum)
        public byte Status;

        // Byte 1: Operating state or error code
        public byte State;

        // Byte 2: Heartbeat counter (increments each cycle)
        public byte Heartbeat;

        // Byte 3: Current execution stage (for diagnostics)
        public byte CurrentStage;

        // Bytes 4-7: Input buffer byte count
        public int InputBytesCount;

        // Bytes 8-11: Output buffer byte count
        public int OutputBytesCount;

        // Bytes 12-15: Selected instance index (-1 = auto-discover, >= 0 = fixed instance)
        public int SelectedInstanceIndex;

        // Bytes 16-19: Reserved for instance name length
        public int SelectedInstanceNameLength;

        // Bytes 20-63: Selected instance name (max 44 chars)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 44)]
        public byte[] SelectedInstanceName;

        // ========== Runtime Screen Connection Section (Bytes 64-127) ==========
        // Byte 64: Runtime screen connection status (RuntimeScreenStatus enum)
        public byte RuntimeScreenStatus;

        // Byte 65: Runtime screen error code (if applicable)
        public byte RuntimeScreenErrorCode;

        // Bytes 66-69: Browser process ID (0 = not running)
        public int BrowserProcessId;

        // Bytes 70-73: Browser debug port number
        public int BrowserDebugPort;

        // Bytes 74-77: Frame rate (fps) - screen capture rate
        public int ScreenCaptureFrameRate;

        // Bytes 78-81: Last frame timestamp (milliseconds)
        public int LastFrameTimestamp;

        // Bytes 82-85: Total frames captured
        public int TotalFramesCaptured;

        // Bytes 86-97: Browser URL (max 12 chars - can expand if needed)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] BrowserUrl;

        // Bytes 98-127: Reserved for future use (30 bytes)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
        public byte[] Reserved;

        public string GetSelectedInstanceNameString()
        {
            if (SelectedInstanceNameLength <= 0 || SelectedInstanceName == null)
                return "";

            int length = Math.Min(SelectedInstanceNameLength, SelectedInstanceName.Length);
            return System.Text.Encoding.UTF8.GetString(SelectedInstanceName, 0, length);
        }

        public void SetSelectedInstanceName(string instanceName)
        {
            if (SelectedInstanceName == null)
                SelectedInstanceName = new byte[44];

            if (string.IsNullOrEmpty(instanceName))
            {
                SelectedInstanceNameLength = 0;
                Array.Clear(SelectedInstanceName, 0, SelectedInstanceName.Length);
            }
            else
            {
                byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(instanceName);
                SelectedInstanceNameLength = Math.Min(nameBytes.Length, 44);
                Array.Clear(SelectedInstanceName, 0, SelectedInstanceName.Length);
                Array.Copy(nameBytes, SelectedInstanceName, SelectedInstanceNameLength);
            }
        }

        public string GetBrowserUrlString()
        {
            if (BrowserUrl == null || BrowserUrl.Length == 0)
                return "";

            // Find first null terminator
            int length = System.Array.IndexOf(BrowserUrl, (byte)0);
            if (length < 0) length = BrowserUrl.Length;

            return System.Text.Encoding.UTF8.GetString(BrowserUrl, 0, Math.Min(length, BrowserUrl.Length));
        }

        public void SetBrowserUrl(string url)
        {
            if (BrowserUrl == null)
                BrowserUrl = new byte[12];

            if (string.IsNullOrEmpty(url))
            {
                Array.Clear(BrowserUrl, 0, BrowserUrl.Length);
            }
            else
            {
                byte[] urlBytes = System.Text.Encoding.UTF8.GetBytes(url);
                int length = Math.Min(urlBytes.Length, 11); // Leave room for null terminator
                Array.Clear(BrowserUrl, 0, BrowserUrl.Length);
                Array.Copy(urlBytes, BrowserUrl, length);
            }
        }
    }

    /// <summary>
    /// Utility class for shared memory protocol constants
    /// </summary>
    public static class SharedMemoryConstants
    {
        public const string DefaultShmName = "PLCSimUnity_SharedMemory";
        public const int HeaderSize = 128; // Expanded from 64 for dual connection tracking
        public const int MaxInstanceNameLength = 44;
        public const int MaxBrowserUrlLength = 12;

        // ========== PLC Connection Section Offsets (Bytes 0-63) ==========
        public const int Offset_Status = 0;
        public const int Offset_State = 1;
        public const int Offset_Heartbeat = 2;
        public const int Offset_CurrentStage = 3;
        public const int Offset_InputBytesCount = 4;
        public const int Offset_OutputBytesCount = 8;
        public const int Offset_SelectedInstanceIndex = 12;
        public const int Offset_SelectedInstanceNameLength = 16;
        public const int Offset_SelectedInstanceName = 20;

        // ========== Runtime Screen Connection Section Offsets (Bytes 64-127) ==========
        public const int Offset_RuntimeScreenStatus = 64;
        public const int Offset_RuntimeScreenErrorCode = 65;
        public const int Offset_BrowserProcessId = 66;
        public const int Offset_BrowserDebugPort = 70;
        public const int Offset_ScreenCaptureFrameRate = 74;
        public const int Offset_LastFrameTimestamp = 78;
        public const int Offset_TotalFramesCaptured = 82;
        public const int Offset_BrowserUrl = 86;
        public const int Offset_Reserved = 98;

        // Input/Output data buffers start after header
        public const int Offset_InputData = 128; // Was 64, now 128
    }
}
