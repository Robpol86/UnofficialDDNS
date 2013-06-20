// ***********************************************************************
// Assembly         : UDDNSQuery
// Author           : Robpol86
// Created          : 04-24-2013
//
// Last Modified By : Robpol86
// Last Modified On : 06-17-2013
// ***********************************************************************
// <copyright file="QueryAPI.cs" company="">
//      Copyright (c) 2013 All rights reserved.
//      This software is made available under the terms of the MIT License
//      that can be found in the LICENSE.txt file.
// </copyright>
// <summary></summary>
// ***********************************************************************

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;

namespace UDDNSQuery {
    
    #region Helper Objects

    /// <summary>
    /// Singleton which centralizes the collection of supported registrars, the string presented in the installer, and
    /// the object used to actually query the registrar.
    /// </summary>
    public sealed class QueryAPIIndex {
        private IDictionary<string, string> _registrarList = new Dictionary<string, string>();
        private static QueryAPIIndex _instance = null;
        private static readonly object _padlock = new object();

        /// <summary>
        /// Singleton instance.
        /// </summary>
        /// <value>The instance object.</value>
        public static QueryAPIIndex I { get { lock ( _padlock ) { if ( _instance == null ) _instance = new QueryAPIIndex(); return _instance; } } }
        /// <summary>
        /// Collection of supported registrars.
        /// </summary>
        /// <value>IDictionary containing all registrars.</value>
        public IDictionary<string, string> Registrars { get { return _registrarList; } }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryAPIIndex" /> class. This is where the list of valid
        /// registrars is kept.
        /// </summary>
        private QueryAPIIndex() {
            _registrarList.Add( "Name.com", "http://name.com/reseller" );
        }

        /// <summary>
        /// Class factory for QueryAPI objects for specific registrars.
        /// </summary>
        /// <param name="registrar">The desired registrar.</param>
        /// <returns>Registrar specific instance which inherits QueryAPI.</returns>
        /// <exception cref="UDDNSQuery.QueryAPIException" />
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
        /// Gets an integer that represents the total number of characters in the user name.
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
        /// The IP address currently set at the registrar. Might be more than one in case last update attempt failed.
        /// </summary>
        IDictionary<string, string> RecordedIP { get; }
        /// <summary>
        /// Indicates if the user has requested cancellation.
        /// </summary>
        /// <value><c>true</c> if [user canceled]; otherwise, <c>false</c>.</value>
        bool UserCanceled { get; set; }

        /// <summary>
        /// Requests the JSON from a URL.
        /// </summary>
        /// <param name="uriPath">The remainder of the URL to query (e.g. /index.html)</param>
        /// <param name="postData">HTTP POST data to send.</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>JSON.net JObject</returns>
        /// <exception cref="QueryAPIException"></exception>
        /// <exception cref="TaskCanceledException"></exception>
        Task<JObject> RequestJSONAsync( string uriPath, StringContent postData, CancellationToken ct );

        /// <summary>
        /// Pass credentials and the target domain to class instance.
        /// </summary>
        /// <param name="userName">API user name.</param>
        /// <param name="apiTokenEncrypted">The encrypted and base64 encoded API token/password.</param>
        /// <param name="domain">The fully qualified domain name target.</param>
        void Credentials( string userName, string apiTokenEncrypted, string domain );

        /// <summary>
        /// Gets the current public IP.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Task.</returns>
        /// <exception cref="QueryAPIException"></exception>
        /// <exception cref="TaskCanceledException"></exception>
        Task GetCurrentIPAsync( CancellationToken ct );

        /// <summary>
        /// Authenticates to the API.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Task.</returns>
        /// <exception cref="QueryAPIException"></exception>
        /// <exception cref="TaskCanceledException"></exception>
        Task AuthenticateAsync( CancellationToken ct );

        /// <summary>
        /// Makes sure the user owns this domain. Responsible for _mainDomain.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Task.</returns>
        Task ValidateDomainAsync( CancellationToken ct );

        /// <summary>
        /// Gets all records related to the domain. Responsible for _recordedIP.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Task.</returns>
        /// <exception cref="QueryAPIException"></exception>
        /// <exception cref="TaskCanceledException"></exception>
        Task GetRecordsAsync( CancellationToken ct );

        /// <summary>
        /// Appends the CurrentIP to the domain as an A record and then purges all other records for the domain.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Task.</returns>
        /// <exception cref="QueryAPIException"></exception>
        /// <exception cref="TaskCanceledException"></exception>
        Task UpdateRecordAsync( CancellationToken ct );

        /// <summary>
        /// De-authenticate from the API.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Task.</returns>
        /// <exception cref="TaskCanceledException"></exception>
        Task LogoutAsync( CancellationToken ct );
    }

    /// <summary>
    /// API-specific exceptions.
    /// </summary>
    [Serializable]
    public class QueryAPIException : Exception {
        /// <summary>
        /// Value for Code property.
        /// </summary>
        protected int _code;
        /// <summary>
        /// Value for Details property.
        /// </summary>
        protected string _details;
        /// <summary>
        /// Value for RMessage property.
        /// </summary>
        protected string _resxMessage;
        /// <summary>
        /// Value for Url property.
        /// </summary>
        protected string _url;

        /// <summary>
        /// The numerical code for this error.
        /// </summary>
        /// <value>Error code.</value>
        public int Code { get { return _code; } }
        /// <summary>
        /// Additional details about the error, not in the errors resource.
        /// </summary>
        /// <value>Details text.</value>
        public string Details { get { return _details; } }
        /// <summary>
        /// The default message in the errors resource correlating to the error code.
        /// </summary>
        /// <value>Strings resource text.</value>
        public string RMessage { get { return _resxMessage; } }
        /// <summary>
        /// URL about this error code if available for more information.
        /// </summary>
        /// <value>The URL.</value>
        public string Url { get { return _url; } }

        /// <summary>
        /// Throws an exception without additional details.
        /// </summary>
        /// <param name="code">The error code.</param>
        public QueryAPIException( int code ) : this( code, null ) { }

        /// <summary>
        /// Throws an exception with an error code and additional details.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="details">The additional details.</param>
        public QueryAPIException( int code, string details ) : base( code.ToString() ) {
            _code = code;
            _details = details;
            _resxMessage = Errors.ResourceManager.GetString( "Error" + code );
            int[] moreInfo = { 100, 1 }; // TODO real errors instead of these placeholders.
            if ( moreInfo.Contains( code ) ) _url = "https://github.com/Robpol86/UnofficialDDNS/wiki/Errors#error-" + code.ToString();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Exception" /> class with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext" /> that contains contextual information about the source or destination.</param>
        protected QueryAPIException( SerializationInfo info, StreamingContext context ) : base(info, context) { }

        /// <summary>
        /// When overridden in a derived class, sets the <see cref="T:System.Runtime.Serialization.SerializationInfo" /> with information about the exception.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext" /> that contains contextual information about the source or destination.</param>
        /// <PermissionSet>
        ///   <IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Read="*AllFiles*" PathDiscovery="*AllFiles*" />
        ///   <IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="SerializationFormatter" />
        ///   </PermissionSet>
        [SecurityPermission( SecurityAction.Demand, SerializationFormatter = true )]
        public override void GetObjectData( SerializationInfo info, StreamingContext context ) {
            base.GetObjectData( info, context );
        }
    }

    #endregion

    /// <summary>
    /// Sub-class containing common methods and properties to be used by all other QueryAPI implementation classes.
    /// </summary>
    abstract class QueryAPI : IQueryAPI {
        /// <summary>
        /// The base URI to query (e.g. http://domain.com:80).
        /// </summary>
        protected Uri _baseUri;
        /// <summary>
        /// Helps differentiate between timeouts and actual user-requested cancellation.
        /// </summary>
        protected bool _userCanceled;
        /// <summary>
        /// // API user name.
        /// </summary>
        protected string _userName;
        /// <summary>
        /// // API token, locally encrypted.
        /// </summary>
        protected string _apiTokenEncrypted;
        /// <summary>
        /// // Domain which will hold the IP address of this host.
        /// </summary>
        protected string _domain;
        /// <summary>
        /// Main domain on the user's account, in case _domain is a sub-domain.
        /// </summary>
        protected string _mainDomain;
        /// <summary>
        /// Current public IP of this host.
        /// </summary>
        protected string _currentIP;
        /// <summary>
        /// A or CNAME record(s) associated with this domain.
        /// </summary>
        protected IDictionary<string, string> _recordedIP;
        /// <summary>
        /// Session token to insert in HTTP header.
        /// </summary>
        protected string _sessionToken;

        /// <summary>
        /// Gets an integer that represents the total number of characters in the username.
        /// </summary>
        public int UserLength { get { return _userName.Length; } }
        /// <summary>
        /// Gets an integer that represents the total number of characters in the (not encrypted) API token.
        /// </summary>
        public int TokenLength { get { return _apiTokenEncrypted.Length; } }
        /// <summary>
        /// Gets an integer that represents the total number of characters in the domain.
        /// </summary>
        public int DomainLength { get { return _domain.Length; } }
        /// <summary>
        /// The current IP address of this host.
        /// </summary>
        public string CurrentIP { get { return _currentIP; } }
        /// <summary>
        /// The IP address currently set at the registrar. Might be more than one incase last update attempt failed.
        /// </summary>
        public IDictionary<string, string> RecordedIP { get { return _recordedIP; } }
        /// <summary>
        /// Indicates if the user has requested cancellation.
        /// </summary>
        /// <value><c>true</c> if [user canceled]; otherwise, <c>false</c>.</value>
        public bool UserCanceled { get { return _userCanceled; } set { _userCanceled = value; } }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryAPI"/> class.
        /// </summary>
        /// <param name="baseUri">The base URI.</param>
        public QueryAPI( Uri baseUri ) {
            _baseUri = baseUri;
        }

        /// <summary>
        /// Requests the JSON from a URL.
        /// </summary>
        /// <param name="uriPath">The remainder of the URL to query (e.g. /index.html)</param>
        /// <param name="postData">HTTP POST data to send.</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>JSON.net JObject</returns>
        /// <exception cref="UDDNSQuery.QueryAPIException" />
        public virtual async Task<JObject> RequestJSONAsync( string uriPath, StringContent postData, CancellationToken ct ) {
            string url = new Uri( _baseUri, uriPath ).AbsoluteUri;
            string serializedJson = null;
            using ( HttpClient client = new HttpClient() ) {
                // Setup HTTP request.
                client.BaseAddress = _baseUri;
                client.Timeout = new TimeSpan( 0, 0, 10 ); // Timeout in 10 seconds.
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

        /// <summary>
        /// Pass credentials and the target domain to class instance.
        /// </summary>
        /// <param name="userName">API Username.</param>
        /// <param name="apiTokenEncrypted">The encrypted and base64 encoded API token/password.</param>
        /// <param name="domain">The fully qualified domain name target.</param>
        public void Credentials( string userName, string apiTokenEncrypted, string domain ) {
            _userName = userName;
            _apiTokenEncrypted = apiTokenEncrypted;
            _domain = domain;
        }

        /// <summary>
        /// Gets the current public IP.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Task.</returns>
        public abstract Task GetCurrentIPAsync( CancellationToken ct );

        /// <summary>
        /// Authenticates to the API.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Task.</returns>
        public abstract Task AuthenticateAsync( CancellationToken ct );

        /// <summary>
        /// Makes sure the user owns this domain. Responsible for _mainDomain.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Task.</returns>
        public abstract Task ValidateDomainAsync( CancellationToken ct );

        /// <summary>
        /// Gets all records related to the domain. Responsible for _recordedIP.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Task.</returns>
        public abstract Task GetRecordsAsync( CancellationToken ct );

        /// <summary>
        /// Appends the CurrentIP to the domain as an A record and then purges all other records for the domain.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Task.</returns>
        public abstract Task UpdateRecordAsync( CancellationToken ct );

        /// <summary>
        /// De-authenticate from the API.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Task.</returns>
        public abstract Task LogoutAsync( CancellationToken ct );

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
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
