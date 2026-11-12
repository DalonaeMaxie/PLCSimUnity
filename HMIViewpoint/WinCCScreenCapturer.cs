using System;
using System.Drawing;
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

        public static byte[] CaptureWindowFrame(IntPtr hWnd, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (hWnd == IntPtr.Zero) return null;

            GetWindowRect(hWnd, out RECT rect);
            width = rect.Right - rect.Left;
            height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0) return null;

            using (Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    IntPtr hdc = g.GetHdc();
                    try
                    {
                        // Print native window content to GDI graphics context
                        PrintWindow(hWnd, hdc, PW_RENDERFULLCONTENT);
                    }
                    finally
                    {
                        g.ReleaseHdc(hdc);
                    }
                }

                // Convert Bitmap to raw BGRA byte array for fast GPU texturing
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bmp.PixelFormat);
                int bytes = Math.Abs(bmpData.Stride) * bmp.Height;
                byte[] rgbValues = new byte[bytes];
                Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);
                bmp.UnlockBits(bmpData);

                return rgbValues;
            }
        }
    }
}