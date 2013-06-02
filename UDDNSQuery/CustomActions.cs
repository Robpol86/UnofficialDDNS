using System;
using System.Text;
using Microsoft.Deployment.WindowsInstaller;
using System.Threading;
using System.IO;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace UDDNSQuery {
    public class CustomActions {
        [CustomAction]
        public static ActionResult BrowseDirectoryButton( Session session ) {
            Thread thread = new Thread( (ThreadStart) delegate {
                using ( FolderBrowser2 dialog = new FolderBrowser2() ) {
                    dialog.DirectoryPath = session["INSTALLDIR"];
                    /*while ( !Directory.Exists( oDialog.DirectoryPath ) ) {
                        try {
                            oDialog.DirectoryPath = Path.GetDirectoryName( oDialog.DirectoryPath );
                        } catch ( System.ArgumentException ) {
                            oDialog.DirectoryPath = null;
                            break;
                        }
                    }*/
                    if ( dialog.ShowDialog() == DialogResult.OK ) {
                        session["INSTALLDIR"] =
                            Path.Combine( dialog.DirectoryPath, "UnofficialDDNS" ) + Path.DirectorySeparatorChar;
                    }
                }
            } );
            thread.SetApartmentState( ApartmentState.STA );
            thread.Start();
            thread.Join();

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult PopulateRegistrarList( Session session ) {
            string wixProperty = "REGISTRAR_REGISTRAR";
            string logPrefix = "UDDNSQuery.PopulateRegistrarList: ";
            session.Log( logPrefix + "Method begin." );
            
            // Nuke the combobox and initialize the View.
            Microsoft.Deployment.WindowsInstaller.View comboBoxView = session.Database.OpenView(
                "DELETE FROM ComboBox WHERE ComboBox.Property = '{0}'",
                new string[] { wixProperty, }
                );
            comboBoxView.Execute();
            comboBoxView = session.Database.OpenView( "SELECT * FROM ComboBox" );
            comboBoxView.Execute();
            session.Log( logPrefix + String.Format( "ComboBox {0} purged.", wixProperty ) );

            // Populate the combobox. http://msdn.microsoft.com/en-us/library/windows/desktop/aa367872(v=vs.85).aspx
            int i = 0;
            Record comboBoxItem;
            string entry;
            foreach ( string name in QueryAPIIndex.Instance.Registrars.Keys ) {
                i++;
                entry = String.Format( "{0} ({1})", name, QueryAPIIndex.Instance.Registrars[name] );
                comboBoxItem = session.Database.CreateRecord( 4 );
                comboBoxItem.SetString( 1, wixProperty ); // Property name.
                comboBoxItem.SetInteger( 2, i ); // Order.
                comboBoxItem.SetString( 3, name ); // Value of item.
                comboBoxItem.SetString( 4, entry ); // Text to represent item.
                comboBoxView.Modify( ViewModifyMode.InsertTemporary, comboBoxItem );
                session.Log( logPrefix + String.Format( "ComboBox {0} new entry: {1}", wixProperty, entry ) );
            }

            session.Log( logPrefix + "Method end." );
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult CancelDialog( Session session ) {
            using ( TaskDialog dialog = new TaskDialog() ) {
                // Launch the dialog and get result.
                Thread thread = new Thread( (ThreadStart) delegate { dialog.ShowCancellation( session["_CancelDlgTitle"], session["_CancelDlgText"] ); } );
                thread.SetApartmentState( ApartmentState.STA );
                thread.Start();
                thread.Join();
                if ( dialog.Result == TaskDialog.TaskDialogResult.Yes ) session["_UserWantsOut"] = "1";
                else session["_UserWantsOut"] = "0";
                return ActionResult.UserExit;
            }
        }

        [CustomAction]
        public static ActionResult ValidateCredentials( Session session ) {
            // Encrypt token.
            if ( session["REGISTRAR_TOKEN"].Length > 0 ) {
                session["REGISTRAR_TOKEN_ENCRYPTED"] = Convert.ToBase64String( ProtectedData.Protect(
                    Encoding.ASCII.GetBytes( session["REGISTRAR_TOKEN"] ),
                    null, DataProtectionScope.LocalMachine
                    ) );
            } else {
                session["REGISTRAR_TOKEN_ENCRYPTED"] = "";
            }

            // Validate with status dialog.
            using ( IQueryAPI api = QueryAPIIndex.Instance.Factory( session["REGISTRAR_REGISTRAR"] ) )
            using ( StatusDialog dialog = new StatusDialog( api ) ) {
                // Pass credentials to class instance.
                api.Credentials( session["REGISTRAR_USER"], session["REGISTRAR_TOKEN_ENCRYPTED"],
                    session["REGISTRAR_DOMAIN"]
                    );

                // Launch the dialog and get result.
                Thread thread = new Thread( (ThreadStart) delegate { dialog.Show(); } );
                thread.SetApartmentState( ApartmentState.STA );
                thread.Start();
                thread.Join();
                if ( dialog.Result == TaskDialog.TaskDialogResult.Ok ) {
                    session["_RegistrarValidated"] = "1";
                    return ActionResult.Success;
                } else {
                    session["_RegistrarValidated"] = "0";
                    return ActionResult.NotExecuted;
                }
            }
        }
    }
}
