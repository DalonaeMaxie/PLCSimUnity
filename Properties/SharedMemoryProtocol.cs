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
    /// Shared memory header structure (64 bytes)
    /// Offset 0-63: Header information
    /// Offset 64+: Input/Output data buffers
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SharedMemoryHeader
    {
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
    }

    /// <summary>
    /// Utility class for shared memory protocol constants
    /// </summary>
    public static class SharedMemoryConstants
    {
        public const string DefaultShmName = "PLCSimUnity_SharedMemory";
        public const int HeaderSize = 64;
        public const int MaxInstanceNameLength = 44;

        // Shared memory offsets (for direct accessor reads)
        public const int Offset_Status = 0;
        public const int Offset_State = 1;
        public const int Offset_Heartbeat = 2;
        public const int Offset_CurrentStage = 3;
        public const int Offset_InputBytesCount = 4;
        public const int Offset_OutputBytesCount = 8;
        public const int Offset_SelectedInstanceIndex = 12;
        public const int Offset_SelectedInstanceNameLength = 16;
        public const int Offset_SelectedInstanceName = 20;
        public const int Offset_InputData = 64; // After header
    }
}
