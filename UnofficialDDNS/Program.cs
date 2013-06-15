using System.ServiceProcess;

namespace UnofficialDDNS {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
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
