// ***********************************************************************
// Assembly         : UnofficialDDNS
// Author           : Robpol86
// Created          : 04-20-2013
//
// Last Modified By : Robpol86
// Last Modified On : 06-17-2013
// ***********************************************************************
// <copyright file="LogSingleton.cs" company="">
//      Copyright (c) 2013 All rights reserved.
//      This software is made available under the terms of the MIT License
//      that can be found in the LICENSE.txt file.
// </copyright>
// <summary></summary>
// ***********************************************************************

using System;
using System.Diagnostics;

namespace UnofficialDDNS {
    /// <summary>
    /// Singleton class which writes to the event log.
    /// </summary>
    public sealed class LogSingleton : IDisposable {
        private EventLog _eventLog = new EventLog();
        private static LogSingleton _instance = null;
        private static readonly object _padlock = new object();
        private bool _debug = false;
        private int _debugcount = 0;

        /// <summary>
        /// Singleton instance.
        /// </summary>
        /// <value>The instance object.</value>
        public static LogSingleton I { get { lock ( _padlock ) { if ( _instance == null ) _instance = new LogSingleton(); return _instance; } } }

        /// <summary>
        /// Enables debug logging.
        /// </summary>
        /// <value><c>true</c> if [debug enabled]; otherwise, <c>false</c>.</value>
        public bool EnableDebug { get { return _debug; } set { _debug = value; } }

        /// <summary>
        /// Initializes the event log source.
        /// </summary>
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
            if ( !_debug ) return;
            lock ( _padlock ) {
                _debugcount++;
                Info( _debugcount, details );
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() {
            _eventLog.Dispose();
        }
    }
}
