using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using TournamentAssistantUI.Misc;
using Color = System.Drawing.Color;
using Point = System.Windows.Point;

namespace TournamentAssistantUI.UI.UserControls
{
    /// <summary>
    /// Interaction logic for UserDialog.xaml
    /// </summary>
    public partial class ColorDropperDialog : UserControl
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
                //Dispatcher.Invoke(() => ColorSampleRect.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(_dropperColor.A, _dropperColor.R, _dropperColor.G, _dropperColor.B)));
            }
        }

        public string Username { get; set; }

        private Action<Point> rMouseUpAction;
        private Point lastLocation = new Point(0, 0);

        public ColorDropperDialog(Action<Point> rMouseUpAction, string username)
        {
            DataContext = this;

            Username = username;

            this.rMouseUpAction = rMouseUpAction;

            InitializeComponent();

            MouseHook.LMouseUp += MouseHook_MouseUp;
            MouseHook.MouseMoved += MouseHook_MouseMoved;
            MouseHook.DisableRMouseDown = true;
            MouseHook.DisableRMouseUp = true;
            MouseHook.StartHook();
        }

        private void MouseHook_MouseUp()
        {
            rMouseUpAction?.Invoke(lastLocation);

            //Reset mouse hook
            MouseHook.StopHook();
            MouseHook.LMouseUp -= MouseHook_MouseUp;
            MouseHook.MouseMoved -= MouseHook_MouseMoved;
            MouseHook.DisableRMouseDown = false;
            MouseHook.DisableRMouseUp = false;
        }

        private void MouseHook_MouseMoved(Point point)
        {
            DropperColor = PixelReader.GetColorAt(point);
            lastLocation = point;
        }
    }
}
