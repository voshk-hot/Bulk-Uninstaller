﻿/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using BulkCrapUninstaller.Functions;
using BulkCrapUninstaller.Functions.ApplicationList;
using Klocman.Forms.Tools;

namespace BulkCrapUninstaller.Controls
{
    [WindowStyleController.ControlStyle(false)]
    public partial class ListLegend : UserControl
    {
        public ListLegend()
        {
            InitializeComponent();
            
            flowLayoutPanellabelInvalid.BackColor = ApplicationListConstants.InvalidColor;
            flowLayoutPanellabelOrphaned.BackColor = ApplicationListConstants.UnregisteredColor;
            flowLayoutPanellabelUnverified.BackColor = ApplicationListConstants.UnverifiedColor;
            flowLayoutPanellabelVerified.BackColor = ApplicationListConstants.VerifiedColor;
            flowLayoutPanellabelWinFeature.BackColor = ApplicationListConstants.WindowsFeatureColor;
            flowLayoutPanellabelStoreApp.BackColor = ApplicationListConstants.WindowsStoreAppColor;
        }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public bool InvalidEnabled
        {
            get { return flowLayoutPanellabelInvalid.Visible; }
            set { flowLayoutPanellabelInvalid.Visible = value; }
        }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public bool WinFeatureEnabled
        {
            get { return flowLayoutPanellabelWinFeature.Visible; }
            set { flowLayoutPanellabelWinFeature.Visible = value; }
        }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public bool CertificatesEnabled
        {
            get { return flowLayoutPanellabelVerified.Visible; }
            set { flowLayoutPanellabelVerified.Visible = value; flowLayoutPanellabelUnverified.Visible = value; }
        }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public bool OrphanedEnabled
        {
            get { return flowLayoutPanellabelOrphaned.Visible; }
            set { flowLayoutPanellabelOrphaned.Visible = value; }
        }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public bool StoreAppEnabled
        {
            get { return flowLayoutPanellabelStoreApp.Visible; }
            set { flowLayoutPanellabelStoreApp.Visible = value; }
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            CloseRequested?.Invoke(sender, e);
        }

        private void ThisEnabledChanged(object sender, EventArgs e)
        {
            BackColor = Enabled ? SystemColors.ControlLightLight : SystemColors.Control;
        }

        public event EventHandler CloseRequested;
    }
}