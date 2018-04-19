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
            //    serviceName: "test_node",
            //    tryLockInterval: TimeSpan.FromSeconds(5),
            //    sessionTTL: TimeSpan.FromSeconds(20),
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
