using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media;
using TournamentAssistantUI.Misc;
using Color = System.Drawing.Color;
using Point = System.Windows.Point;

namespace TournamentAssistantUI.UI
{
    /// <summary>
    /// Interaction logic for DropperPage.xaml
    /// </summary>
    public partial class DropperPage : Page
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern int BitBlt(IntPtr hDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

        private Color _dropperColor;
        private Color DropperColor
        {
            get
            {
                return _dropperColor;
            }
            set
            {
                _dropperColor = value;
                Dispatcher.Invoke(() => ColorSampleRect.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(_dropperColor.A, _dropperColor.R, _dropperColor.G, _dropperColor.B)));
            }
        }

        public DropperPage()
        {
            InitializeComponent();

            MouseHook.LMouseUp += MouseHook_MouseUp;
            MouseHook.MouseMoved += MouseHook_MouseMoved;
            MouseHook.StartHook();
        }

        private void MouseHook_MouseUp()
        {
            MouseHook.StopHook();
        }

        private void MouseHook_MouseMoved(Point point)
        {
            DropperColor = GetColorAt(point);
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
