using System;
using System.Drawing;
using System.Windows.Forms;

namespace TournamentAssistantUI.UI.Forms
{
    class ResizableLocationSpecifier : Form
    {
        public Action<int, int, Size> LocationOrSizeChanged;

        public ResizableLocationSpecifier()
        {
            BackColor = Color.LimeGreen;
            TransparencyKey = Color.LimeGreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = new Rectangle(0, 0, 1920, 1080);
            TopMost = true;
            LocationChanged += ResizableLocationSpecifier_LocationChanged;
            SizeChanged += ResizableLocationSpecifier_SizeChanged;
        }

        private void ResizableLocationSpecifier_SizeChanged(object sender, EventArgs e)
        {
            LocationOrSizeChanged?.Invoke(Bounds.X, Bounds.Y, Bounds.Size);
        }

        private void ResizableLocationSpecifier_LocationChanged(object sender, EventArgs e)
        {
            LocationOrSizeChanged?.Invoke(Bounds.X, Bounds.Y, Bounds.Size);
        }
    }
}
