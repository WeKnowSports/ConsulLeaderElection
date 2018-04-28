# Consul Leader Election

Simple library to use leader election functionality based on Consul. Fully written in F#.

## Leader election to do distributed locking
Leader election is really quite a simple concept. The service nodes register against a host using a specific common key. One of the nodes is elected leader and performs the job, while the other ones are idle. This lock to a specific node is held as long as the node's session remains in the host's store. When the node's session is gone, the leadership is open for taking by the next node that checks for it. Every time the nodes are scheduled to run their task, this check is performed. 

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
