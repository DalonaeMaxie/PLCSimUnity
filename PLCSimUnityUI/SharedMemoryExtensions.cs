using System;
using System.IO.MemoryMappedFiles;
using System.Text;
using PLCSimUnity.Shared;

namespace PLCSimUnity.UI
{
    /// <summary>
    /// Extension methods for MemoryMappedViewAccessor to simplify reading/writing selected instance
    /// </summary>
    public static class SharedMemoryExtensions
    {
        /// <summary>
        /// Writes a selected instance name to the shared memory header
        /// </summary>
        public static void SetSelectedInstanceName(this MemoryMappedViewAccessor view, string instanceName)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            byte[] nameBytes = string.IsNullOrEmpty(instanceName) 
                ? new byte[0] 
                : Encoding.UTF8.GetBytes(instanceName);

            int lengthToWrite = Math.Min(nameBytes.Length, SharedMemoryConstants.MaxInstanceNameLength);

            // Write length
            view.Write(SharedMemoryConstants.Offset_SelectedInstanceNameLength, lengthToWrite);

            // Clear the name buffer
            byte[] clearBuffer = new byte[SharedMemoryConstants.MaxInstanceNameLength];
            view.WriteArray(SharedMemoryConstants.Offset_SelectedInstanceName, clearBuffer, 0, clearBuffer.Length);

            // Write new name
            if (lengthToWrite > 0)
            {
                view.WriteArray(SharedMemoryConstants.Offset_SelectedInstanceName, nameBytes, 0, lengthToWrite);
            }
        }

        /// <summary>
        /// Reads the selected instance name from shared memory header
        /// </summary>
        public static string GetSelectedInstanceName(this MemoryMappedViewAccessor view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            int length = view.ReadInt32(SharedMemoryConstants.Offset_SelectedInstanceNameLength);
            if (length <= 0)
                return "";

            byte[] nameBuffer = new byte[length];
            view.ReadArray(SharedMemoryConstants.Offset_SelectedInstanceName, nameBuffer, 0, length);
            return Encoding.UTF8.GetString(nameBuffer);
        }

        /// <summary>
        /// Reads the current service status
        /// </summary>
        public static ServiceStatus GetServiceStatus(this MemoryMappedViewAccessor view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            byte status = view.ReadByte(SharedMemoryConstants.Offset_Status);
            return (ServiceStatus)status;
        }
    }
}
