/**
 * Copyright (c) 2013, Robpol86
 * This software is made available under the terms of the MIT License that can
 * be found in the LICENSE.txt file.
 */

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UnofficialDDNS {
    interface IQueryAPI {
        string CurrentIP { get; }
        IDictionary<string, string> RecordedIP { get; }

        JObject RequestJSON( string sUrl, byte[] baPostData );
        void GetCurrentIP();
        void Authenticate( string sUserName, byte[] baApiToken );
        void GetPriDomain();
        void GetRecords();
        void UpdateDNSRecord();
        void Logout();
        //TODO
    }

    class QueryAPI {
        protected string m_sDomain = null; // FQDN target.
        protected string m_sPriDomain = null; // Primary portion of the FQDN (e.g. test.co.uk from server.test.co.uk).
        protected string m_sCurrentIP = null; // Current public IP of the local host.
        protected IDictionary<string, string> m_dRecordedIP = null; // IP currently set at registrar. List in case of multiple records.
        protected string m_sSessionToken = null; // Session token to insert in HTTP header.
        
        public QueryAPI( string sDomain ) {
            this.m_sDomain = sDomain;
        }

        public string CurrentIP { get { return this.m_sCurrentIP; } }
        public IDictionary<string, string> RecordedIP { get { return this.m_dRecordedIP; } }

        /// <summary>
        /// Requests the JSON from a URL.
        /// </summary>
        /// <param name="sUrl">The URL to query.</param>
        /// <param name="baPostData">HTTP POST data to send.</param>
        /// <returns>JSON.nt JObject</returns>
        /// <exception cref="QueryException" />
        public JObject RequestJSON( string sUrl, byte[] baPostData ) {
            // Setup HTTP request.
            HttpWebRequest oRequest = (HttpWebRequest) WebRequest.Create( sUrl );
            oRequest.ContentType = "application/x-www-form-urlencoded";
            oRequest.Accept = "application/json";
            if ( this.m_sSessionToken != null ) oRequest.Headers.Add( "Api-Session-Token", this.m_sSessionToken );
            oRequest.Method = baPostData != null ? "POST" : "GET";
            if ( baPostData != null ) {
                oRequest.ContentLength = baPostData.Length;
                Stream oStream = oRequest.GetRequestStream();
                oStream.Write( baPostData, 0, baPostData.Length );
                oStream.Close();
            }
            // Execute request.
            string sSerializedJson;
            try {
                using ( HttpWebResponse oResponse = (HttpWebResponse) oRequest.GetResponse() ) {
                    if ( oResponse.StatusCode != HttpStatusCode.OK ) {
                        string sAdditional = 
                            String.Format( "HTTP {1}; URL: {0}", oResponse.StatusCode.ToString(), sUrl );
                        throw new QueryException( 10300, sAdditional );
                    }
                    using ( StreamReader oStream = new StreamReader( oResponse.GetResponseStream(), true ) ) {
                        sSerializedJson = oStream.ReadToEnd();
                    }
                }
            } catch ( WebException e ) {
                string sAdditional = String.Format( "URL: {0}; WebException: ", sUrl, e.ToString() );
                throw new QueryException( 10301, sAdditional );
            }
            // Parse JSON.
            JObject oJson;
            try {
                oJson = JObject.Parse( sSerializedJson );
            } catch ( Exception e ) {
                string sAdditional = String.Format( "URL: {0}; Exception: ", sUrl, e.ToString() );
                throw new QueryException( 10302, sAdditional );
            }
            return oJson;
        }
    }
}
