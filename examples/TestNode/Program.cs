using System;
using Consul;
using SBTech.Consul.LeaderElection;

namespace TestNode
{
    class Program
    {
        static void Main(string[] args)
        {
            var consulClient = new ConsulClient(); // using http://localhost:8500

            var config = ElectionMonitorConfig.Default(serviceName: "test_node", client: consulClient);

            //var config = new ElectionMonitorConfig(
            //    client: consulClient,
            //    lockOptions: new LockOptions(key: "services/MyServiceName/leader")
            //    {
            //        SessionName = "lock session for 'MyServiceName'",

            //        // specifies the number of seconds (between 10s and 86400s).
            //        // if provided, the session is invalidated if it is not renewed before the TTL expires.
            //        // The lowest practical TTL should be used to keep the number of managed sessions low.
            //        SessionTTL = TimeSpan.FromSeconds(10), // min is 10 sec

            //        // is how long we block for at a time to check if lock acquisition is possible.
            //        // This affects the minimum time it takes to cancel a Lock acquisition.
            //        LockWaitTime = TimeSpan.FromSeconds(5), // default is 5 sec

            //        // is how long we wait after a failed lock acquisition before attempting
            //        // to do the lock again. This is so that once a lock-delay is in effect, we do not hot loop
            //        // retrying the acquisition.
            //        LockRetryTime = TimeSpan.FromSeconds(5), // default is 5 sec

            //        // is allowing to try acquire lock only one time in one round
            //        // basically if LockTryOnce == false then 
            //        // electionMonitor.Start() will not exit until you acquire the lock                    
            //        LockTryOnce = false // default is true
            //    }                
            //);

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
