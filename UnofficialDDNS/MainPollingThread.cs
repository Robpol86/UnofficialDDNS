/**
 * Copyright (c) 2013, Robpol86
 * This software is made available under the terms of the MIT License that can
 * be found in the LICENSE.txt file.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace UnofficialDDNS {
    class MainPollingThread {
        private RegistryRead m_oConfig;

        public MainPollingThread( RegistryRead oConfig ) {
            this.m_oConfig = oConfig;
        }

        public void TheThread() {
            int iSleep = 0; // 0 = first iteration
            IQueryAPI oApi;
            string sRecordedIP = null;
            
            while ( true ) {
                if ( iSleep != 0 ) Thread.Sleep( iSleep ); // Don't sleep on first iteration.
                iSleep = this.m_oConfig.Interval * 60 * 1000;

                try {
                    // Instantiate the API class.
                    switch ( this.m_oConfig.Registrar ) {
                        case "namedotcom":
                            oApi = new QueryAPINameDotCom( m_oConfig.Domain ); break;
                        default:
                            oApi = null; break; // Not meant to get here, force an NPE if hell freezes over.
                    }

                    // Check if IP has changed if this is not the first iteration.
                    oApi.GetCurrentIP(); // Get current public IP address of this machine.
                    if ( sRecordedIP != null && oApi.CurrentIP == sRecordedIP ) continue;

                    // Get DNS records.
                    oApi.Authenticate( m_oConfig.UserName, m_oConfig.ApiToken ); // Login to API.
                    oApi.GetPriDomain(); // Get primary domain incase user is using a subdomain for DDNS.
                    oApi.GetRecords(); // Get any records assigned to the DDNS domain.

                    // Check if IP has changed or if DNS needs updating.
                    if ( oApi.RecordedIP.Count == 1 && oApi.RecordedIP.Values.First() == oApi.CurrentIP ) {
                        // There is only one record on this domain and it matches the public IP. Nothing to do.
                        oApi.Logout();
                        sRecordedIP = oApi.RecordedIP.Values.First();
                        continue;
                    }

                    // IP has changed, or there are more than one records for this domain.
                    oApi.UpdateDNSRecord();
                    oApi.Logout();
                    // Next iteration will verify with sRecordedIP.
                } catch ( QueryException ) {
                    // The exception already handles any logging.
                    iSleep = this.m_oConfig.IntervalError * 60 * 1000;
                }
            }
        }
    }
}
