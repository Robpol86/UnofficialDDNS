using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace UDDNSQuery {
    /*
     * TODO:
     *  UpdateDNSRecords should only update A records.
     *  ValidateDomain needs to be done.
     *  Authentication failures have unclear messages.
     *  Entering correct credentials gives error 202. /dns/list, no error.
     *  Have a dialog come up while working (2s timer on the cancel button).
     *      Show screenshot example if more than 1 record/wrong record.
     *  Change RecordedIP to string.
     */

    #region Helper Objects

    /// <summary>
    /// Sinleton which centralizes the collection of supported registrars, the string presented in the installer, and the
    /// object used to actually query the registrar.
    /// </summary>
    public sealed class QueryAPIIndex {
        private IDictionary<string, string> _registrarList = new Dictionary<string, string>();
        private static QueryAPIIndex _instance = null;
        private static readonly object _padlock = new object();

        /// <summary>
        /// Singleton instance.
        /// </summary>
        /// <value>
        /// The instance object.
        /// </value>
        public static QueryAPIIndex Instance { get { lock ( _padlock ) { if ( _instance == null ) _instance = new QueryAPIIndex(); return _instance; } } }
        /// <summary>
        /// Collection of supported registrars.
        /// </summary>
        /// <value>
        /// IDictionary containing all registrars.
        /// </value>
        public IDictionary<string, string> Registrars { get { return _registrarList; } }
        
        private QueryAPIIndex() {
            _registrarList.Add( "Name.com", "http://name.com/reseller" );
        }

        /// <summary>
        /// Class factory for QueryAPI objects for specific registrars.
        /// </summary>
        /// <param name="registrar">The desired registrar.</param>
        /// <returns>Registrar specific instance which inherits QueryAPI.</returns>
        public IQueryAPI Factory( string registrar ) {
            switch ( registrar ) {
                case "Name.com": return new QueryAPIName();
                default: return null;
            }
        }
    }

    /// <summary>
    /// Represents an object that queries a registrar's API.
    /// </summary>
    public interface IQueryAPI : IDisposable {
        /// <summary>
        /// Gets an integer that represents the total number of characters in the username.
        /// </summary>
        int UserLength { get; }
        /// <summary>
        /// Gets an integer that represents the total number of characters in the (not encrypted) API token.
        /// </summary>
        int TokenLength { get; }
        /// <summary>
        /// Gets an integer that represents the total number of characters in the domain.
        /// </summary>
        int DomainLength { get; }
        /// <summary>
        /// The current IP address of this host.
        /// </summary>
        string CurrentIP { get; }
        /// <summary>
        /// The IP address currently set at the registrar.
        /// </summary>
        IDictionary<string, string> RecordedIP { get; }

        /// <summary>
        /// Requests the JSON from a URL.
        /// </summary>
        /// <param name="url">The URL to query.</param>
        /// <param name="postData">HTTP POST data to send.</param>
        /// <returns>JSON.net JObject</returns>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        Task<JObject> RequestJSONAsync( string url, byte[] postData, CancellationToken ct );
        
        /// <summary>
        /// Pass credentials and the target domain to class instance.
        /// </summary>
        /// <param name="userName">API Username.</param>
        /// <param name="apiTokenEncrypted">The encrypted and base64 encoded API token/password.</param>
        /// <param name="domain">The fully qualified domain name target.</param>
        void Credentials( string userName, string apiTokenEncrypted, string domain );

        /// <summary>
        /// Gets the current public IP.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        Task GetCurrentIPAsync( CancellationToken ct );

        /// <summary>
        /// Authenticates to the API.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        Task AuthenticateAsync( CancellationToken ct );

        Task ValidateDomainAsync( CancellationToken ct );
        
        /// <summary>
        /// Gets all records related to the domain.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        Task GetRecordsAsync( CancellationToken ct );

        Task UpdateDNSRecordAsync( CancellationToken ct );

        /// <summary>
        /// De-authenticate from the API.
        /// </summary>
        /// <exception cref="OperationCancelledException" />
        Task LogoutAsync( CancellationToken ct );
    }

    [Serializable]
    class QueryAPIException : Exception {
        protected int _code; // Error code.
        protected string _details; // Additional details about the error (error messages from API for example).
        protected string _resxMessage; // String from Errors.resx.

        public int Code { get { return _code; } }
        public string Details { get { return _details; } }
        public string RMessage { get { return _resxMessage; } }

        public QueryAPIException( int code ) : base( code.ToString() ) {
            _code = code;
            _details = null;
            _resxMessage = Errors.ResourceManager.GetString( "Error" + code );
        }

        public QueryAPIException( int code, string details ) : base( code.ToString() ) {
            _code = code;
            _details = details;
            _resxMessage = Errors.ResourceManager.GetString( "Error" + code );
        }
    }

    #endregion

    class QueryAPI {
        protected string m_sUserName = null;
        protected string m_sApiTokenEncrypted = null;
        protected string m_sDomain = null; // FQDN target.

        protected string m_sCurrentIP = null; // Current public IP of the local host.
        protected string m_sPriDomain = null; // Primary portion of the FQDN (e.g. test.co.uk from server.test.co.uk).
        protected IDictionary<string, string> m_dRecordedIP = null; // IP currently set at registrar. List in case of multiple records.
        protected string m_sSessionToken = null; // Session token to insert in HTTP header.

        public int UserLength { get { return this.m_sUserName.Length; } }
        public int TokenLength { get { return this.m_sApiTokenEncrypted.Length; } }
        public int DomainLength { get { return this.m_sDomain.Length; } }
        public string CurrentIP { get { return this.m_sCurrentIP; } }
        public IDictionary<string, string> RecordedIP { get { return this.m_dRecordedIP; } }

        public QueryAPI() { }

        /// <summary>
        /// Pass credentials and the target domain to class instance.
        /// </summary>
        /// <param name="sUserName">The API Username.</param>
        /// <param name="sApiTokenEncrypted">The encrypted and base64 encoded API token/password.</param>
        /// <param name="sDomain">The fully qualified domain name target.</param>
        public void Credentials( string sUserName, string sApiTokenEncrypted, string sDomain ) {
            this.m_sUserName = sUserName;
            this.m_sApiTokenEncrypted = sApiTokenEncrypted;
            this.m_sDomain = sDomain;
        }

        /// <summary>
        /// Requests the JSON from a URL.
        /// </summary>
        /// <param name="sUrl">The URL to query.</param>
        /// <param name="baPostData">HTTP POST data to send.</param>
        /// <returns>JSON.nt JObject</returns>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        public async Task<JObject> RequestJSONAsync( string sUrl, byte[] baPostData, CancellationToken oCT ) {
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
            // Setup the cancelation token.
            CancellationTokenRegistration oCTReg = oCT.Register( () => {
                try { if ( oRequest != null ) oRequest.Abort(); oCT.ThrowIfCancellationRequested(); } catch { }
            } );
            // Execute request.
            string sSerializedJson;
            try {
                using ( HttpWebResponse oResponse = (HttpWebResponse) await oRequest.GetResponseAsync() ) {
                    if ( oResponse.StatusCode != HttpStatusCode.OK ) {
                        string sDetails =
                            String.Format( "HTTP {1}; URL: {0}", oResponse.StatusCode.ToString(), sUrl );
                        throw new QueryAPIException( 200, sDetails );
                    }
                    using ( StreamReader oStream = new StreamReader( oResponse.GetResponseStream(), true ) ) {
                        sSerializedJson = oStream.ReadToEnd();
                    }
                }
            } catch ( WebException e ) {
                string sDetails = String.Format( "URL: {0}; WebException: ", sUrl, e.ToString() );
                throw new QueryAPIException( 201, sDetails );
            } finally {
                oCTReg.Dispose();
            }
            // Parse JSON.
            JObject oJson;
            try {
                oJson = JObject.Parse( sSerializedJson );
            } catch ( Exception e ) {
                string sDetails = String.Format( "URL: {0}; Exception: ", sUrl, e.ToString() );
                throw new QueryAPIException( 202, sDetails );
            }
            return oJson;
        }

        public void Dispose() {
            this.m_sUserName = null;
            this.m_sApiTokenEncrypted = null;
            this.m_sDomain = null;
            this.m_sCurrentIP = null;
            this.m_sPriDomain = null;
            this.m_dRecordedIP = null;
            this.m_sSessionToken = null;
        }
    }
}
