using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PLCSimUnity.HMIViewport
{
    public static class WinCCScreenCapturer
    {
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const uint PW_RENDERFULLCONTENT = 0x00000002;

        public static byte[] CaptureWindowFrame(IntPtr hWnd, int targetWidth, int targetHeight, out int width, out int height)
        {
            width = targetWidth;
            height = targetHeight;

            if (hWnd == IntPtr.Zero || targetWidth <= 0 || targetHeight <= 0) return null;

            GetWindowRect(hWnd, out RECT rect);
            int sourceWidth = rect.Right - rect.Left;
            int sourceHeight = rect.Bottom - rect.Top;

            if (sourceWidth <= 0 || sourceHeight <= 0) return null;

            using (Bitmap source = new Bitmap(sourceWidth, sourceHeight, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(source))
                {
                    IntPtr hdc = g.GetHdc();
                    try
                    {
                        PrintWindow(hWnd, hdc, PW_RENDERFULLCONTENT);
                    }
                    finally
                    {
                        g.ReleaseHdc(hdc);
                    }
                }

                using (Bitmap frame = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(frame))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(source, new Rectangle(0, 0, targetWidth, targetHeight));
                    }

                    BitmapData data = frame.LockBits(
                        new Rectangle(0, 0, targetWidth, targetHeight),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format32bppArgb);
                    try
                    {
                        int rowBytes = targetWidth * 4;
                        byte[] pixels = new byte[rowBytes * targetHeight];
                        for (int y = 0; y < targetHeight; y++)
                        {
                            IntPtr row = IntPtr.Add(data.Scan0, y * data.Stride);
                            Marshal.Copy(row, pixels, y * rowBytes, rowBytes);
                        }

                        return pixels;
                    }
                    finally
                    {
                        frame.UnlockBits(data);
                    }
                }
            }
        }
    }
}