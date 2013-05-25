using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
        
        public StatusDialog( IQueryAPI api ) {
            // Initialize.
            _api = api;
            _cts = new CancellationTokenSource();
            _progressMax = 40;

            // Setup dialog/progressbar.
            Caption = Strings.StatusDialogTitle;
            InstructionText = Strings.StatusDialogHeading;
            ProgressBarRange = new int[2] { 0, _progressMax };
            ProgressBarValue = 1;
            ProgressBarState = TaskDialogProgressBarState.Normal;
            
            // Hook events.
            Closing += new EventHandler<CancelEventArgs>( StatusDialog_Canceled );
            Opened += new EventHandler( StatusDialog_Opened );
        }

        public new void Dispose() {
            _cts.Dispose();
            base.Dispose();
            GC.SuppressFinalize( this );
        }

        private void StatusDialog_Canceled( object sender, EventArgs e ) {
            if ( _cts != null ) _cts.Cancel();
        }

        private async void StatusDialog_Opened( object sender, EventArgs e ) {
            // Finish drawing dialog.
            Application.DoEvents();

            // Do async work and catch errors/cancelation.
            try {
                if ( _api.UserLength == 0 ) {
                    await Task.Delay( 700, _cts.Token ); throw new QueryAPIException( 100 );
                }
                if ( _api.TokenLength == 0 ) {
                    await Task.Delay( 700, _cts.Token ); throw new QueryAPIException( 101 );
                }
                if ( _api.DomainLength == 0 ) {
                    await Task.Delay( 700, _cts.Token ); throw new QueryAPIException( 102 );
                }

                Text = Strings.StatusDialogTextAuth;
                await Task.Delay( 700, _cts.Token ); // Wait for UI to catch up.
                await _api.AuthenticateAsync( _cts.Token ); // Login to API to validate user/token.
                ProgressBarValue = 10;

                Text = Strings.StatusDialogTextDomain;
                await _api.ValidateDomainAsync( _cts.Token ); // Check if user owns the domain.
                ProgressBarValue = 20;

                Text = Strings.StatusDialogTextRecords;
                await _api.GetRecordsAsync( _cts.Token ); // Make sure domain doesn't have anything other than 0 or 1 A record.
                ProgressBarValue = 30;

                Text = Strings.StatusDialogTextLogout;
                await _api.LogoutAsync( _cts.Token );
                ProgressBarValue = _progressMax;
                await Task.Delay( 700, _cts.Token );
            } catch ( QueryAPIException err ) {
                ProgressBarState = TaskDialogProgressBarState.Error;
                InstructionText = Strings.StatusDialogHeadingError;
                Text = err.Code.ToString() + ": " + err.RMessage;
                if ( err.Details != null ) Text += "\n" + err.Details;
                return;
            } catch ( OperationCanceledException ) {
                // Nothing.
            }

            // No errors, this must mean validation was successfull.
            Close( TaskDialogResult.Ok );
        }
    }
}