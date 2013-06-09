/**
 * Copyright (c) 2013, Robpol86
 * This software is made available under the terms of the MIT License that can
 * be found in the LICENSE.txt file.
 */

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using UDDNSQuery;

namespace UnofficialDDNS {
    // TODO
    // remove as many using statements everywhere.
    // test wrong url scenarios.
    // enable Optimized code.
    // copyright statement all files.
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
            // Read data from registry..
            string regBase = @"HKEY_LOCAL_MACHINE\SOFTWARE\UnofficialDDNS";
            IDictionary<string, string> regPack = new Dictionary<string, string>();
            regPack.Add( "registrar", (string) Registry.GetValue( regBase, "Registrar", "" ) );
            regPack.Add( "userName", (string) Registry.GetValue( regBase, "Username", "" ) );
            regPack.Add( "apiToken", (string) Registry.GetValue( regBase, "ApiToken", "" ) );
            regPack.Add( "domain", (string) Registry.GetValue( regBase, "Domain", "" ) );
            regPack.Add( "interval", ((int) Registry.GetValue( regBase, "Interval", "" )).ToString() );
            regPack.Add( "debug", ((int) Registry.GetValue( regBase, "Debug", "0" )).ToString() );

            // Start main thread.
            if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, "Initializing polling thread.", EventLogEntryType.Information );
            _cts = new CancellationTokenSource();
            _pollingThread = new Thread( (ThreadStart) delegate { PollingThreadWorker( _cts, regPack ); } );
            _pollingThread.Start();
            if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, "Polling thread started.", EventLogEntryType.Information );
        }

        protected override void OnStop() {
            _cts.Cancel();
            Thread.Sleep( 1000 );
            _pollingThread.Abort();
        }

        private async static void PollingThreadWorker( CancellationTokenSource cts, IDictionary<string, string> regPack ) {
            while ( true ) {
                int sleep = Convert.ToInt32( regPack["interval"] ) * 60 * 1000;
                if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, String.Format( "Sleep set to {0} seconds.", sleep ), EventLogEntryType.Information );

                try {
                    if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, "Initializing QueryAPI object.", EventLogEntryType.Information );
                    using ( IQueryAPI api = QueryAPIIndex.Instance.Factory( regPack["registrar"] ) ) {
                        // Pass credentials to class instance.
                        if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, "Setting registrar credentials to object instance.", EventLogEntryType.Information );
                        api.Credentials( regPack["userName"], regPack["apiToken"].Replace( "ENCRYPTED:", "" ),
                            regPack["domain"]
                            );

                        // Basic checks.
                        if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, "Checking for zero-length strings.", EventLogEntryType.Information );
                        if ( api.UserLength == 0 ) throw new QueryAPIException( 100 );
                        if ( api.TokenLength == 0 ) throw new QueryAPIException( 101 );
                        if ( api.DomainLength == 0 ) throw new QueryAPIException( 102 );

                        // Read only.
                        if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, "Executing GetCurrentIPAsync()", EventLogEntryType.Information );
                        await api.GetCurrentIPAsync( cts.Token );
                        if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, "Executing AuthenticateAsync()", EventLogEntryType.Information );
                        await api.AuthenticateAsync( cts.Token );
                        if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, "Executing ValidateDomainAsync()", EventLogEntryType.Information );
                        await api.ValidateDomainAsync( cts.Token );
                        if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, "Executing GetRecordsAsync()", EventLogEntryType.Information );
                        await api.GetRecordsAsync( cts.Token );

                        // Check if DNS is outdated.
                        if ( api.RecordedIP.Count != 1 || api.RecordedIP.Values.First() != api.CurrentIP ) {
                            // DNS is outdated, will update DNS record.

                        }

                        if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, "Executing LogoutAsync()", EventLogEntryType.Information );
                        await api.LogoutAsync( cts.Token );

                        //TODO
                    }
                } catch ( QueryAPIException err ) {
                    string text = String.Format( "Error {0}: {1}", err.Code.ToString(), err.RMessage );
                    if ( err.Details != null ) text += "\n\n" + err.Details;
                    if ( err.Url != null ) text += "\n\n" + err.Url;
                    LogSingleton.Instance.Log( err.Code, text );
                    sleep /= 4;
                    if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, String.Format( "Sleep set to {0} seconds.", sleep ), EventLogEntryType.Information );
                } catch ( OperationCanceledException ) {
                    // Service is stopping.
                    if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, "Caught OperationCanceledException", EventLogEntryType.Information );
                    break;
                }

                if ( regPack["debug"] == "1" ) LogSingleton.Instance.Log( 999, String.Format( "Sleeping {0} seconds.", sleep ), EventLogEntryType.Information );
                Thread.Sleep( sleep );
            }
        }
    }
}
