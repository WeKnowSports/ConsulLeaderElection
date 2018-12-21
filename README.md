# Consul Leader Election

Simple library(100% written in F#) to use leader election functionality based on Consul.

## Leader election to do distributed locking
Leader election is quite a simple concept. The service nodes are registered against a host using a specific common key. Usually key format is "services/Your_Service_Name/leader". One of the nodes is elected as a leader and performs the job, while the other ones are being idle. This lock to a specific node is held as long as the session of the node remains on the host. When the session is gone, the leadership is open to be taken by the next node that checks for it. 

## How to install
To install Consul Leader Election via NuGet, run this command in NuGet package manager console:
```code
PM> Install-Package SBTech.Consul.LeaderElection
```

## Example
Full example you can find by this [link](https://github.com/WeKnowSports/ConsulLeaderElection/blob/master/examples/TestNode/Program.cs).

```csharp
var consulClient = new ConsulClient(); // using http://localhost:8500
var config = ElectionMonitorConfig.Default(serviceName: "test_node", client: consulClient);

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

electionMonitor.Start();
```

## Configuration

```csharp
var consulClient = new ConsulClient(); // using http://localhost:8500
var config = new ElectionMonitorConfig(
    client: consulClient,
    lockOptions: new LockOptions(key: "services/MyServiceName/leader")
    {
        SessionName = "lock session for 'MyServiceName'",

        // specifies the number of seconds (between 10s and 86400s).
        // if provided, the session is invalidated if it is not renewed before the TTL expires.
        // The lowest practical TTL should be used to keep the number of managed sessions low.
        SessionTTL = TimeSpan.FromSeconds(10), // min is 10 sec

        // is how long we block for at a time to check if lock acquisition is possible.
        // This affects the minimum time it takes to cancel a Lock acquisition.
        LockWaitTime = TimeSpan.FromSeconds(5), // default is 5 sec

        // is how long we wait after a failed lock acquisition before attempting
        // to do the lock again. This is so that once a lock-delay is in effect, we do not hot loop
        // retrying the acquisition.
        LockRetryTime = TimeSpan.FromSeconds(5), // default is 5 sec

        // is allowing to try acquire lock only one time in one round
        // basically, if LockTryOnce == false then 
        // electionMonitor.Start() will not exit until you acquire the lock                    
        LockTryOnce = true // default is true
    }                
);
```

