﻿// ***********************************************************************
// Assembly         : UnofficialDDNS
// Author           : Robpol86
// Created          : 04-20-2013
//
// Last Modified By : Robpol86
// Last Modified On : 06-15-2013
// ***********************************************************************
// <copyright file="ProjectInstaller.cs" company="">
//      Copyright (c) 2013 All rights reserved.
//      This software is made available under the terms of the MIT License
//      that can be found in the LICENSE.txt file.
// </copyright>
// <summary>Installs the service.</summary>
// ***********************************************************************

using System.ComponentModel;
using System.Reflection;

namespace UnofficialDDNS {
    /// <summary>
    /// Installs the service.
    /// </summary>
    [System.ComponentModel.DesignerCategory( "Code" )]
    [RunInstaller( true )]
    public partial class ProjectInstaller : System.Configuration.Install.Installer {
        private System.ServiceProcess.ServiceProcessInstaller serviceProcessInstaller1;
        private System.ServiceProcess.ServiceInstaller serviceInstaller1;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectInstaller"/> class.
        /// </summary>
        public ProjectInstaller() {
            this.serviceProcessInstaller1 = new System.ServiceProcess.ServiceProcessInstaller();
            this.serviceInstaller1 = new System.ServiceProcess.ServiceInstaller();
            // 
            // serviceProcessInstaller1
            // 
            this.serviceProcessInstaller1.Account = System.ServiceProcess.ServiceAccount.LocalService;
            this.serviceProcessInstaller1.Password = null;
            this.serviceProcessInstaller1.Username = null;
            // 
            // serviceInstaller1
            // 
            this.serviceInstaller1.Description = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
            this.serviceInstaller1.ServiceName = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>().Product;
            this.serviceInstaller1.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange( new System.Configuration.Install.Installer[] {
                this.serviceProcessInstaller1,
                this.serviceInstaller1}
                );
        }
    }
}
