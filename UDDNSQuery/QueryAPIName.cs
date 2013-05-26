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
    class QueryAPIName : QueryAPI {
        private static readonly string _urlPrefix = "https://api.name.com";
        private static readonly string _urlGetCurrentIP = _urlPrefix + "/api/hello";
        private static readonly string _urlAuthenticate = _urlPrefix + "/api/login";
        private static readonly string _urlGetMainDomain = _urlPrefix + "/api/domain/list";
        private static readonly string _urlGetRecordsPrefix = _urlPrefix + "/api/dns/list/";
        private static readonly string _urlDeleteRecordPrefix = _urlPrefix + "/api/dns/delete/";
        private static readonly string _urlCreateRecordPrefix = _urlPrefix + "/api/dns/create/";
        private static readonly string _urlLogout = _urlPrefix + "/api/logout";

        public QueryAPIName() : base() { }

        public override async Task<JObject> RequestJSONAsync( string url, byte[] postData, CancellationToken ct ) {
            JObject json = await base.RequestJSONAsync( url, postData, ct );
            try {
                int apiCode = (int) json["result"]["code"];
                string apiMessage = (string) json["result"]["message"];
                if ( apiCode != 100 ) {
                    throw new QueryAPIException( 203, String.Format( "URL: {0}\nCode {1}: {2}", url, apiCode, apiMessage ) );
                }
            } catch ( Exception e ) {
                if ( e is NullReferenceException || e is ArgumentNullException || e is FormatException ) {
                    throw new QueryAPIException( 204, String.Format( "URL: {0}\nJSON: {1}", url, json.ToString() ) );
                }
                throw;
            }
            return json;
        }

        public override async Task GetCurrentIPAsync( CancellationToken ct ) {
            JObject json = await RequestJSONAsync( _urlGetCurrentIP, null, ct );
            // Get public IP.
            if ( (string) json.SelectToken( "client_ip" ) == null ) throw new QueryAPIException( 302 );
            string client_ip = (string) json.SelectToken( "client_ip" );
            if ( !Regex.Match( client_ip, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$" ).Success ) {
                throw new QueryAPIException( 303 );
            }
            _currentIP = client_ip;
        }

        public override async Task AuthenticateAsync( CancellationToken ct ) {
            // Decrypt API token and setup API query for session token.
            byte[] data = Encoding.ASCII.GetBytes( String.Format( "{{\"username\":\"{0}\",\"api_token\":\"{1}\"}}",
                _userName, Encoding.ASCII.GetString( ProtectedData.Unprotect( Convert.FromBase64String(
                _apiTokenEncrypted ), null, DataProtectionScope.LocalMachine ) ) ) );
            JObject json = await RequestJSONAsync( _urlAuthenticate, data, ct );
            data = null; // Remove clear-text API token from memory.
            if ( (string) json.SelectToken( "session_token" ) == null ) throw new QueryAPIException( 402 );
            string session_token = (string) json.SelectToken( "session_token" );
            if ( !Regex.Match( session_token, @"^([A-Fa-f0-9]){10,46}$" ).Success ) {
                throw new QueryAPIException( 403 );
            }
            _sessionToken = session_token;
        }

        public override async Task ValidateDomainAsync( CancellationToken ct ) {
            JObject json = await RequestJSONAsync( _urlGetMainDomain, null, ct );
            IList<string> domains = json.SelectToken( "domains" ).Select( p => ((JProperty) p).Name ).ToList();
            if ( domains.Count == 0 ) throw new QueryAPIException( 502 );
            if ( domains.Contains( _domain ) ) {
                _mainDomain = _domain;
                return;
            }
            // Determine primary domain.
            string[] domainSplit = _domain.Split( '.' );
            string mainDomain;
            for ( int i = 2; i < domainSplit.Count(); i++ ) {
                mainDomain = String.Join( ".", domainSplit.Skip( domainSplit.Count() - i ) );
                if ( domains.Contains( mainDomain ) ) {
                    _mainDomain = mainDomain;
                    return;
                }
            }
            throw new QueryAPIException( 503 );
        }
        
        public override async Task GetRecordsAsync( CancellationToken ct ) {
            JObject json = await RequestJSONAsync( _urlGetRecordsPrefix + _mainDomain, null, ct );
            try {
                _recordedIP = json["records"]
                    .Where( s => (string) s["name"] == _domain )
                    .Where( s => (string) s["type"] == "A" )
                    .ToDictionary( i => (string) i["record_id"], s => (string) s["content"] );
                if ( _recordedIP.Count == 0 ) throw new ArgumentNullException();
            } catch ( Exception e ) {
                if ( e is NullReferenceException || e is ArgumentNullException || e is ArgumentException ) {
                    throw new QueryAPIException( 600 );
                }
                throw;
            }
        }

        /// <summary>
        /// Removes all records for the domain and adds an A record with the current public IP address.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        public override async Task UpdateRecordAsync( CancellationToken ct ) {
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
        public override async Task LogoutAsync( CancellationToken ct ) {
            await RequestJSONAsync( _urlLogout, null, ct );
            _sessionToken = null;
        }
    }
}
