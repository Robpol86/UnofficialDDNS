/**
 * Copyright (c) 2013, Robpol86
 * This software is made available under the terms of the MIT License that can
 * be found in the LICENSE.txt file.
 */

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UnofficialDDNS {
    class QueryAPINameDotCom : QueryAPI, IQueryAPI {
        private static readonly string m_sUrlPrefix = "https://api.name.com";
        private static readonly string m_sUrlGetCurrentIP = m_sUrlPrefix + "/api/hello";
        private static readonly string m_sUrlAuthenticate = m_sUrlPrefix + "/api/login";
        private static readonly string m_sUrlGetPriDomain = m_sUrlPrefix + "/api/domain/list";
        private static readonly string m_sUrlGetRecordsPrefix = m_sUrlPrefix + "/api/dns/list/";
        private static readonly string m_sUrlDeleteRecordPrefix = m_sUrlPrefix + "/api/dns/delete/";
        private static readonly string m_sUrlCreateRecordPrefix = m_sUrlPrefix + "/api/dns/create/";
        private static readonly string m_sUrlLogout = m_sUrlPrefix + "/api/logout";

        public QueryAPINameDotCom( string sDomain ) : base( sDomain ) { }

        /// <summary>
        /// Gets the current public IP.
        /// </summary>
        /// <exception cref="QueryException" />
        public void GetCurrentIP() {
            // Parse JSON.
            JObject oJson = this.RequestJSON( m_sUrlGetCurrentIP, null );
            if ( (string) oJson.SelectToken( "result.code" ) == null || 
                (string) oJson.SelectToken( "result.message" ) == null
                ) throw new QueryException( 10400, null );
            if ( (string) oJson.SelectToken( "result.code" ) != "100" )
                throw new QueryException( 10401, (string) oJson.SelectToken( "result.message" ) );
            // Get pblic IP.
            if ( (string) oJson.SelectToken( "client_ip" ) == null ) throw new QueryException( 10402, null );
            string sClient_ip = (string) oJson.SelectToken( "client_ip" );
            if ( !Regex.Match( sClient_ip, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$" ).Success ) {
                throw new QueryException( 10403, null );
            }
            this.m_sCurrentIP = sClient_ip;
        }

        /// <summary>
        /// Authenticates to the API.
        /// </summary>
        /// <param name="sUserName">Name.com user.</param>
        /// <param name="baApiToken">API token emailed to the domain administrator.</param>
        /// <exception cref="QueryException" />
        public void Authenticate( string sUserName, byte[] baApiToken ) {
            // Setup API query for session token.
            string sApiToken = Encoding.ASCII.GetString(
                ProtectedData.Unprotect( baApiToken, null, DataProtectionScope.LocalMachine )
                );
            string sPostData = String.Format( "{{\"username\":\"{0}\",\"api_token\":\"{1}\"}}", sUserName, sApiToken );
            byte[] baData = Encoding.ASCII.GetBytes( sPostData );
            sApiToken = sPostData = null; // Remove clear-text apitoken from memory.
            // Parse JSON.
            JObject oJson = this.RequestJSON( m_sUrlAuthenticate, baData );
            baData = null; // Remove clear-text apitoken from memory.
            if ( (string) oJson.SelectToken( "result.code" ) == null ||
                (string) oJson.SelectToken( "result.message" ) == null
                ) throw new QueryException( 10410, null );
            if ( (string) oJson.SelectToken( "result.code" ) != "100" )
                throw new QueryException( 10411, (string) oJson.SelectToken( "result.message" ) );
            // Set session token.
            if ( (string) oJson.SelectToken( "session_token" ) == null ) throw new QueryException( 10412, null );
            string sSession_token = (string) oJson.SelectToken( "session_token" );
            if ( !Regex.Match( sSession_token, @"^([A-Fa-f0-9]){10,46}$" ).Success ) {
                throw new QueryException( 10413, null );
            }
            this.m_sSessionToken = sSession_token;
        }

        /// <summary>
        /// Gets the primary domain (e.g. mydomain.com from office.mydomain.com).
        /// </summary>
        /// <exception cref="QueryException" />
        public void GetPriDomain() {
            // Parse JSON.
            JObject oJson = this.RequestJSON( m_sUrlGetPriDomain, null );
            if ( (string) oJson.SelectToken( "result.code" ) == null ||
                (string) oJson.SelectToken( "result.message" ) == null
                ) throw new QueryException( 10420, null );
            if ( (string) oJson.SelectToken( "result.code" ) != "100" )
                throw new QueryException( 10421, (string) oJson.SelectToken( "result.message" ) );
            // Get list of domains associated with authenticated account.
            IList<string> lDomains = oJson.SelectToken( "domains" ).Select( p => ((JProperty) p).Name ).ToList();
            if ( lDomains.Count == 0 ) throw new QueryException( 10422, null );
            if ( lDomains.Contains(this.m_sDomain) ) {
                this.m_sPriDomain = this.m_sDomain;
                return;
            }
            // Determine primary domain.
            string[] saDomainSplit = this.m_sDomain.Split( '.' );
            string sPriDomain;
            for ( int i = 2; i < saDomainSplit.Count(); i++ ) {
                sPriDomain = String.Join( ".", saDomainSplit.Skip( saDomainSplit.Count() - i ) );
                if ( lDomains.Contains( sPriDomain ) ) {
                    this.m_sPriDomain = sPriDomain;
                    return;
                }
            }
            throw new QueryException( 10423, null );
        }

        /// <summary>
        /// Gets all records related to the domain.
        /// </summary>
        /// <exception cref="QueryException" />
        public void GetRecords() {
            // Parse JSON.
            JObject oJson = this.RequestJSON( m_sUrlGetRecordsPrefix + this.m_sPriDomain, null );
            if ( (string) oJson.SelectToken( "result.code" ) == null ||
                (string) oJson.SelectToken( "result.message" ) == null
                ) throw new QueryException( 10430, null );
            if ( (string) oJson.SelectToken( "result.code" ) != "100" )
                throw new QueryException( 10431, (string) oJson.SelectToken( "result.message" ) );
            // Get dictionary of records associated with the requested FQDN.
            IDictionary<string, string> dRecords = oJson.SelectToken( "records" )
                .Where( o => (string) o.SelectToken( "name" ) == this.m_sDomain )
                .ToDictionary( s => (string) s.SelectToken( "record_id" ), s => (string) s.SelectToken( "content" ) );
            this.m_dRecordedIP = dRecords;
        }

        /// <summary>
        /// Removes all records for the domain and adds an A record with the current public IP address.
        /// </summary>
        /// <exception cref="QueryException" />
        public void UpdateDNSRecord() {
            // Create record.
            JObject oJson;
            byte[] baData;
            string sSubDomain = this.m_sDomain.Substring( 0, this.m_sDomain.Length - this.m_sPriDomain.Length - 1 );
            string s = "{\"hostname\":\"{0}\",\"type\":\"A\",\"content\":\"{1}\",\"ttl\":\"300\",\"priority\":\"10\"}";
            baData = Encoding.ASCII.GetBytes( String.Format( s, sSubDomain, this.m_sCurrentIP ) );
            oJson = this.RequestJSON( m_sUrlCreateRecordPrefix + this.m_sPriDomain, baData );
            if ( (string) oJson.SelectToken( "result.code" ) == null ||
                (string) oJson.SelectToken( "result.message" ) == null
                ) throw new QueryException( 10442, null );
            if ( (string) oJson.SelectToken( "result.code" ) != "100" )
                throw new QueryException( 10443, (string) oJson.SelectToken( "result.message" ) );
            // Delete all previous records for this FQDN.
            foreach ( string sRecord_id in this.m_dRecordedIP.Keys ) {
                baData = Encoding.ASCII.GetBytes( String.Format( "{\"record_id\":\"{0}\"}", sRecord_id ) );
                oJson = this.RequestJSON( m_sUrlDeleteRecordPrefix + this.m_sPriDomain, baData );
                if ( (string) oJson.SelectToken( "result.code" ) == null ||
                (string) oJson.SelectToken( "result.message" ) == null
                ) throw new QueryException( 10440, null );
                if ( (string) oJson.SelectToken( "result.code" ) != "100" )
                    throw new QueryException( 10441, (string) oJson.SelectToken( "result.message" ) );
            }
            // Add Event Log entry.
            LogSingleton.Instance.Log( 
                10444, 
                String.Format( "Domain: {0}; IP: {1}", this.m_sDomain, this.m_sCurrentIP ), 
                System.Diagnostics.EventLogEntryType.Information 
                );
        }

        /// <summary>
        /// De-authenticate from the API.
        /// </summary>
        public void Logout() {
            this.RequestJSON( m_sUrlLogout, null );
            this.m_sSessionToken = null;
        }
    }
}
