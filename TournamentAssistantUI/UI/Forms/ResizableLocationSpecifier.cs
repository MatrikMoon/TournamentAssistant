using System;
using System.Drawing;
using System.Windows.Forms;

namespace TournamentAssistantUI.UI.Forms
{
    class ResizableLocationSpecifier : Form
    {
        public event Action<int, int, Size> LocationOrSizeChanged;

        private Panel panel;

        public ResizableLocationSpecifier()
        {
            InitializeComponent();

            BackColor = Color.Green;
            TransparencyKey = Color.Blue;
            FormBorderStyle = FormBorderStyle.Sizable;
            SizeGripStyle = SizeGripStyle.Hide;
            Bounds = new Rectangle(0, 0, 1920, 1080);
            LocationChanged += ResizableLocationSpecifier_LocationChanged;
            SizeChanged += ResizableLocationSpecifier_SizeChanged;

            panel.BackColor = Color.Blue;
            panel.Padding = new Padding(5);
            panel.Dock = DockStyle.Fill;
        }

        private void ResizableLocationSpecifier_SizeChanged(object sender, EventArgs e)
        {
            LocationOrSizeChanged?.Invoke(Bounds.X, Bounds.Y, Bounds.Size);
        }

        private void ResizableLocationSpecifier_LocationChanged(object sender, EventArgs e)
        {
            LocationOrSizeChanged?.Invoke(Bounds.X, Bounds.Y, Bounds.Size);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panel = new Panel();
            SuspendLayout();
            // 
            // panel1
            // 
            panel.Location = new Point(30, 31);
            panel.Margin = new Padding(4, 5, 4, 5);
            panel.Name = "panel";
            panel.Size = new Size(736, 482);
            panel.TabIndex = 0;
            // 
            // PrimaryDisplayHighlighter
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(796, 543);
            Controls.Add(panel);
            Margin = new Padding(4, 5, 4, 5);
            Name = "PrimaryDisplayHighlighter";
            Padding = new Padding(30, 31, 30, 31);
            Text = "Drag this so it encapsulates the multistream";
            ResumeLayout(false);

        }

        #endregion
    }
}
