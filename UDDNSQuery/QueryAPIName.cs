using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UDDNSQuery {
    class QueryAPIName : QueryAPI, IQueryAPI {
        private static readonly string m_sUrlPrefix = "https://api.name.com";
        private static readonly string m_sUrlGetCurrentIP = m_sUrlPrefix + "/api/hello";
        private static readonly string m_sUrlAuthenticate = m_sUrlPrefix + "/api/login";
        private static readonly string m_sUrlGetPriDomain = m_sUrlPrefix + "/api/domain/list";
        private static readonly string m_sUrlGetRecordsPrefix = m_sUrlPrefix + "/api/dns/list/";
        private static readonly string m_sUrlDeleteRecordPrefix = m_sUrlPrefix + "/api/dns/delete/";
        private static readonly string m_sUrlCreateRecordPrefix = m_sUrlPrefix + "/api/dns/create/";
        private static readonly string m_sUrlLogout = m_sUrlPrefix + "/api/logout";

        public QueryAPIName( string sUserName, string sApiTokenEncrypted, string sDomain ) : 
            base( sUserName, sApiTokenEncrypted, sDomain ) { }

        public void GetCurrentIP() { }
        public void Authenticate() { }
        public void GetPriDomain() { }
        public void GetRecords() { }
        public void UpdateDNSRecord() { }
        public void Logout() { }
    }
}
