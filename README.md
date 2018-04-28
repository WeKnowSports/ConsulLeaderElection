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
electionMonitor.Start();
```
