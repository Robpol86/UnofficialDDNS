/**
 * Copyright (c) 2013, Robpol86
 * This software is made available under the terms of the MIT License that can
 * be found in the LICENSE.txt file.
 */

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace UDDNSQuery {
    class QueryAPIName : QueryAPI {
        private static readonly string _uriGetCurrentIP = "/api/hello";
        private static readonly string _uriAuthenticate = "/api/login";
        private static readonly string _uriGetMainDomain = "/api/domain/list";
        private static readonly string _uriGetRecordsPrefix = "/api/dns/list/";
        private static readonly string _uriDeleteRecordPrefix = "/api/dns/delete/";
        private static readonly string _uriCreateRecordPrefix = "/api/dns/create/";
        private static readonly string _uriLogout = "/api/logout";

        public QueryAPIName() : base( new Uri( "https://api.name.com" ) ) { }

        public override async Task<JObject> RequestJSONAsync( string uriPath, StringContent postData, CancellationToken ct ) {
            string url = new Uri( _baseUri, uriPath ).AbsoluteUri;
            JObject json = await base.RequestJSONAsync( uriPath, postData, ct );
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
            JObject json = await RequestJSONAsync( _uriGetCurrentIP, null, ct );
            if ( (string) json.SelectToken( "client_ip" ) == null ) throw new QueryAPIException( 302 );
            string client_ip = (string) json.SelectToken( "client_ip" );
            if ( !Regex.Match( client_ip, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$" ).Success ) {
                throw new QueryAPIException( 303 );
            }
            _currentIP = client_ip;
        }

        public override async Task AuthenticateAsync( CancellationToken ct ) {
            // Decrypt API token and setup API query for session token.
            StringContent data = new StringContent( String.Format( "{{\"username\":\"{0}\",\"api_token\":\"{1}\"}}",
                _userName, Encoding.ASCII.GetString( ProtectedData.Unprotect( Convert.FromBase64String(
                _apiTokenEncrypted ), null, DataProtectionScope.LocalMachine ) ) ), System.Text.Encoding.UTF8,
                "application/x-www-form-urlencoded" );
            JObject json = await RequestJSONAsync( _uriAuthenticate, data, ct );
            data = null; // Remove clear-text API token from memory.
            if ( (string) json.SelectToken( "session_token" ) == null ) throw new QueryAPIException( 402 );
            string session_token = (string) json.SelectToken( "session_token" );
            if ( !Regex.Match( session_token, @"^([A-Fa-f0-9]){10,46}$" ).Success ) {
                throw new QueryAPIException( 403 );
            }
            _sessionToken = session_token;
        }

        public override async Task ValidateDomainAsync( CancellationToken ct ) {
            JObject json = await RequestJSONAsync( _uriGetMainDomain, null, ct );
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
            JObject json = await RequestJSONAsync( _uriGetRecordsPrefix + _mainDomain, null, ct );
            try {
                _recordedIP = json["records"]
                    .Where( s => (string) s["name"] == _domain )
                    .Where( s => (new string[] { "A", "CNAME" }).Contains( (string) s["type"] ) )
                    .ToDictionary( i => (string) i["record_id"], s => (string) s["content"] );
            } catch ( Exception e ) {
                if ( e is NullReferenceException || e is ArgumentNullException || e is ArgumentException ) {
                    //throw new QueryAPIException( 600 );
                }
                throw;
            }
        }

        /// <summary>
        /// Creates an A record with the current IP if one does not exist, then deletes all non-matching A records.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        public override async Task UpdateRecordAsync( CancellationToken ct ) {
            if ( !_recordedIP.Values.Contains( _currentIP ) ) {
                StringContent data = new StringContent( String.Format(
                    "{{\"hostname\":\"{0}\",\"type\":\"A\",\"content\":\"{1}\",\"ttl\":\"300\",\"priority\":\"10\"}}",
                    _domain.Replace( _mainDomain, "" ).Trim( '.' ), _currentIP ), System.Text.Encoding.UTF8,
                    "application/x-www-form-urlencoded" );
                JObject json = await RequestJSONAsync( _uriCreateRecordPrefix + _mainDomain, data, ct );
            }
            foreach ( string id in _recordedIP.Where( s => (string) s.Value != _currentIP ).Select( s => s.Key ).ToList<string>() ) {
                StringContent data = new StringContent( String.Format( "{{\"record_id\":\"{0}\"}}", id ),
                    System.Text.Encoding.UTF8, "application/x-www-form-urlencoded" );
                JObject json = await RequestJSONAsync( _uriDeleteRecordPrefix + _mainDomain, data, ct );
            }
        }

        /// <summary>
        /// De-authenticate from the API.
        /// </summary>
        public override async Task LogoutAsync( CancellationToken ct ) {
            await RequestJSONAsync( _uriLogout, null, ct );
            _sessionToken = null;
        }
    }
}
