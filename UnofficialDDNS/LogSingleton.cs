/**
 * Copyright (c) 2013, Robpol86
 * This software is made available under the terms of the MIT License that can
 * be found in the LICENSE.txt file.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnofficialDDNS {
    public sealed class LogSingleton {
        private EventLog _eventLog = new EventLog();
        private static LogSingleton _instance = null;
        private static readonly object _padlock = new object();
        private bool _debug = false;

        /// <summary>
        /// Singleton instance.
        /// </summary>
        /// <value>
        /// The instance object.
        /// </value>
        public static LogSingleton I { get { lock ( _padlock ) { if ( _instance == null ) _instance = new LogSingleton(); return _instance; } } }

        /// <summary>
        /// Enables debug logging.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [debug enabled]; otherwise, <c>false</c>.
        /// </value>
        public bool EnableDebug { get { return _debug; } set { _debug = value; } }

        private LogSingleton() {
            ((System.ComponentModel.ISupportInitialize)(_eventLog)).BeginInit();
            _eventLog.Log = "Application";
            _eventLog.Source = "UnofficialDDNS";
            ((System.ComponentModel.ISupportInitialize)(_eventLog)).EndInit();
        }

        /// <summary>
        /// Write an event to the Event Log using a specific event type.
        /// </summary>
        /// <param name="code">Event ID.</param>
        /// <param name="details">Details about the error.</param>
        /// <param name="type">Event type (error, warning, or information).</param>
        public void Log( int code, string details, EventLogEntryType type ) {
            _eventLog.WriteEntry( details, type, code );
        }
        
        /// <summary>
        /// Write an error to Event Log with an additional message.
        /// </summary>
        /// <param name="code">Event ID.</param>
        /// <param name="details">Details about the error.</param>
        public void Error( int code, string details ) {
            Log( code, details, EventLogEntryType.Error );
        }

        /// <summary>
        /// Write a warning to Event Log with an additional message.
        /// </summary>
        /// <param name="code">Event ID.</param>
        /// <param name="details">Details about the error.</param>
        public void Warning( int code, string details ) {
            Log( code, details, EventLogEntryType.Warning );
        }

        /// <summary>
        /// Write information to Event Log with an additional message.
        /// </summary>
        /// <param name="code">Event ID.</param>
        /// <param name="details">Details about the error.</param>
        public void Info( int code, string details ) {
            Log( code, details, EventLogEntryType.Information );
        }
        
        /// <summary>
        /// Writes debug messages (as Information) to the event log, only if EnableDebug is true.
        /// </summary>
        /// <param name="details">Details about the error.</param>
        public void Debug( string details ) {
            if ( _debug ) Info( 999, details );
        }
    }
}
