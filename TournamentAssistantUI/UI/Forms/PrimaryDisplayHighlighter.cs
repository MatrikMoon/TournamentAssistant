using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

/**
 * Created by Moon on 5/14/2020
 * Draws a green border around the Primary monitor when active
 */

namespace TournamentAssistantUI.UI.Forms
{
    class PrimaryDisplayHighlighter : Form
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        private Panel panel;
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        // This code example demonstrates using the Padding property to
        // create a border around a RichTextBox control.
        public PrimaryDisplayHighlighter(Screen screen, Color? color = null)
        {
            InitializeComponent();

            Rectangle bounds = screen.Bounds;

            BackColor = color ?? Color.Green;
            TransparencyKey = Color.Blue;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = bounds;
            TopMost = true;

            panel.BackColor = Color.Blue;
            panel.Padding = new Padding(5);
            panel.Dock = DockStyle.Fill;

            uint initialStyle = GetWindowLong(Handle, -20);
            SetWindowLong(Handle, -20, initialStyle | 0x80000 | 0x20);
            MoveWindow(Handle, bounds.Left, bounds.Top, bounds.Width, bounds.Height, false);
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panel = new System.Windows.Forms.Panel();
            this.SuspendLayout();
            // 
            // panel
            // 
            this.panel.Location = new System.Drawing.Point(20, 20);
            this.panel.Name = "panel";
            this.panel.Size = new System.Drawing.Size(491, 313);
            this.panel.TabIndex = 0;
            // 
            // PrimaryDisplayHighlighter
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(531, 353);
            this.Controls.Add(this.panel);
            this.Name = "PrimaryDisplayHighlighter";
            this.Padding = new System.Windows.Forms.Padding(20, 20, 20, 20);
            this.ShowInTaskbar = false;
            this.Text = "StreamSync";
            this.ResumeLayout(false);

        }

        #endregion
    }
}
