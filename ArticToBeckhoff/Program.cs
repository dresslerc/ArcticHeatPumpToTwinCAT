using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace ArticToBeckhoff
{
    internal class Program
    {
        #region Nested classes to support running as service
        public const string ServiceName = "ArticToBeckhoff";

        public class Service : ServiceBase
        {
            public Service()
            {
                ServiceName = Program.ServiceName;
            }

            protected override void OnStart(string[] args)
            {
                Program.Start(args);
            }

            protected override void OnStop()
            {
                Program.Stop();
            }
        }
        #endregion

        private static Core service = new Core();

        static void Main(string[] args)
        {

            if (!Environment.UserInteractive)
                // running as service
                using (var service = new Service())
                    ServiceBase.Run(service);
            else
            {
                // running as console app
                Start(args);

                Console.WriteLine("Press any key to stop...");
                Console.ReadKey(true);

                Stop();
            } 
        }

        private static void Start(string[] args)
        {

            var thread = new Thread(() =>
            {

                service.Start();

            });

            thread.Start(); 
        }

        private static void Stop()
        {
            service.Stop();

        } 
    }
}
