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

        private Panel panel;
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        // This code example demonstrates using the Padding property to
        // create a border around a RichTextBox control.
        public PrimaryDisplayHighlighter()
        {
            InitializeComponent();

            BackColor = Color.Green;
            TransparencyKey = Color.Blue;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = Screen.PrimaryScreen.Bounds;
            TopMost = true;

            panel.BackColor = Color.Blue;
            panel.Padding = new Padding(5);
            panel.Dock = DockStyle.Fill;

            uint initialStyle = GetWindowLong(Handle, -20);
            SetWindowLong(Handle, -20, initialStyle | 0x80000 | 0x20);
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
            this.panel = new Panel();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel.Location = new Point(30, 31);
            this.panel.Margin = new Padding(4, 5, 4, 5);
            this.panel.Name = "panel1";
            this.panel.Size = new Size(736, 482);
            this.panel.TabIndex = 0;
            // 
            // PrimaryDisplayHighlighter
            // 
            this.AutoScaleDimensions = new SizeF(9F, 20F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(796, 543);
            this.Controls.Add(this.panel);
            this.Margin = new Padding(4, 5, 4, 5);
            this.Name = "PrimaryDisplayHighlighter";
            this.Padding = new Padding(30, 31, 30, 31);
            this.Text = "Form1";
            this.ResumeLayout(false);

        }

        #endregion
    }
}
