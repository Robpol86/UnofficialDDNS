/**
 * Copyright (c) 2013, Robpol86
 * This software is made available under the terms of the MIT License that can
 * be found in the LICENSE.txt file.
 */

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace UnofficialDDNS {
    class RegistryRead {
        private int m_iInterval_minutes = 0;
        private int m_iInterval_minutes_error = 0;
        private string m_sRegistrar = null;
        private string m_sUserName = null;
        private byte[] m_baApiToken = null;
        private string m_sDomain = null;
        
        public int Interval { get { return this.m_iInterval_minutes; } }
        public int IntervalError { get { return this.m_iInterval_minutes_error; } }
        public string Registrar { get { return this.m_sRegistrar; } }
        public string UserName { get { return this.m_sUserName; } }
        public byte[] ApiToken { get { return this.m_baApiToken; } }
        public string Domain { get { return this.m_sDomain; } }
        public static string RegPath {
            get {
                if ( Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ) {
                    return @"HKLM\SOFTWARE\Wow6432Node\UnofficialDDNS";
                } else {
                    return @"HKLM\SOFTWARE\UnofficialDDNS";
                }
            }
        }

        public RegistryRead() {
            RegistryKey oReg = Registry.LocalMachine.OpenSubKey( @"SOFTWARE\UnofficialDDNS" );
            if ( oReg == null ) throw new RegistryException( 10000 );
            Tuple<string, RegistryValueKind> tPair;

            // interval_minutes
            tPair = Tuple.Create("interval_minutes", RegistryValueKind.DWord);
            if ( !oReg.GetValueNames().Contains( tPair.Item1 ) || oReg.GetValueKind( tPair.Item1 ) != tPair.Item2 ) {
                throw new RegistryException( 10001 );
            } else { this.m_iInterval_minutes = (int) oReg.GetValue( tPair.Item1, 0 ); }

            // interval_minutes_error
            tPair = Tuple.Create( "interval_minutes_error", RegistryValueKind.DWord );
            if ( !oReg.GetValueNames().Contains( tPair.Item1 ) || oReg.GetValueKind( tPair.Item1 ) != tPair.Item2 ) {
                throw new RegistryException( 10002 );
            } else { this.m_iInterval_minutes_error = (int) oReg.GetValue( tPair.Item1, 0 ); }

            // registrar
            tPair = Tuple.Create( "registrar", RegistryValueKind.String );
            if ( !oReg.GetValueNames().Contains( tPair.Item1 ) || oReg.GetValueKind( tPair.Item1 ) != tPair.Item2 ) {
                throw new RegistryException( 10003 );
            } else { this.m_sRegistrar = (string) oReg.GetValue( tPair.Item1, null ); }

            // username
            tPair = Tuple.Create( "username", RegistryValueKind.String );
            if ( !oReg.GetValueNames().Contains( tPair.Item1 ) || oReg.GetValueKind( tPair.Item1 ) != tPair.Item2 ) {
                throw new RegistryException( 10004 );
            } else { this.m_sUserName = (string) oReg.GetValue( tPair.Item1, null ); }

            // apitoken
            tPair = Tuple.Create( "apitoken", RegistryValueKind.Binary );
            if ( !oReg.GetValueNames().Contains( tPair.Item1 ) || oReg.GetValueKind( tPair.Item1 ) != tPair.Item2 ) {
                throw new RegistryException( 10005 );
            } else {
                byte[] baApiToken = (byte[]) oReg.GetValue( tPair.Item1, null );
                try {
                    string sApiToken = Encoding.ASCII.GetString(
                        ProtectedData.Unprotect( baApiToken, null, DataProtectionScope.LocalMachine )
                        );
                    sApiToken = null;
                } catch ( CryptographicException ) {
                    baApiToken = null;
                    throw new RegistryException( 10006 );
                }
                this.m_baApiToken = baApiToken;
            }

            // domain
            tPair = Tuple.Create( "domain", RegistryValueKind.String );
            if ( !oReg.GetValueNames().Contains( tPair.Item1 ) || oReg.GetValueKind( tPair.Item1 ) != tPair.Item2 ) {
                throw new RegistryException( 10007 );
            } else { this.m_sDomain = (string) oReg.GetValue( tPair.Item1, null ); }

            oReg.Close();
        }

        public void RegistryWrite() {
            //TODO
        }
    }
}
