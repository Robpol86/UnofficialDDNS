using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UDDNSQuery {
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
        
        public IQueryAPI Factory( string sRegistrar, string sUserName, string sApiTokenEncrypted, string sDomain ) {
            switch ( sRegistrar ) {
                case "Name.com": return new QueryAPIName( sUserName, sApiTokenEncrypted, sDomain );
                default: return null;
            }
        }
    }

    public interface IQueryAPI {
        string CurrentIP { get; }
        IDictionary<string, string> RecordedIP { get; }

        //JObject RequestJSON( string sUrl, byte[] baPostData ); TODO
        void GetCurrentIP();
        void Authenticate();
        void GetPriDomain();
        void GetRecords();
        void UpdateDNSRecord();
        void Logout();
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

        public string CurrentIP { get { return this.m_sCurrentIP; } }
        public IDictionary<string, string> RecordedIP { get { return this.m_dRecordedIP; } }

        public QueryAPI( string sUserName, string sApiTokenEncrypted, string sDomain ) {
            this.m_sUserName = sUserName;
            this.m_sApiTokenEncrypted = sApiTokenEncrypted;
            this.m_sDomain = sDomain;
        }
    }
}
