using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Deployment.WindowsInstaller;
using System.Threading;
using System.IO;

namespace UDDNSQuery {
    public class CustomActions {
        [CustomAction]
        public static ActionResult BrowseDirectoryButton( Session oSession ) {
            Thread oThread = new Thread( (ThreadStart) delegate {
                using ( var oDialog = new System.Windows.Forms.FolderBrowserDialog() ) {
                    oDialog.ShowNewFolderButton = true;
                    oDialog.SelectedPath = oSession["INSTALLDIR"];
                    while ( !Directory.Exists( oDialog.SelectedPath ) ) {
                        try {
                            oDialog.SelectedPath = Path.GetDirectoryName( oDialog.SelectedPath );
                        } catch ( System.ArgumentException ) {
                            oDialog.SelectedPath = null;
                            break;
                        }
                    }
                    oDialog.Description = "Directory 'UnofficialDDNS' will be appended to the selected path.";
                    if ( oDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ) {
                        oSession["INSTALLDIR"] = 
                            Path.Combine( oDialog.SelectedPath, "UnofficialDDNS" ) + Path.DirectorySeparatorChar;
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
            View oComboBoxView = oSession.Database.OpenView( "DELETE FROM ComboBox WHERE ComboBox.Property = '{0}'",
                new string[] { sWixProperty, } );
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
                oComboBoxItem.SetString( 1, "RegistrarRegistrar" ); // Property name.
                oComboBoxItem.SetInteger( 2, iCounter ); // Order.
                oComboBoxItem.SetString( 3, sName ); // Value of item.
                oComboBoxItem.SetString( 4, sEntry ); // Text to represent item.
                oComboBoxView.Modify( ViewModifyMode.InsertTemporary, oComboBoxItem );
                oSession.Log( sLogPrefix + String.Format( "ComboBox {0} new entry: {1}", sWixProperty, sEntry ) );
            }

            oSession.Log( sLogPrefix + "Method end." );
            return ActionResult.Success;
        }
    }
}
