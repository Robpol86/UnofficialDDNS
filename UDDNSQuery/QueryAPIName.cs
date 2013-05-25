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
        private static readonly string _urlPrefix = "https://api.name.com";
        private static readonly string _urlGetCurrentIP = _urlPrefix + "/api/hello";
        private static readonly string _urlAuthenticate = _urlPrefix + "/api/login";
        private static readonly string _urlGetPriDomain = _urlPrefix + "/api/domain/list";
        private static readonly string _urlGetRecordsPrefix = _urlPrefix + "/api/dns/list/";
        private static readonly string _urlDeleteRecordPrefix = _urlPrefix + "/api/dns/delete/";
        private static readonly string _urlCreateRecordPrefix = _urlPrefix + "/api/dns/create/";
        private static readonly string _urlLogout = _urlPrefix + "/api/logout";

        public QueryAPIName() : base() { }

        /// <summary>
        /// Gets the current public IP.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        public async Task GetCurrentIPAsync( CancellationToken ct ) {
            // Parse JSON.
            JObject json = await RequestJSONAsync( _urlGetCurrentIP, null, ct );
            if ( (string) json.SelectToken( "result.code" ) == null ||
                (string) json.SelectToken( "result.message" ) == null
                ) throw new QueryAPIException( 300 );
            if ( (string) json.SelectToken( "result.code" ) != "100" )
                throw new QueryAPIException( 301, (string) json.SelectToken( "result.message" ) );
            // Get pblic IP.
            if ( (string) json.SelectToken( "client_ip" ) == null ) throw new QueryAPIException( 302 );
            string client_ip = (string) json.SelectToken( "client_ip" );
            if ( !Regex.Match( client_ip, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$" ).Success ) {
                throw new QueryAPIException( 303 );
            }
            _currentIP = client_ip;
        }

        /// <summary>
        /// Authenticates to the API.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        public async Task AuthenticateAsync( CancellationToken ct ) {
            // Decrypt API token and setup API query for session token.
            byte[] data = Encoding.ASCII.GetBytes( String.Format( "{{\"username\":\"{0}\",\"api_token\":\"{1}\"}}",
                _userName, Encoding.ASCII.GetString( ProtectedData.Unprotect( Convert.FromBase64String(
                _apiTokenEncrypted ), null, DataProtectionScope.LocalMachine ) ) ) );
            // Parse JSON.
            JObject json = await RequestJSONAsync( _urlAuthenticate, data, ct );
            data = null; // Remove clear-text API token from memory.
            if ( (string) json.SelectToken( "result.code" ) == null ||
                (string) json.SelectToken( "result.message" ) == null
                ) throw new QueryAPIException( 400 );
            if ( (string) json.SelectToken( "result.code" ) != "100" )
                throw new QueryAPIException( 401, (string) json.SelectToken( "result.message" ) );
            // Set session token.
            if ( (string) json.SelectToken( "session_token" ) == null ) throw new QueryAPIException( 402 );
            string session_token = (string) json.SelectToken( "session_token" );
            if ( !Regex.Match( session_token, @"^([A-Fa-f0-9]){10,46}$" ).Success ) {
                throw new QueryAPIException( 403 );
            }
            _sessionToken = session_token;
        }

        public async Task ValidateDomainAsync( CancellationToken ct ) { } //TODO

        /// <summary>
        /// Gets all records related to the domain.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        public async Task GetRecordsAsync( CancellationToken ct ) {
            // Parse JSON.
            JObject json = await RequestJSONAsync( _urlGetRecordsPrefix + _priDomain, null, ct );
            if ( (string) json.SelectToken( "result.code" ) == null ||
                (string) json.SelectToken( "result.message" ) == null
                ) throw new QueryAPIException( 600 );
            if ( (string) json.SelectToken( "result.code" ) != "100" )
                throw new QueryAPIException( 601, (string) json.SelectToken( "result.message" ) );
            // Get dictionary of records associated with the requested FQDN.
            IDictionary<string, string> records = json.SelectToken( "records" )
                .Where( o => (string) o.SelectToken( "name" ) == _domain )
                .ToDictionary( s => (string) s.SelectToken( "record_id" ), s => (string) s.SelectToken( "content" ) );
            _recordedIP = records;
        }

        /// <summary>
        /// Removes all records for the domain and adds an A record with the current public IP address.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        public async Task UpdateDNSRecordAsync( CancellationToken ct ) {
            /*// Create record.
            JObject oJson;
            byte[] baData;
            string sSubDomain = m_sDomain.Substring( 0, m_sDomain.Length - m_sPriDomain.Length - 1 );
            string s = "{\"hostname\":\"{0}\",\"type\":\"A\",\"content\":\"{1}\",\"ttl\":\"300\",\"priority\":\"10\"}";
            baData = Encoding.ASCII.GetBytes( String.Format( s, sSubDomain, m_sCurrentIP ) );
            oJson = RequestJSON( m_sUrlCreateRecordPrefix + m_sPriDomain, baData );
            if ( (string) oJson.SelectToken( "result.code" ) == null ||
                (string) oJson.SelectToken( "result.message" ) == null
                ) throw new QueryAPIException( 702 );
            if ( (string) oJson.SelectToken( "result.code" ) != "100" )
                throw new QueryAPIException( 703, (string) oJson.SelectToken( "result.message" ) );
            // Delete all previous records for this FQDN.
            foreach ( string sRecord_id in m_dRecordedIP.Keys ) {
                baData = Encoding.ASCII.GetBytes( String.Format( "{\"record_id\":\"{0}\"}", sRecord_id ) );
                oJson = RequestJSON( m_sUrlDeleteRecordPrefix + m_sPriDomain, baData );
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
        public async Task LogoutAsync( CancellationToken ct ) {
            await RequestJSONAsync( _urlLogout, null, ct );
            _sessionToken = null;
        }
    }
}
