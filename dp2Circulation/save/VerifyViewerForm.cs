﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using DigitalPlatform;

namespace dp2Circulation
{
    public partial class VerifyViewerForm : Form
    {
        public event DoDockEventHandler DoDockEvent = null;

        public bool Docked = false;

        // public MainForm MainForm = null;

        public event LocateEventHandler Locate = null;

        public VerifyViewerForm()
        {
            InitializeComponent();
        }

        public string ResultString
        {
            get
            {
                return this.textBox_verifyResult.Text;
            }
            set
            {
                this.textBox_verifyResult.Text = value;
            }
        }

        private void toolStripButton_dock_Click(object sender, EventArgs e)
        {
            DoDock(true);
        }

        public void DoDock(bool bShowFixedPanel)
        {
            /*
            this.MainForm.CurrentVerifyResultControl = this.textBox_verifyResult;
            if (bShowFixedPanel == true
                && this.MainForm.PanelFixedVisible == false)
                this.MainForm.PanelFixedVisible = true;

            this.Docked = true;
            this.Visible = false;
             * */
            if (this.DoDockEvent != null)
            {
                DoDockEventArgs e = new DoDockEventArgs();
                e.ShowFixedPanel = bShowFixedPanel;
                this.DoDockEvent(this, e);
            }
        }

        public void Clear()
        {
            this.textBox_verifyResult.Text = "";
        }

        public TextBox ResultControl
        {
            get
            {
                return this.textBox_verifyResult;
            }
        }

        private void textBox_verifyResult_DoubleClick(object sender, EventArgs e)
        {
            if (this.Locate == null)
                return;

            int x = 0;
            int y = 0;
            API.GetEditCurrentCaretPos(
                this.textBox_verifyResult,
                out x,
                out y);

            string strLine = "";

            try
            {
                strLine = textBox_verifyResult.Lines[y];
            }
            catch
            {
                return;
            }

            // 析出"(字段名，子字段名, 字符位置)"值

            int nRet = strLine.IndexOf("(");
            if (nRet == -1)
                return;
            strLine = strLine.Substring(nRet + 1);
            nRet = strLine.IndexOf(")");
            if (nRet != -1)
                strLine = strLine.Substring(0, nRet);
            strLine = strLine.Trim();

            LocateEventArgs e1 = new LocateEventArgs();
            e1.Location = strLine;
            this.Locate(this, e1);
        }
    }
}
