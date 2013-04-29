using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Deployment.WindowsInstaller;
using System.Threading;
using System.IO;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace UDDNSQuery {
    public class CustomActions {
        [CustomAction]
        public static ActionResult BrowseDirectoryButton( Session oSession ) {
            Thread oThread = new Thread( (ThreadStart) delegate {
                using ( FolderBrowser2 oDialog = new FolderBrowser2() ) {
                    oDialog.DirectoryPath = oSession["INSTALLDIR"];
                    /*while ( !Directory.Exists( oDialog.DirectoryPath ) ) {
                        try {
                            oDialog.DirectoryPath = Path.GetDirectoryName( oDialog.DirectoryPath );
                        } catch ( System.ArgumentException ) {
                            oDialog.DirectoryPath = null;
                            break;
                        }
                    }*/
                    if ( oDialog.ShowDialog( null ) == DialogResult.OK ) {
                        oSession["INSTALLDIR"] =
                            Path.Combine( oDialog.DirectoryPath, "UnofficialDDNS" ) + Path.DirectorySeparatorChar;
                    }
                }
            } );
            oThread.SetApartmentState( ApartmentState.STA );
            oThread.Start();
            oThread.Join();

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult PopulateRegistrarList( Session oSession ) {
            string sWixProperty = "RegistrarRegistrar";
            string sLogPrefix = "UDDNSQuery.PopulateRegistrarList: ";
            oSession.Log( sLogPrefix + "Method begin." );
            
            // Nuke the combobox and initialize the View.
            Microsoft.Deployment.WindowsInstaller.View oComboBoxView = oSession.Database.OpenView(
                "DELETE FROM ComboBox WHERE ComboBox.Property = '{0}'",
                new string[] { sWixProperty, }
                );
            oComboBoxView.Execute();
            oComboBoxView = oSession.Database.OpenView( "SELECT * FROM ComboBox" );
            oComboBoxView.Execute();
            oSession.Log( sLogPrefix + String.Format( "ComboBox {0} purged.", sWixProperty ) );

            // Populate the combobox. http://msdn.microsoft.com/en-us/library/windows/desktop/aa367872(v=vs.85).aspx
            int iCounter = 0;
            Record oComboBoxItem;
            string sEntry;
            foreach ( string sName in QueryAPIIndex.Instance.RegistrarList.Keys ) {
                iCounter++;
                sEntry = String.Format( "{0} ({1})", sName, QueryAPIIndex.Instance.RegistrarList[sName] );
                oComboBoxItem = oSession.Database.CreateRecord( 4 );
                oComboBoxItem.SetString( 1, sWixProperty ); // Property name.
                oComboBoxItem.SetInteger( 2, iCounter ); // Order.
                oComboBoxItem.SetString( 3, sName ); // Value of item.
                oComboBoxItem.SetString( 4, sEntry ); // Text to represent item.
                oComboBoxView.Modify( ViewModifyMode.InsertTemporary, oComboBoxItem );
                oSession.Log( sLogPrefix + String.Format( "ComboBox {0} new entry: {1}", sWixProperty, sEntry ) );
            }

            oSession.Log( sLogPrefix + "Method end." );
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult ValidateCredentials( Session oSession ) {
            // Check for empty text fields.
            string sError = null;
            if ( oSession["RegistrarDomain"].Length == 0 ) sError = Errors.Error102;
            if ( oSession["RegistrarToken"].Length == 0 ) sError = Errors.Error101;
            if ( oSession["RegistrarUser"].Length == 0 ) sError = Errors.Error100;
            if ( sError != null ) {
                Thread oThread = new Thread( (ThreadStart) delegate {
                    MessageBox.Show( sError, Strings.EmptyFieldTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1
                        );
                } );
                oThread.SetApartmentState( ApartmentState.STA );
                oThread.Start();
                oThread.Join();
                return ActionResult.NotExecuted;
            }

            // Encrypt token.
            oSession["RegistrarTokenEncrypted"] = Convert.ToBase64String ( ProtectedData.Protect(
                Encoding.ASCII.GetBytes( oSession["RegistrarToken"] ),
                null, DataProtectionScope.LocalMachine
                ) );

            // Validate.
            using ( IQueryAPI oApi = QueryAPIIndex.Instance.Factory( oSession["RegistrarRegistrar"] ) ) {
                try {
                    oApi.Credentials( oSession["RegistrarUser"], oSession["RegistrarTokenEncrypted"], 
                        oSession["RegistrarDomain"]
                        ); // Pass credentials to class instance.
                    oApi.Authenticate(); // Login to API to validate user/token.
                    oApi.ValidateDomain(); // Check if user owns the domain.
                    oApi.GetRecords(); // Make sure domain doesn't have anything other than 0 or 1 A record.
                    oApi.Logout();
                } catch ( QueryAPIException e ) {
                    Thread oThread = new Thread( (ThreadStart) delegate {
                        string sMessage = e.Code.ToString() + ": " + e.RMessage;
                        if ( e.Details != null ) sMessage += "\n" + e.Details;
                        MessageBox.Show( sMessage, Strings.ValidationFailedTitle,
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1
                            );
                    } );
                    oThread.SetApartmentState( ApartmentState.STA );
                    oThread.Start();
                    oThread.Join();
                    return ActionResult.NotExecuted;
                }
            }
            
            // Success!
            oSession["_RegistrarValidated"] = "1";
            return ActionResult.Success;
        }
    }
}
