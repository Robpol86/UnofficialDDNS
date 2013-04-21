/**
 * Copyright (c) 2013, Robpol86
 * This software is made available under the terms of the MIT License that can
 * be found in the LICENSE.txt file.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnofficialDDNS {
    class CustomException : Exception {
        public int m_iCode;
        public string m_sAdditional;
        public CustomException( int iCode, string sAdditional ) : base( iCode.ToString() ) {
            this.m_iCode = iCode;
            this.m_sAdditional = sAdditional;
            LogSingleton.Instance.Log( iCode, sAdditional );
        }
    }

    [Serializable]
    class QueryException : CustomException {
        public QueryException( int iCode, string sAdd ) : base( iCode, sAdd ) { }
    }
    
    [Serializable]
    class RegistryException : Exception {
        public int m_iCode;
        public RegistryException( int iCode ) : base( iCode.ToString() ) { this.m_iCode = iCode; }
    }
}
