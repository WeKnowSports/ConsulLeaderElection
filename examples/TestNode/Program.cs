using System;
using System.Diagnostics;
using System.Threading;
using Consul;
using SBTech.Consul.LeaderElection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestNode
{
    class Program
    {
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            
            var consulClient = new ConsulClient();

            var config = ElectionMonitorConfig.Default(serviceName: "test_node", client: consulClient);

//            var config = new ElectionMonitorConfig(
//                // Parameters are described here https://github.com/PlayFab/consuldotnet                 
//                client: consulClient,
//                sessionOptions: new SessionEntry(), 
//                lockOptions: new KVPair("key"),
//
//                //How often to try to acquire a lock
//                tryAcquireLockInterval: TimeSpan.FromSeconds(1)
//            );

            var electionMonitor = new LeaderElectionMonitor(config);
            electionMonitor.LeaderChanged += (s, e) =>
            {
                if (e.IsLeader)
                    Console.WriteLine($"[Master] at {DateTime.Now.ToString("hh:mm:ss")}");
                else
                    Console.WriteLine($"[Slave] at {DateTime.Now.ToString("hh:mm:ss")}");
            };

            var joined = false;
            while (!joined)
            {
                Console.WriteLine($"TestNode is trying to join cluster at {DateTime.Now.ToString("hh:mm:ss")}");

                joined = electionMonitor.Start().Wait(timeout: TimeSpan.FromSeconds(30));

                if (joined)
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
            }

            Console.ReadLine();

            electionMonitor.Stop();
            Console.WriteLine($"TestNode stopped at {DateTime.Now.ToString("hh:mm:ss")}");
        }
    }
}
