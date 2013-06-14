/**
 * Copyright (c) 2013, Robpol86
 * This software is made available under the terms of the MIT License that can
 * be found in the LICENSE.txt file.
 */

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
    // TODO
    // remove as many using statements everywhere.
    // enable Optimized code.
    // copyright statement all files.
    // test invalid url again.
    // fix cancelling with DNS stalling the cancellationtoken.
    // test start and quickly stopping the service.
    // test network timeouts.
    [System.ComponentModel.DesignerCategory( "Code" )]
    public partial class UnofficialDDNS : ServiceBase {
        private CancellationTokenSource _cts;
        private Thread _pollingThread;
        
        public UnofficialDDNS() {
            CanPauseAndContinue = false;
            ServiceName = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>().Product;
            AutoLog = true;
        }

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
            _pollingThread = new Thread( (ThreadStart) delegate { PollingThreadWorker( _cts, regPack ); } );
            _pollingThread.Start();
            LogSingleton.I.Debug( "Polling thread started." );
        }

        protected override void OnStop() {
            _cts.Cancel();
            Task.WaitAll();
            _pollingThread.Abort();
        }

        private async static void PollingThreadWorker( CancellationTokenSource cts, IDictionary<string, string> regPack ) {
            if ( LogSingleton.I.EnableDebug ) Thread.Sleep( 1000 ); // Bring order to the event log.
            while ( !cts.IsCancellationRequested ) {
                int sleep = Convert.ToInt32( regPack["interval"] ) * 60 * 1000;
                LogSingleton.I.Debug( String.Format( "Sleep set to {0} seconds.", sleep ) );

                try {
                    LogSingleton.I.Debug( "Initializing QueryAPI object." );
                    using ( IQueryAPI api = QueryAPIIndex.I.Factory( regPack["registrar"] ) ) {
                        // Pass credentials to class instance.
                        LogSingleton.I.Debug( "Setting registrar credentials to object instance." );
                        api.Credentials( regPack["userName"], regPack["apiToken"].Replace( "ENCRYPTED:", "" ),
                            regPack["domain"]
                            );

                        // Read only.
                        LogSingleton.I.Debug( "Executing GetCurrentIPAsync()" );
                        await api.GetCurrentIPAsync( cts.Token );
                        LogSingleton.I.Debug( "Executing AuthenticateAsync()" );
                        await api.AuthenticateAsync( cts.Token );
                        LogSingleton.I.Debug( "Executing ValidateDomainAsync()" );
                        await api.ValidateDomainAsync( cts.Token );
                        LogSingleton.I.Debug( "Executing GetRecordsAsync()" );
                        await api.GetRecordsAsync( cts.Token );

                        // Check if DNS is outdated.
                        LogSingleton.I.Debug( "Recorded IP(s): " + string.Join( ",", api.RecordedIP.Values ) );
                        if ( api.RecordedIP.Count != 1 || api.RecordedIP.Values.First() != api.CurrentIP ) {
                            LogSingleton.I.Info(
                                999,
                                String.Format( "Updating {0} with the current IP address of {1}.", regPack["domain"], api.CurrentIP )
                                );
                            LogSingleton.I.Debug( "Executing UpdateRecordAsync()" );
                            await api.UpdateRecordAsync( cts.Token );
                        }

                        LogSingleton.I.Debug( "Executing LogoutAsync()" );
                        await api.LogoutAsync( cts.Token );
                    }
                } catch ( QueryAPIException err ) {
                    string text = String.Format( "Error {0}: {1}", err.Code.ToString(), err.RMessage );
                    if ( err.Details != null ) text += "\n\n" + err.Details;
                    if ( err.Url != null ) text += "\n\n" + err.Url;
                    LogSingleton.I.Error( err.Code, text );
                    if ( cts.IsCancellationRequested ) break;
                    sleep /= 4;
                    LogSingleton.I.Debug( String.Format( "Sleep set to {0} seconds.", sleep ) );
                } catch ( OperationCanceledException ) {
                    LogSingleton.I.Debug( "Caught OperationCanceledException" );
                }

                Thread.Sleep( 1000 ); // Gives OnStop a chance to Abort thread before logging the below statement.
                if ( cts.IsCancellationRequested ) break;
                LogSingleton.I.Debug( String.Format( "Sleeping {0} seconds.", sleep ) );
                Thread.Sleep( sleep );
            }
        }
    }
}
