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

            var joinedCluster = electionMonitor.Start().Wait(timeout: TimeSpan.FromSeconds(30));

            if (joinedCluster)
            {
                if (electionMonitor.IsLeader)
                    Console.WriteLine($"Joined cluster as [Master] at {DateTime.Now.ToString("hh:mm:ss")}");
                else
                    Console.WriteLine($"Joined cluster as [Slave] at {DateTime.Now.ToString("hh:mm:ss")}");
            }
            else
            {
                Console.WriteLine($"TestNode failed to join cluster at {DateTime.Now.ToString("hh:mm:ss")}");
            }

            Console.ReadLine();

            electionMonitor.Stop();
            Console.WriteLine($"TestNode stopped at {DateTime.Now.ToString("hh:mm:ss")}");
        }
    }
}
