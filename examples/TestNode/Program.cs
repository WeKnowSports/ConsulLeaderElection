using System;
using Consul;
using SBTech.Consul.LeaderElection;

namespace TestNode
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("TestNode is joining...");

            var consulClient = new ConsulClient(); // using http://localhost:8500

            var config = ElectionMonitorConfig.Default(serviceName: "test_node", client: consulClient);

            //var config = new ElectionMonitorConfig(
            //    lockOptions: new LockOptions(key: "services/MyServiceName/leader")
            //    {
            //        SessionTTL = TimeSpan.FromSeconds(20),
            //        SessionName = "lock session for 'MyServiceName'"                    
            //    },
            //    tryLockInterval: TimeSpan.FromSeconds(5),                
            //    client: consulClient
            //);

            var electionMonitor = new LeaderElectionMonitor(config);
            electionMonitor.LeaderChanged += (s, e) =>
            {
                if (e.IsLeader)
                    Console.WriteLine($"[Master] at {DateTime.Now.ToString("hh:mm:ss")}");
                else
                    Console.WriteLine($"[Slave] at {DateTime.Now.ToString("hh:mm:ss")}");
            };
            electionMonitor.Start();

            Console.WriteLine($"[Slave] at {DateTime.Now.ToString("hh:mm:ss")}");

            Console.ReadLine();

            electionMonitor.Stop();
            Console.WriteLine($"TestNode stopped at {DateTime.Now.ToString("hh:mm:ss")}");
        }
    }
}
