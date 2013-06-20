// ***********************************************************************
// Assembly         : UDDNSQuery
// Author           : Robpol86
// Created          : 04-22-2013
//
// Last Modified By : Robpol86
// Last Modified On : 06-15-2013
// ***********************************************************************
// <copyright file="CustomActions.cs" company="">
//      Copyright (c) 2013 All rights reserved.
//      This software is made available under the terms of the MIT License
//      that can be found in the LICENSE.txt file.
// </copyright>
// <summary></summary>
// ***********************************************************************

using Microsoft.Deployment.WindowsInstaller;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace UDDNSQuery {
    /// <summary>
    /// All CustomAction DLL entry points.
    /// </summary>
    public class CustomActions {
        /// <summary>
        /// Uses the Vista-style directory browse dialog.
        /// </summary>
        /// <param name="session">CustomAction session.</param>
        /// <returns>ActionResult.</returns>
        [CustomAction]
        public static ActionResult BrowseDirectoryButton( Session session ) {
            Thread thread = new Thread( (ThreadStart) delegate {
                using ( FolderBrowser2 dialog = new FolderBrowser2() ) {
                    dialog.DirectoryPath = session["INSTALLDIR"];
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

        /// <summary>
        /// Populates the registrar list.
        /// </summary>
        /// <param name="session">CustomAction session.</param>
        /// <returns>ActionResult.</returns>
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
            foreach ( string name in QueryAPIIndex.I.Registrars.Keys ) {
                i++;
                entry = String.Format( "{0} ({1})", name, QueryAPIIndex.I.Registrars[name] );
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

        /// <summary>
        /// TaskDialog-based cancel dialog.
        /// </summary>
        /// <param name="session">CustomAction session.</param>
        /// <returns>ActionResult.</returns>
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

        /// <summary>
        /// Validates the user's registrar credentials and displays the validation status in a TaskDialog.
        /// </summary>
        /// <param name="session">CustomAction session.</param>
        /// <returns>ActionResult.</returns>
        /// <exception cref="QueryAPIException" />
        [CustomAction]
        public static ActionResult ValidateCredentials( Session session ) {
            // Encrypt token.
            if ( session["REGISTRAR_TOKEN"].Length > 0 && !session["REGISTRAR_TOKEN"].StartsWith("ENCRYPTED:") ) {
                session["REGISTRAR_TOKEN"] = "ENCRYPTED:" + Convert.ToBase64String( ProtectedData.Protect(
                    Encoding.ASCII.GetBytes( session["REGISTRAR_TOKEN"] ),
                    null, DataProtectionScope.LocalMachine
                    ) );
            }

            // Validate dialog fields.
            try {
                if ( session["REGISTRAR_REGISTRAR"].Length == 0 ) throw new QueryAPIException( 103 );
                using ( IQueryAPI api = QueryAPIIndex.I.Factory( session["REGISTRAR_REGISTRAR"] ) ) {
                    // Testing for Error98 in the above using statement.
                    api.Credentials( session["REGISTRAR_USER"], session["REGISTRAR_TOKEN"].Replace( "ENCRYPTED:", "" ),
                        session["REGISTRAR_DOMAIN"]
                        );
                    if ( api.UserLength == 0 ) throw new QueryAPIException( 100 );
                    if ( api.TokenLength == 0 ) throw new QueryAPIException( 101 );
                    if ( api.DomainLength == 0 ) throw new QueryAPIException( 102 );
                }
            } catch ( QueryAPIException err ) {
                using ( TaskDialog dialog = new TaskDialog() ) {
                    // Launch the dialog and get result.
                    Thread thread = new Thread( (ThreadStart) delegate { dialog.ShowError( Strings.ErrorDialogTitle, err.RMessage + "\n" ); } );
                    thread.SetApartmentState( ApartmentState.STA );
                    thread.Start();
                    thread.Join();
                    session["_RegistrarValidated"] = "0";
                    return ActionResult.NotExecuted;
                }
            }

            // Validate with status dialog.
            using ( IQueryAPI api = QueryAPIIndex.I.Factory( session["REGISTRAR_REGISTRAR"] ) )
            using ( StatusDialog dialog = new StatusDialog( api ) ) {
                // Pass credentials to class instance.
                api.Credentials( session["REGISTRAR_USER"], session["REGISTRAR_TOKEN"].Replace( "ENCRYPTED:", "" ),
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
