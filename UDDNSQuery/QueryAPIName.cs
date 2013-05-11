using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace UDDNSQuery {
    class QueryAPIName : QueryAPI, IQueryAPI {
        private static readonly string m_sUrlPrefix = "https://api.name.com";
        private static readonly string m_sUrlGetCurrentIP = m_sUrlPrefix + "/api/hello";
        private static readonly string m_sUrlAuthenticate = m_sUrlPrefix + "/api/login";
        private static readonly string m_sUrlGetPriDomain = m_sUrlPrefix + "/api/domain/list";
        private static readonly string m_sUrlGetRecordsPrefix = m_sUrlPrefix + "/api/dns/list/";
        private static readonly string m_sUrlDeleteRecordPrefix = m_sUrlPrefix + "/api/dns/delete/";
        private static readonly string m_sUrlCreateRecordPrefix = m_sUrlPrefix + "/api/dns/create/";
        private static readonly string m_sUrlLogout = m_sUrlPrefix + "/api/logout";

        public QueryAPIName() : base() { }

        /// <summary>
        /// Gets the current public IP.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        public async Task GetCurrentIPAsync( CancellationToken oCT ) {
            // Parse JSON.
            JObject oJson = await this.RequestJSONAsync( m_sUrlGetCurrentIP, null, oCT );
            if ( (string) oJson.SelectToken( "result.code" ) == null ||
                (string) oJson.SelectToken( "result.message" ) == null
                ) throw new QueryAPIException( 300 );
            if ( (string) oJson.SelectToken( "result.code" ) != "100" )
                throw new QueryAPIException( 301, (string) oJson.SelectToken( "result.message" ) );
            // Get pblic IP.
            if ( (string) oJson.SelectToken( "client_ip" ) == null ) throw new QueryAPIException( 302 );
            string sClient_ip = (string) oJson.SelectToken( "client_ip" );
            if ( !Regex.Match( sClient_ip, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$" ).Success ) {
                throw new QueryAPIException( 303 );
            }
            this.m_sCurrentIP = sClient_ip;
        }

        /// <summary>
        /// Authenticates to the API.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        public async Task AuthenticateAsync( CancellationToken oCT ) {
            // Decrypt API token and setup API query for session token.
            byte[] baData = Encoding.ASCII.GetBytes( String.Format( "{{\"username\":\"{0}\",\"api_token\":\"{1}\"}}",
                this.m_sUserName, Encoding.ASCII.GetString( ProtectedData.Unprotect( Convert.FromBase64String(
                this.m_sApiTokenEncrypted ), null, DataProtectionScope.LocalMachine ) ) ) );
            // Parse JSON.
            JObject oJson = await this.RequestJSONAsync( m_sUrlAuthenticate, baData, oCT );
            baData = null; // Remove clear-text API token from memory.
            if ( (string) oJson.SelectToken( "result.code" ) == null ||
                (string) oJson.SelectToken( "result.message" ) == null
                ) throw new QueryAPIException( 400 );
            if ( (string) oJson.SelectToken( "result.code" ) != "100" )
                throw new QueryAPIException( 401, (string) oJson.SelectToken( "result.message" ) );
            // Set session token.
            if ( (string) oJson.SelectToken( "session_token" ) == null ) throw new QueryAPIException( 402 );
            string sSession_token = (string) oJson.SelectToken( "session_token" );
            if ( !Regex.Match( sSession_token, @"^([A-Fa-f0-9]){10,46}$" ).Success ) {
                throw new QueryAPIException( 403 );
            }
            this.m_sSessionToken = sSession_token;
        }

        public async Task ValidateDomainAsync( CancellationToken oCT ) { } //TODO

        /// <summary>
        /// Gets all records related to the domain.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        public async Task GetRecordsAsync( CancellationToken oCT ) {
            // Parse JSON.
            JObject oJson = await this.RequestJSONAsync( m_sUrlGetRecordsPrefix + this.m_sPriDomain, null, oCT );
            if ( (string) oJson.SelectToken( "result.code" ) == null ||
                (string) oJson.SelectToken( "result.message" ) == null
                ) throw new QueryAPIException( 600 );
            if ( (string) oJson.SelectToken( "result.code" ) != "100" )
                throw new QueryAPIException( 601, (string) oJson.SelectToken( "result.message" ) );
            // Get dictionary of records associated with the requested FQDN.
            IDictionary<string, string> dRecords = oJson.SelectToken( "records" )
                .Where( o => (string) o.SelectToken( "name" ) == this.m_sDomain )
                .ToDictionary( s => (string) s.SelectToken( "record_id" ), s => (string) s.SelectToken( "content" ) );
            this.m_dRecordedIP = dRecords;
        }

        /// <summary>
        /// Removes all records for the domain and adds an A record with the current public IP address.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        public async Task UpdateDNSRecordAsync( CancellationToken oCT ) {
            /*// Create record.
            JObject oJson;
            byte[] baData;
            string sSubDomain = this.m_sDomain.Substring( 0, this.m_sDomain.Length - this.m_sPriDomain.Length - 1 );
            string s = "{\"hostname\":\"{0}\",\"type\":\"A\",\"content\":\"{1}\",\"ttl\":\"300\",\"priority\":\"10\"}";
            baData = Encoding.ASCII.GetBytes( String.Format( s, sSubDomain, this.m_sCurrentIP ) );
            oJson = this.RequestJSON( m_sUrlCreateRecordPrefix + this.m_sPriDomain, baData );
            if ( (string) oJson.SelectToken( "result.code" ) == null ||
                (string) oJson.SelectToken( "result.message" ) == null
                ) throw new QueryAPIException( 702 );
            if ( (string) oJson.SelectToken( "result.code" ) != "100" )
                throw new QueryAPIException( 703, (string) oJson.SelectToken( "result.message" ) );
            // Delete all previous records for this FQDN.
            foreach ( string sRecord_id in this.m_dRecordedIP.Keys ) {
                baData = Encoding.ASCII.GetBytes( String.Format( "{\"record_id\":\"{0}\"}", sRecord_id ) );
                oJson = this.RequestJSON( m_sUrlDeleteRecordPrefix + this.m_sPriDomain, baData );
                if ( (string) oJson.SelectToken( "result.code" ) == null ||
                (string) oJson.SelectToken( "result.message" ) == null
                ) throw new QueryAPIException( 700 );
                if ( (string) oJson.SelectToken( "result.code" ) != "100" )
                    throw new QueryAPIException( 701, (string) oJson.SelectToken( "result.message" ) );
            } */
        }

        /// <summary>
        /// De-authenticate from the API.
        /// </summary>
        /// <exception cref="OperationCancelledException" />
        public async Task LogoutAsync( CancellationToken oCT ) {
            await this.RequestJSONAsync( m_sUrlLogout, null, oCT );
            this.m_sSessionToken = null;
        }
    }
}
