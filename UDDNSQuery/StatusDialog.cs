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
        private IQueryAPI m_oApi; // API object.
        private CancellationTokenSource m_oCTS; // Cancellation for all API tasks.
        private int m_iProgressMax; // The maximum value for progress bars.
        
        public StatusDialog( IQueryAPI oApi ) {
            // Initialize.
            this.m_oApi = oApi;
            this.m_oCTS = new CancellationTokenSource();
            this.m_iProgressMax = 40;

            // Setup dialog/progressbar.
            this.Caption = Strings.StatusDialogTitle;
            this.InstructionText = Strings.StatusDialogHeading;
            this.ProgressBarRange = new int[2] { 0, this.m_iProgressMax };
            this.ProgressBarValue = 1;
            this.ProgressBarState = TaskDialogProgressBarState.Normal;
            
            // Hook events.
            this.Closing += new EventHandler<CancelEventArgs>( this.StatusDialog_Canceled );
            this.Opened += new EventHandler( this.StatusDialog_Shown );
        }

        public new void Dispose() {
            this.m_oCTS.Dispose();
            base.Dispose();
        }

        private void StatusDialog_Canceled( object oSender, EventArgs oEvent ) {
            if ( this.m_oCTS != null ) this.m_oCTS.Cancel();
        }

        private async void StatusDialog_Shown( object oSender, EventArgs oEvent ) {
            // Finish drawing dialog.
            Application.DoEvents();

            // Do async work and catch errors/cancelation.
            try {
                if ( this.m_oApi.UserLength == 0 ) {
                    await Task.Delay( 700, this.m_oCTS.Token ); throw new QueryAPIException( 100 );
                }
                if ( this.m_oApi.TokenLength == 0 ) {
                    await Task.Delay( 700, this.m_oCTS.Token ); throw new QueryAPIException( 101 );
                }
                if ( this.m_oApi.DomainLength == 0 ) {
                    await Task.Delay( 700, this.m_oCTS.Token ); throw new QueryAPIException( 102 );
                }

                this.Text = Strings.StatusDialogTextAuth;
                await Task.Delay( 700, this.m_oCTS.Token ); // Wait for UI to catch up.
                await this.m_oApi.AuthenticateAsync( m_oCTS.Token ); // Login to API to validate user/token.
                this.ProgressBarValue = 10;

                this.Text = Strings.StatusDialogTextDomain;
                await this.m_oApi.ValidateDomainAsync( m_oCTS.Token ); // Check if user owns the domain.
                this.ProgressBarValue = 20;

                this.Text = Strings.StatusDialogTextRecords;
                await this.m_oApi.GetRecordsAsync( m_oCTS.Token ); // Make sure domain doesn't have anything other than 0 or 1 A record.
                this.ProgressBarValue = 30;

                this.Text = Strings.StatusDialogTextLogout;
                await this.m_oApi.LogoutAsync( m_oCTS.Token );
                this.ProgressBarValue = this.m_iProgressMax;
                await Task.Delay( 700, this.m_oCTS.Token );
            } catch ( QueryAPIException e ) {
                this.ProgressBarState = TaskDialogProgressBarState.Error;
                this.InstructionText = Strings.StatusDialogHeadingError;
                this.Text = e.Code.ToString() + ": " + e.RMessage;
                if ( e.Details != null ) this.Text += "\n" + e.Details;
                return;
            } catch ( OperationCanceledException ) {
                // Nothing.
            }

            // No errors, this must mean validation was successfull.
            this.Close( TaskDialogResult.Ok );
        }
    }
}