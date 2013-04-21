/**
 * Copyright (c) 2013, Robpol86
 * This software is made available under the terms of the MIT License that can
 * be found in the LICENSE.txt file.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnofficialDDNS {
    public partial class UnofficialDDNS : ServiceBase {
        private Thread m_oMainThread = null;

        public UnofficialDDNS() {
            InitializeComponent();
            LogSingleton.Instance.SetEventLog( eventLog );
        }

        protected override void OnStart( string[] saArgs ) {
            // DEBUG
            //byte[] input = ProtectedData.Protect(Encoding.ASCII.GetBytes("ABCD1234"), null, DataProtectionScope.LocalMachine);
            //RegistryKey reg = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\AutomaticDDNS", true);
            //reg.SetValue("namedotcom_apitoken", input);
            //reg.Close();
            //System.Threading.Thread.Sleep(10000);
            // TODO
            // remove as many using statements everywhere.
            // test wrong url scenarios.
            // enable Optimized code.
            
            // Read data from registry..
            RegistryRead oConfig;
            try {
                oConfig = new RegistryRead();
            } catch ( RegistryException e ) {
                this.AutoLog = false;
                this.ExitCode = 2611;
                LogSingleton.Instance.Log( e.m_iCode, RegistryRead.RegPath );
                throw;
            }

            // Verify data from registry.
            int iThrow = 0;
            if ( oConfig.Interval < 1 ) iThrow = 10100;
            if ( oConfig.IntervalError < 1 ) iThrow = 10101;
            if ( oConfig.Registrar != "namedotcom" ) iThrow = 10102;
            if ( oConfig.UserName == null || oConfig.UserName.Length < 1 ) iThrow = 10103;
            if ( oConfig.Domain == null || oConfig.Domain.Length < 1 ) iThrow = 10104;
            if ( iThrow != 0 ) {
                this.AutoLog = false;
                this.ExitCode = 2611;
                LogSingleton.Instance.Log( iThrow, RegistryRead.RegPath );
                throw new RegistryException( iThrow );
            }

            // Start main thread.
            MainPollingThread oMainThread = new MainPollingThread( oConfig );
            if ( m_oMainThread == null ) m_oMainThread = new Thread( oMainThread.TheThread );
            if ( !m_oMainThread.IsAlive ) m_oMainThread.Start();
        }

        protected override void OnStop() {
            m_oMainThread.Abort();
        }
    }
}
