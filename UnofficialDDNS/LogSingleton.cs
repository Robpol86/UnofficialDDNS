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
        private EventLog oEventLog = null; // Holds the application's Event Log object.
        
        /// <summary>
        /// Setup the singleton class.
        /// </summary>
        private static readonly LogSingleton instance = new LogSingleton();
        private LogSingleton() { }
        public static LogSingleton Instance { get { return instance; } }

        /// <summary>
        /// Sets the Event Log object only once.
        /// </summary>
        /// <param name="oEventLog">The application's EventLog object.</param>
        public void SetEventLog( EventLog oEventLog ) {
            if ( this.oEventLog == null ) this.oEventLog = oEventLog;
        }

        /// <summary>
        /// Write an error to the Event Log with the specified event ID and the message inside the Strings resource.
        /// </summary>
        /// <param name="iCode">Event ID with correlating message.</param>
        public void Log( int iCode ) {
            string message = Strings.ResourceManager.GetString( "Event" + iCode.ToString() );
            this.oEventLog.WriteEntry( message, EventLogEntryType.Error, iCode );
        }

        /// <summary>
        /// Write an error to Event Log with an additional message.
        /// </summary>
        /// <param name="iCode">Event ID with correlating message.</param>
        /// <param name="sAdditional">Additional message text after a new line.</param>
        public void Log( int iCode, string sAdditional ) {
            string message = Strings.ResourceManager.GetString( "Event" + iCode.ToString() );
            if ( sAdditional != null ) message += System.Environment.NewLine + sAdditional;
            this.oEventLog.WriteEntry( message, EventLogEntryType.Error, iCode );
        }

        /// <summary>
        /// Write an event to the Event Log using a specific event type.
        /// </summary>
        /// <param name="iCode">Event ID with correlating message.</param>
        /// <param name="sAdditional">Additional message text after a new line.</param>
        /// <param name="oType">Event type (error, warning, or information).</param>
        public void Log( int iCode, string sAdditional, EventLogEntryType oType ) {
            string message = Strings.ResourceManager.GetString( "Event" + iCode.ToString() );
            if ( sAdditional != null ) message += System.Environment.NewLine + sAdditional;
            this.oEventLog.WriteEntry( message, oType, iCode );
        }
    }
}
