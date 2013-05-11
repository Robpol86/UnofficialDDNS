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
     *      http://www.codeproject.com/Articles/17026/TaskDialog-for-WinForms
     *      http://social.msdn.microsoft.com/Forums/en-US/winformssetup/thread/bbe69f12-8908-4c65-aa89-1963720d4c11
     *      http://stackoverflow.com/questions/16020136/taskdialog-fires-exception-comctl32-dll-in-version-6-required/16494194#16494194
     * </references>
     * <todo>
     *      * Destroy method.
     *      * In DoWorkDialog throw exception when canceled, set oEvent.Cancel = true, % to 0, return.
     *      * Migrate to TPL.
     *      * Raise exception in worker thread instead of waiting to check if user cancled.
     *      * http://www.componentone.com/newimages/Products/ScreenShots/StudioWinForms/C1Win7Pack/Win7Pack_C1TaskDialog3.png
     *      * http://www.codeguru.com/csharp/article.php/c18885/Understanding-NET-Framework-Task-Parallel-Library-Cancellations.htm
     *      * http://blogs.msdn.com/b/dotnet/archive/2012/06/06/async-in-4-5-enabling-progress-and-cancellation-in-async-apis.aspx
     *      * http://code.msdn.microsoft.com/NET-Asynchronous-9c582d61/sourcecode?fileId=71412&pathId=1294572038
     *      
     *      * USE TaskDialog.cs instead of this, build and trim code. When its small enough integrate it here.
     * </todo>
     */
    public enum TaskDialogResult { OK, Cancel }
    public enum TaskDialogShowState { PreShow, Showing, Closing, Closed }

    public class TaskDialogClosingEventArgs : CancelEventArgs {
        private TaskDialogResult m_oDialogResult;
        public TaskDialogResult TaskDialogResult { get { return m_oDialogResult; } set { m_oDialogResult = value; } }
    }

    /// <summary>
    /// Opens a dialog showing the status of validation using the new-to-Vista TaskDialog.
    /// </summary>
    public class StatusDialog : IDisposable {
        private IQueryAPI m_oApi; // API object.
        private CancellationTokenSource m_oCTS; // Cancellation for all API tasks.
        private string m_oDialogTitle;
        private string m_oDialogHeading;
        private string m_oDialogText;
        
        public StatusDialog( IQueryAPI oApi ) {
            // Initialize.
            this.m_oApi = oApi;
            this.m_oCTS = new CancellationTokenSource();
            this.m_oDialogTitle = Strings.StatusDialogTitle;
            this.m_oDialogHeading = "Please wait while your settings are validated"; // TODO
            this.m_oDialogText = "Validation is done."; // TODO.
        }

        [DllImport( "comctl32.dll", CharSet = CharSet.Unicode )]
        public static extern int TaskDialog( IntPtr hWndParent, IntPtr hInstance, string pszWindowTitle,
            string pszMainInstruction, string pszContent, int dwCommonButtons, IntPtr pszIzon, out int pnButton );

        public TaskDialogResult Show() {
            // TODO Temporary code.
            this.StatusDialog_Shown();
            // TODO Temporary code done.
            int p;
            using ( new EnableThemingInScope( true ) ) {
                TaskDialog( IntPtr.Zero, IntPtr.Zero, this.m_oDialogTitle, this.m_oDialogHeading, this.m_oDialogText,
                    8, IntPtr.Zero, out p );
            }
            return p == 1 ? TaskDialogResult.OK : TaskDialogResult.Cancel;
        }

        public void Dispose() { }

        private async void StatusDialog_Shown() {
            // Finish drawing dialog.
            Application.DoEvents();

            // Do async work and catch errors/cancelation.
            try {
                if ( this.m_oApi.UserLength == 0 ) throw new QueryAPIException( 100 );
                if ( this.m_oApi.TokenLength == 0 ) throw new QueryAPIException( 101 );
                if ( this.m_oApi.DomainLength == 0 ) throw new QueryAPIException( 102 );

                this.m_oDialogText = "Authenticating..."; // TODO
                await this.m_oApi.AuthenticateAsync( m_oCTS.Token ); // Login to API to validate user/token.

                this.m_oDialogText = "Validating domain...";
                await this.m_oApi.ValidateDomainAsync( m_oCTS.Token ); // Check if user owns the domain.

                this.m_oDialogText = "Geting records...";
                await this.m_oApi.GetRecordsAsync( m_oCTS.Token ); // Make sure domain doesn't have anything other than 0 or 1 A record.

                this.m_oDialogText = "Logging ou...";
                await this.m_oApi.LogoutAsync( m_oCTS.Token );
            } catch ( QueryAPIException e ) {
                //this.ProgressBarState = ProgressBarState.Error;
                //this.WindowTitle = Strings.ValidationFailedTitle;
                this.m_oDialogText = e.Code.ToString() + ": " + e.RMessage;
                if ( e.Details != null ) this.m_oDialogText += "\n" + e.Details;
                return;
            } catch ( OperationCanceledException ) {
                // Nothing.
            }

            // No errors, this must mean validation was successfull.
            //this.DialogResult = DialogResult.OK;
            //this.Close();
            //this.Dispose();
        }
    }

        /*

        private TaskDialogButton m_oCancelButton;

        
            this.m_oCancelButton = new TaskDialogButton( ButtonType.Cancel );

            // Setup dialog.
            this.AllowDialogCancellation = true;
            this.Buttons.Add( this.m_oCancelButton );
            this.MinimizeBox = false;
            this.Width = 350;
            this.WindowTitle = Strings.StatusDialogTitle;

            // Progress bar.
            this.ProgressBarStyle = Ookii.Dialogs.Wpf.ProgressBarStyle.MarqueeProgressBar;
            this.ProgressBarMarqueeAnimationSpeed = 100;
            this.ProgressBarState = ProgressBarState.Normal;
            
            // Dialog events.
            this.Created += new EventHandler( this.StatusDialog_Shown ); // Automatically query when dialog is shown.
            this.Disposed += new EventHandler( ( obj, e ) => { if ( m_oCTS != null ) m_oCTS.Cancel(); } );
            //this.ButtonClicked += new EventHandler<TaskDialogItemClickedEventArgs>( this.CancelButton );
        }

        
        /*
        private void CancelButton( object oSender, TaskDialogItemClickedEventArgs oEvent ) {
            if ( m_oCTS != null ) m_oCTS.Cancel();
        }* /
    } ************************************/
}
