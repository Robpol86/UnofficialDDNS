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
     *      Dialog will show progress.
     *      Dialog will allow user to cancel.
     *      Show screenshot example if more than 1 record/wrong record.
     *      Center dialogs over installer window.
     */
    public sealed class QueryAPIIndex {
        private IDictionary<string, string> m_dRegistrarList = new Dictionary<string, string>();

        /// <summary>
        /// Setup the singleton class and populate the registrar list.
        /// </summary>
        private static QueryAPIIndex instance = null;
        private static readonly object padlock = new object();
        private QueryAPIIndex() {
            this.m_dRegistrarList.Add( "Name.com", "http://name.com/reseller" );
        }
        public static QueryAPIIndex Instance {
            get {
                lock ( padlock ) {
                    if ( instance == null ) instance = new QueryAPIIndex();
                    return instance;
                }
            }
        }

        public IDictionary<string, string> RegistrarList { get { return this.m_dRegistrarList; } }
        
        public IQueryAPI Factory( string sRegistrar ) {
            switch ( sRegistrar ) {
                case "Name.com": return new QueryAPIName();
                default: return null;
            }
        }
    }

    public interface IQueryAPI : IDisposable {
        int UserLength { get; }
        int TokenLength { get; }
        int DomainLength { get; }
        string CurrentIP { get; }
        IDictionary<string, string> RecordedIP { get; }

        /// <summary>
        /// Requests the JSON from a URL.
        /// </summary>
        /// <param name="sUrl">The URL to query.</param>
        /// <param name="baPostData">HTTP POST data to send.</param>
        /// <returns>JSON.net JObject</returns>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        Task<JObject> RequestJSONAsync( string sUrl, byte[] baPostData, CancellationToken oCT );
        
        /// <summary>
        /// Pass credentials and the target domain to class instance.
        /// </summary>
        /// <param name="sUserName">API Username.</param>
        /// <param name="sApiTokenEncrypted">The encrypted and base64 encoded API token/password.</param>
        /// <param name="sDomain">The fully qualified domain name target.</param>
        void Credentials( string sUserName, string sApiTokenEncrypted, string sDomain );

        /// <summary>
        /// Gets the current public IP.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        Task GetCurrentIPAsync( CancellationToken oCT );

        /// <summary>
        /// Authenticates to the API.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        Task AuthenticateAsync( CancellationToken oCT );

        Task ValidateDomainAsync( CancellationToken oCT );
        
        /// <summary>
        /// Gets all records related to the domain.
        /// </summary>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        Task GetRecordsAsync( CancellationToken oCT );

        Task UpdateDNSRecordAsync( CancellationToken oCT );

        /// <summary>
        /// De-authenticate from the API.
        /// </summary>
        /// <exception cref="OperationCancelledException" />
        Task LogoutAsync( CancellationToken oCT );
        //TODO
    }

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

    [Serializable]
    class QueryAPIException : Exception {
        protected int m_iCode; // Error code.
        protected string m_sDetails; // Additional details about the error (error messages from API for example).
        protected string m_sResxMessage; // String from Errors.resx.

        public int Code { get { return this.m_iCode; } }
        public string Details { get { return this.m_sDetails; } }
        public string RMessage { get { return this.m_sResxMessage; } }

        public QueryAPIException( int iCode )
            : base( iCode.ToString() ) {
            this.m_iCode = iCode;
            this.m_sDetails = null;
            this.m_sResxMessage = Errors.ResourceManager.GetString( "Error" + iCode );
        }
        
        public QueryAPIException( int iCode, string sDetails )
            : base( iCode.ToString() ) {
            this.m_iCode = iCode;
            this.m_sDetails = sDetails;
            this.m_sResxMessage = Errors.ResourceManager.GetString( "Error" + iCode );
        }
    }
}
