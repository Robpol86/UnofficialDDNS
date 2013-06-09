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

        /// <summary>
        /// Singleton instance.
        /// </summary>
        /// <value>
        /// The instance object.
        /// </value>
        public static LogSingleton Instance { get { lock ( _padlock ) { if ( _instance == null ) _instance = new LogSingleton(); return _instance; } } }

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
        public void Log( int code, string details ) {
            Log( code, details, EventLogEntryType.Error );
        }
    }
}
