// ***********************************************************************
// Assembly         : UnofficialDDNS
// Author           : Robpol86
// Created          : 04-20-2013
//
// Last Modified By : Robpol86
// Last Modified On : 06-15-2013
// ***********************************************************************
// <copyright file="Program.cs" company="">
//      Copyright (c) 2013 All rights reserved.
//      This software is made available under the terms of the MIT License
//      that can be found in the LICENSE.txt file.
// </copyright>
// <summary>The main entry point for the application.</summary>
// ***********************************************************************

using System.ServiceProcess;

namespace UnofficialDDNS {
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    static class Program {
        /// <summary>
        /// Creates a new instance of <see cref="UnofficialDDNS" /> and passes it to the Service Control Manager.
        /// </summary>
        static void Main() {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
            { 
                new UnofficialDDNS() 
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
