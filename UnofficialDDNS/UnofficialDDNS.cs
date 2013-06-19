// ***********************************************************************
// Assembly         : UnofficialDDNS
// Author           : Robpol86
// Created          : 04-20-2013
//
// Last Modified By : Robpol86
// Last Modified On : 06-18-2013
// ***********************************************************************
// <copyright file="UnofficialDDNS.cs" company="Robpol86">
//      Copyright (c) 2013 All rights reserved.
//      This software is made available under the terms of the MIT License
//      that can be found in the LICENSE.txt file.
// </copyright>
// <summary>
//      Holds the service OnStart, OnStop, and worker thread. Main code
//      for the service.
// </summary>
// ***********************************************************************

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using UDDNSQuery;

namespace UnofficialDDNS {
    /// <summary>
    /// UnofficialDDNS service class with OnStart/OnStop methods and the worker thread that periodically queries the
    /// registrar's API through an external DLL.
    /// </summary>
    [System.ComponentModel.DesignerCategory( "Code" )]
    public partial class UnofficialDDNS : ServiceBase {
        /// <summary>
        /// QueryAPI instance from UDDNSQuery.dll.
        /// </summary>
        private IQueryAPI _api;
        /// <summary>
        /// Cancellation Token source used by OnStop.
        /// </summary>
        private CancellationTokenSource _cts;
        /// <summary>
        /// Holds the current polling thread, which executes PollingThreadWorker().
        /// </summary>
        private Thread _pollingThread;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnofficialDDNS" /> class.
        /// </summary>
        public UnofficialDDNS() {
            CanPauseAndContinue = false;
            ServiceName = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>().Product;
            AutoLog = true;
        }

        /// <summary>
        /// Implementation of OnStart. Executes when a Start command is sent to the service by the Service Control
        /// Manager (SCM) or when the operating system starts. Specifies actions to take when the service starts.
        /// </summary>
        /// <param name="args">Data passed by the start command.</param>
        /// <exception cref="QueryAPIException" />
        protected override void OnStart( string[] args ) {
            // Read data from registry.
            RegistryKey regKey = Registry.LocalMachine.OpenSubKey( @"SOFTWARE\UnofficialDDNS" );
            IDictionary<string, string> regPack = new Dictionary<string, string>();
            try {
                if ( (int) regKey.GetValue( "Debug", 0 ) == 1 ) LogSingleton.I.EnableDebug = true;
                regPack.Add( "interval", ((int) regKey.GetValue( "Interval", "" )).ToString() );
                regPack.Add( "registrar", (string) regKey.GetValue( "Registrar", "" ) );
                regPack.Add( "userName", (string) regKey.GetValue( "Username", "" ) );
                regPack.Add( "apiToken", (string) regKey.GetValue( "ApiToken", "" ) );
                regPack.Add( "domain", (string) regKey.GetValue( "Domain", "" ) );
            } catch ( NullReferenceException ) { // Key doesn't exist.
                ExitCode = 1012;
                throw;
            } catch ( InvalidCastException ) { // Integer values aren't integers, or string values aren't strings.
                ExitCode = 1010;
                throw;
            }

            // Validate data from registry.
            try {
                if ( regPack["registrar"].Length == 0 ) throw new QueryAPIException( 103 );
                if ( regPack["userName"].Length == 0 ) throw new QueryAPIException( 100 );
                if ( regPack["apiToken"].Length == 0 ) throw new QueryAPIException( 101 );
                if ( regPack["domain"].Length == 0 ) throw new QueryAPIException( 102 );
                using ( IQueryAPI api = QueryAPIIndex.I.Factory( regPack["registrar"] ) ) {
                    // Just testing for Error104.
                }
            } catch ( QueryAPIException err ) {
                string text = String.Format( "Error {0}: {1}", err.Code.ToString(), err.RMessage );
                if ( err.Details != null ) text += "\n\n" + err.Details;
                if ( err.Url != null ) text += "\n\n" + err.Url;
                LogSingleton.I.Error( err.Code, text );
                ExitCode = 1011;
                throw;
            }
            
            // Start main thread.
            LogSingleton.I.Debug( "Initializing polling thread." );
            _cts = new CancellationTokenSource();
            _pollingThread = new Thread( (ThreadStart) delegate { PollingThreadWorker( regPack ); } );
            _pollingThread.Start();
            LogSingleton.I.Debug( "Polling thread started." );
        }

        /// <summary>
        /// Implemented of OnStop. Executes when a Stop command is sent to the service by the Service Control Manager
        /// (SCM). Specifies actions to take when a service stops running.
        /// </summary>
        protected override void OnStop() {
            try { _api.UserCanceled = true; } catch ( NullReferenceException ) { }
            _cts.Cancel();
            Thread.Sleep( 500 );
            _pollingThread.Abort();
        }

        /// <summary>
        /// Called from a thread delegate. Does the actual work by querying the registrar's API through UDDNSQuery.dll
        /// and then sleeps according to the interval set in the registry.
        /// </summary>
        /// <param name="regPack">The reg pack.</param>
        private async void PollingThreadWorker( IDictionary<string, string> regPack ) {
            while ( !_cts.IsCancellationRequested ) {
                int sleep = Convert.ToInt32( regPack["interval"] ) * 60 * 1000; // [minutes] * seconds * milliseconds
                LogSingleton.I.Debug( String.Format( "Sleep set to {0} milliseconds.", sleep ) );

                try {
                    LogSingleton.I.Debug( "Initializing QueryAPI object." );
                    using ( _api = QueryAPIIndex.I.Factory( regPack["registrar"] ) ) {
                        // Pass credentials to class instance.
                        LogSingleton.I.Debug( "Setting registrar credentials to object instance." );
                        _api.Credentials( regPack["userName"], regPack["apiToken"].Replace( "ENCRYPTED:", "" ),
                            regPack["domain"]
                            );

                        // Read only.
                        LogSingleton.I.Debug( "Executing GetCurrentIPAsync()" );
                        await _api.GetCurrentIPAsync( _cts.Token );
                        LogSingleton.I.Debug( "Executing AuthenticateAsync()" );
                        await _api.AuthenticateAsync( _cts.Token );
                        LogSingleton.I.Debug( "Executing ValidateDomainAsync()" );
                        await _api.ValidateDomainAsync( _cts.Token );
                        LogSingleton.I.Debug( "Executing GetRecordsAsync()" );
                        await _api.GetRecordsAsync( _cts.Token );

                        // Check if DNS is outdated.
                        LogSingleton.I.Debug( "Recorded IP(s): " + string.Join( ",", _api.RecordedIP.Values ) );
                        if ( _api.RecordedIP.Count != 1 || _api.RecordedIP.Values.First() != _api.CurrentIP ) {
                            LogSingleton.I.Info(
                                999,
                                String.Format( "Updating {0} with the current IP address of {1}.", regPack["domain"], _api.CurrentIP )
                                );
                            LogSingleton.I.Debug( "Executing UpdateRecordAsync()" );
                            await _api.UpdateRecordAsync( _cts.Token );
                        }

                        LogSingleton.I.Debug( "Executing LogoutAsync()" );
                        await _api.LogoutAsync( _cts.Token );
                    }
                    _api = null;
                } catch ( QueryAPIException err ) {
                    string text = String.Format( "Error {0}: {1}", err.Code.ToString(), err.RMessage );
                    if ( err.Details != null ) text += "\n\n" + err.Details;
                    if ( err.Url != null ) text += "\n\n" + err.Url;
                    LogSingleton.I.Error( err.Code, text );
                    sleep /= 4;
                    LogSingleton.I.Debug( String.Format( "Sleep set to {0} milliseconds.", sleep ) );
                } catch ( OperationCanceledException ) {
                    LogSingleton.I.Debug( "Caught OperationCanceledException" );
                    return;
                }

                // Give OnStop a chance to Abort thread before logging the below statement.
                try { await Task.Delay( 1000, _cts.Token ); } catch ( OperationCanceledException ) { return; }

                LogSingleton.I.Debug( String.Format( "Sleeping {0} milliseconds.", sleep ) );
                try { await Task.Delay( sleep, _cts.Token ); } catch ( OperationCanceledException ) { return; }
            }
        }
    }
}
