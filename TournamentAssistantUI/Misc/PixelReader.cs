using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Color = System.Drawing.Color;
using Point = System.Windows.Point;

/**
 * Created by Moon on 10/13/2019 while on a plane to Atlanta
 * The purpose of this class is to trigger a callback when a particular pixel
 * has a particular color. At first I will do this with a while loop, but
 * in the end it should probably be changed to use something less resource-murdering.
 * TODO: Use some sort of callback instead of a while loop to check for the callback condition
 */

namespace TournamentAssistantUI.Misc
{
    class PixelReader
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern int BitBlt(IntPtr hDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

        private Point location;
        private Func<Color, bool> condition;
        private Action callback;

        public PixelReader(Point location, Func<Color, bool> condition, Action callback)
        {
            this.location = location;
            this.condition = condition;
            this.callback = callback;
        }

        public void StartWatching()
        {
            Task.Run(() =>
            {
                while (!condition(GetColorAt(location))) Thread.Sleep(0);
                callback();
            });
        }

        public Color GetColorAt(Point location)
        {
            Bitmap screenPixel = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics gdest = Graphics.FromImage(screenPixel))
            {
                using (Graphics gsrc = Graphics.FromHwnd(IntPtr.Zero))
                {
                    IntPtr hSrcDC = gsrc.GetHdc();
                    IntPtr hDC = gdest.GetHdc();
                    int retval = BitBlt(hDC, 0, 0, 1, 1, hSrcDC, (int)location.X, (int)location.Y, (int)CopyPixelOperation.SourceCopy);
                    gdest.ReleaseHdc();
                    gsrc.ReleaseHdc();
                }
            }

            return screenPixel.GetPixel(0, 0);
        }
    }
}
