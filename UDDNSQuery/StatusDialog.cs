// ***********************************************************************
// Assembly         : UDDNSQuery
// Author           : Robpol86
// Created          : 05-03-2013
//
// Last Modified By : Robpol86
// Last Modified On : 06-17-2013
// ***********************************************************************
// <copyright file="StatusDialog.cs" company="">
//      Copyright (c) 2013 All rights reserved.
//      This software is made available under the terms of the MIT License
//      that can be found in the LICENSE.txt file.
// </copyright>
// <summary>
//      Launches a TaskDialog showing the status of validation.
// </summary>
// ***********************************************************************

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UDDNSQuery {
    /* <references>
     *      http://truecheaters.com/f51/%5Bc-%5D-taskdialog-9368.html
     *      http://dotnet.dzone.com/articles/using-new-taskdialog-winapi
     *      http://stackoverflow.com/questions/16020136/taskdialog-fires-exception-comctl32-dll-in-version-6-required/16494194#16494194
     *      http://blogs.msdn.com/b/dotnet/archive/2012/06/06/async-in-4-5-enabling-progress-and-cancellation-in-async-apis.aspx
     * </references>
     */

    /// <summary>
    /// Opens a dialog showing the status of validation using the new-to-Vista TaskDialog.
    /// </summary>
    public class StatusDialog : TaskDialog {
        private IQueryAPI _api; // API object.
        private CancellationTokenSource _cts; // Cancellation for all API tasks.
        private int _progressMax; // The maximum value for progress bars.
        private bool _isWorking; // Is StatusDialog_Opened() busy or is it done?
        private static readonly object _padlock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="StatusDialog" /> class.
        /// </summary>
        /// <param name="api">The instantiated IQueryAPI object.</param>
        public StatusDialog( IQueryAPI api ) {
            // Initialize.
            _api = api;
            _cts = new CancellationTokenSource();
            _progressMax = 30;

            // Setup dialog/progress bar.
            Caption = Strings.StatusDialogTitle;
            InstructionText = Strings.StatusDialogHeading;
            ProgressBarRange = new int[2] { 0, _progressMax };
            ProgressBarValue = 1;
            ProgressBarState = TaskDialogProgressBarState.Normal;
            
            // Hook events.
            Closing += new EventHandler<CancelEventArgs>( StatusDialog_Canceled );
            Opened += new EventHandler( StatusDialog_Opened );
            HyperlinkClick += new EventHandler<HyperlinkEventArgs>( StatusDialog_Hyperlink );
        }

        /// <summary>
        /// Dispose the CancellationTokenSource and TaskDialog resources.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1063:ImplementIDisposableCorrectly" )]
        public override void Dispose() {
            _cts.Dispose();
            base.Dispose();
            GC.SuppressFinalize( this );
        }

        /// <summary>
        /// Handles the Hyperlink event of the TaskDialog control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void StatusDialog_Hyperlink( object sender, HyperlinkEventArgs e ) {
            Process.Start( e.LinkText );
        }

        /// <summary>
        /// Handles the Canceled event of the TaskDialog control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="CancelEventArgs"/> instance containing the event data.</param>
        private void StatusDialog_Canceled( object sender, CancelEventArgs e ) {
            if ( !_isWorking ) return;
            lock ( _padlock ) {
                if ( !_isWorking ) return;
                _api.UserCanceled = true;
                InstructionText = Strings.StatusDialogCancellingTitle;
                UpdateButtonEnabled( TaskDialogCommonButtonReturnId.Cancel, false );
                if ( _cts != null ) _cts.Cancel();
                e.Cancel = true; // Wait for StatusDialog_Opened() to time out.
            }
        }

        /// <summary>
        /// Handles the Opened event of the TaskDialog control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private async void StatusDialog_Opened( object sender, EventArgs e ) {
            // Finish drawing dialog.
            _isWorking = true;
            Application.DoEvents();

            // Do async work and catch errors/cancellation.
            try {
                Text = Strings.StatusDialogTextAuth;
                await Task.Delay( 700, _cts.Token ); // Wait for UI to catch up.
                await _api.AuthenticateAsync( _cts.Token ); // Login to API to validate user/token.
                ProgressBarValue = 10;

                Text = Strings.StatusDialogTextDomain;
                await _api.ValidateDomainAsync( _cts.Token ); // Check if user owns the domain.
                ProgressBarValue = 20;

                Text = Strings.StatusDialogTextLogout;
                await _api.LogoutAsync( _cts.Token );
                ProgressBarValue = _progressMax;
                await Task.Delay( 700, _cts.Token );
            } catch ( QueryAPIException err ) {
                lock ( _padlock ) {
                    Icon = TaskDialogStandardIcon.Error;
                    ProgressBarState = TaskDialogProgressBarState.Error;
                    InstructionText = Strings.StatusDialogHeadingError;
                    string text = String.Format( "Error {0}: {1}", err.Code.ToString(), err.RMessage );
                    if ( err.Details != null ) text += "\n\n" + err.Details;
                    if ( err.Url != null ) text += "\n\n" + String.Format( Strings.StatusDialogMoreInfo, err.Url );
                    Text = text;
                    _isWorking = false;
                }
                return;
            } catch ( OperationCanceledException ) {
                lock ( _padlock ) {
                    _isWorking = false;
                    Close( TaskDialogResult.Cancel );
                }
            }

            // No errors, this must mean validation was successful.
            lock ( _padlock ) {
                _isWorking = false;
                Close( TaskDialogResult.Ok );
            }
        }
    }
}