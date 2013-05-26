using Newtonsoft.Json;
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
     *  URLS in dialog to GitHub page with screenshots and help for each error.
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
        /// The IP address currently set at the registrar. Might be more than one incase last update attempt failed.
        /// </summary>
        IDictionary<string, string> RecordedIP { get; }

        /// <summary>
        /// Requests the JSON from a URL.
        /// </summary>
        /// <param name="url">The URL to query.</param>
        /// <param name="postData">HTTP POST data to send.</param>
        /// <param name="ct">Cancellation token</param>
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
        /// <param name="ct">Cancellation token</param>
        /// <returns></returns>
        /// <exception cref="QueryAPIException"></exception>
        /// <exception cref="OperationCancelledException"></exception>
        Task GetCurrentIPAsync( CancellationToken ct );

        /// <summary>
        /// Authenticates to the API.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        Task AuthenticateAsync( CancellationToken ct );

        /// <summary>
        /// Makes sure the user owns this domain. Responsible for _mainDomain.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        Task ValidateDomainAsync( CancellationToken ct );

        /// <summary>
        /// Gets all records related to the domain. Responsible for _recordedIP.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        Task GetRecordsAsync( CancellationToken ct );

        /// <summary>
        /// Appends the CurrentIP to the domain as an A record and then purges all other records for the domain.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        Task UpdateRecordAsync( CancellationToken ct );

        /// <summary>
        /// De-authenticate from the API.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
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

        public QueryAPIException( int code ) : this( code, null ) { }

        public QueryAPIException( int code, string details ) : base( code.ToString() ) {
            _code = code;
            _details = details;
            _resxMessage = Errors.ResourceManager.GetString( "Error" + code );
        }
    }

    #endregion

    abstract class QueryAPI : IQueryAPI {
        protected string _userName; // API username.
        protected string _apiTokenEncrypted; // API token, locally encrypted.
        protected string _domain; // Domain which will hold the IP address of this host.
        protected string _mainDomain; // Main domain on the user's account, incase _domain is a subdomain.
        protected string _currentIP; // Current public IP of this host.
        protected IDictionary<string, string> _recordedIP; // A record(s) associated with this domain.
        protected string _sessionToken; // Session token to insert in HTTP header.

        public int UserLength { get { return _userName.Length; } }
        public int TokenLength { get { return _apiTokenEncrypted.Length; } }
        public int DomainLength { get { return _domain.Length; } }
        public string CurrentIP { get { return _currentIP; } }
        public IDictionary<string, string> RecordedIP { get { return _recordedIP; } }

        public QueryAPI() { }

        public virtual async Task<JObject> RequestJSONAsync( string url, byte[] postData, CancellationToken ct ) {
            // Setup HTTP request.
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create( url );
            request.ContentType = "application/x-www-form-urlencoded";
            request.Accept = "application/json";
            if ( _sessionToken != null ) request.Headers.Add( "Api-Session-Token", _sessionToken );
            request.Method = postData != null ? "POST" : "GET";
            if ( postData != null ) {
                request.ContentLength = postData.Length;
                Stream stream = request.GetRequestStream();
                stream.Write( postData, 0, postData.Length );
                stream.Close();
            }
            // Setup the cancelation token.
            CancellationTokenRegistration ctReg = ct.Register( () => {
                try { if ( request != null ) request.Abort(); ct.ThrowIfCancellationRequested(); } catch { }
            } );
            // Execute request.
            string serializedJson;
            try {
                using ( HttpWebResponse response = (HttpWebResponse) await request.GetResponseAsync() ) {
                    if ( response.StatusCode != HttpStatusCode.OK ) {
                        string details = String.Format( "HTTP {0}; URL: {1}", response.StatusCode.ToString(), url );
                        throw new QueryAPIException( 200, details );
                    }
                    using ( StreamReader stream = new StreamReader( response.GetResponseStream(), true ) ) {
                        serializedJson = stream.ReadToEnd();
                    }
                }
            } catch ( WebException e ) {
                string details = String.Format( "URL: {0}\nWebException: {1}", url, e.ToString() );
                throw new QueryAPIException( 201, details );
            } finally {
                ctReg.Dispose();
            }
            // Parse JSON.
            JObject json;
            try {
                json = JObject.Parse( serializedJson );
            } catch ( JsonReaderException e ) {
                string details = String.Format( "URL: {0}\nException: {1}", url, e.ToString() );
                throw new QueryAPIException( 202, details );
            }
            return json;
        }

        public void Credentials( string userName, string apiTokenEncrypted, string domain ) {
            _userName = userName;
            _apiTokenEncrypted = apiTokenEncrypted;
            _domain = domain;
        }

        public abstract Task GetCurrentIPAsync( CancellationToken ct );

        public abstract Task AuthenticateAsync( CancellationToken ct );

        public abstract Task ValidateDomainAsync( CancellationToken ct );

        public abstract Task GetRecordsAsync( CancellationToken ct );

        public abstract Task UpdateRecordAsync( CancellationToken ct );

        public abstract Task LogoutAsync( CancellationToken ct );

        public void Dispose() {
            _userName = null;
            _apiTokenEncrypted = null;
            _domain = null;
            _mainDomain = null;
            _currentIP = null;
            _recordedIP = null;
            _sessionToken = null;
        }
    }
}
