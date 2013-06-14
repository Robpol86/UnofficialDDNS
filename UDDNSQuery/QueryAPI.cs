using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace UDDNSQuery {
    
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
        public static QueryAPIIndex I { get { lock ( _padlock ) { if ( _instance == null ) _instance = new QueryAPIIndex(); return _instance; } } }
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
                default: throw new QueryAPIException( 104 );
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
        /// Indicates if the user has requested cancellation.
        /// </summary>
        bool UserCanceled { get; set; }

        /// <summary>
        /// Requests the JSON from a URL.
        /// </summary>
        /// <param name="uriPath">The remainder of the URL to query (e.g. /index.html)</param>
        /// <param name="postData">HTTP POST data to send.</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>JSON.net JObject</returns>
        /// <exception cref="QueryAPIException" />
        /// <exception cref="OperationCancelledException" />
        Task<JObject> RequestJSONAsync( string uriPath, StringContent postData, CancellationToken ct );
        
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
    public class QueryAPIException : Exception {
        protected int _code; // Error code.
        protected string _details; // Additional details about the error (error messages from API for example).
        protected string _resxMessage; // String from Errors.resx.
        protected string _url; // URL to project wiki for more information about the error.

        /// <summary>
        /// The numerical code for this error.
        /// </summary>
        /// <value>
        /// Error code.
        /// </value>
        public int Code { get { return _code; } }
        /// <summary>
        /// Additional information about the error, not in the strings resource.
        /// </summary>
        /// <value>
        /// Details text.
        /// </value>
        public string Details { get { return _details; } }
        /// <summary>
        /// The default message in the strings resource correlating to the error code.
        /// </summary>
        /// <value>
        /// Strings resource text.
        /// </value>
        public string RMessage { get { return _resxMessage; } }
        /// <summary>
        /// URL about this error code if avaiable for more information.
        /// </summary>
        /// <value>
        /// The URL.
        /// </value>
        public string Url { get { return _url; } }

        public QueryAPIException( int code ) : this( code, null ) { }

        public QueryAPIException( int code, string details ) : base( code.ToString() ) {
            _code = code;
            _details = details;
            _resxMessage = Errors.ResourceManager.GetString( "Error" + code );
            int[] moreInfo = { 100, 1 }; // TODO real errors instead of these placeholders.
            if ( moreInfo.Contains( code ) ) _url = "https://github.com/Robpol86/UnofficialDDNS/wiki/Errors#error-" + code.ToString();
        }
    }

    #endregion

    abstract class QueryAPI : IQueryAPI {
        protected Uri _baseUri; // The base URI to query (e.g. http://domain.com:80).
        protected bool _userCanceled; // Helps differentiate between timeouts and actual user-requested cancellation.
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
        public bool UserCanceled { get { return _userCanceled; } set { _userCanceled = value; } }

        public QueryAPI( Uri baseUri ) {
            _baseUri = baseUri;
        }

        public virtual async Task<JObject> RequestJSONAsync( string uriPath, StringContent postData, CancellationToken ct ) {
            string url = new Uri( _baseUri, uriPath ).AbsoluteUri;
            string serializedJson = null;
            using ( HttpClient client = new HttpClient() ) {
                // Setup HTTP request.
                client.BaseAddress = _baseUri;
                client.Timeout = new TimeSpan( 0, 0, 5 ); // Timeout in 5 seconds.
                client.DefaultRequestHeaders.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/json" ) );
                if ( _sessionToken != null ) client.DefaultRequestHeaders.Add( "Api-Session-Token", _sessionToken );
                HttpRequestMessage request = new HttpRequestMessage( postData != null ? HttpMethod.Post : HttpMethod.Get, uriPath );
                if ( postData != null ) request.Content = postData;
                // Execute request.
                HttpResponseMessage response;
                try {
                    response = await client.SendAsync( request, HttpCompletionOption.ResponseContentRead, ct );
                    if ( !response.IsSuccessStatusCode ) {
                        string details = String.Format( "HTTP {0}; URL: {1}", response.StatusCode.ToString(), url );
                        throw new QueryAPIException( 200, details );
                    }
                    using ( StreamReader stream = new StreamReader( await response.Content.ReadAsStreamAsync(), true ) ) {
                        serializedJson = await stream.ReadToEndAsync();
                    }
                } catch ( HttpRequestException e ) {
                    string details = String.Format( "URL: {0}\nHttpRequestException: {1}", url, e.ToString() );
                    throw new QueryAPIException( 201, details );
                } catch ( TaskCanceledException ) {
                    if ( _userCanceled ) throw; // Handled farther up in the stack.
                    throw new QueryAPIException( 205, "URL: " + url );
                }
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
