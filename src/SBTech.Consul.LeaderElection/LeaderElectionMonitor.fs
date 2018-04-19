namespace rec SBTech.Consul.LeaderElection

open System
open System.Timers
open Consul

type ElectionMonitorConfig = {
    ServiceName: string
    TryLockInterval: TimeSpan
    SessionTTL: TimeSpan
    Client: ConsulClient
}
  with
  static member Default(serviceName, client) =
      { ServiceName     = serviceName
        TryLockInterval = TimeSpan.FromSeconds(3.)
        SessionTTL      = TimeSpan.FromSeconds(10.)
        Client          = client }

type ElectionArgs = { IsLeader: bool }

type LeaderElectionMonitor(config) as x =
    
    let mutable isLeader = false
    let mutable currentLock = Option<IDistributedLock>.None
    let lockOptions   = LockOptions(key = sprintf "services/%s/leader" config.ServiceName)
    let leaderChanged = Event<ElectionArgs>()
    let tryLockTimer  = new Timer(config.TryLockInterval.TotalMilliseconds)

    do lockOptions.SessionName  <- "lock session for " + config.ServiceName
       lockOptions.SessionTTL   <- config.SessionTTL              
       lockOptions.LockWaitTime <- TimeSpan.FromSeconds(1.)
       lockOptions.LockTryOnce  <- true
       tryLockTimer.Elapsed.Add(fun _ -> x.TryLock() |> Async.RunSynchronously)

    [<CLIEvent>]
    member x.LeaderChanged = leaderChanged.Publish    
    member x.IsLeader = isLeader

    member x.Start() = if not tryLockTimer.Enabled then tryLockTimer.Start()
    
    member x.Stop() =
        try
            tryLockTimer.Stop()
            if currentLock.IsSome 
            then currentLock.Value.Release().Wait()
                 currentLock.Value.Destroy().Wait()
        with
        | ex -> ex.ToString() |> System.Diagnostics.Trace.TraceError
        
    member private x.TryLock() = async {
    
        let tryCreateAndAcquireLock () = config.Client.AcquireLock(lockOptions) 
                                         |> Async.AwaitTask        

        let tryAcquireLock (lock: IDistributedLock) = lock.Acquire()
                                                      |> Async.AwaitTask 
                                                      |> Async.Ignore                

        let checkIfLeaderChanged (lock: IDistributedLock) =
            if lock.IsHeld <> isLeader 
            then isLeader <- lock.IsHeld
                 leaderChanged.Trigger({ IsLeader = isLeader })
        
        let handleException (ex: AggregateException) =
            match ex.InnerException with            
            | :? LockMaxAttemptsReachedException -> () // this is expected if it couldn't acquire the lock within the first attempt.
            | ex -> ex.ToString() |> System.Diagnostics.Trace.TraceError

        try
            match currentLock with
            | Some lock -> if lock.IsHeld <> true 
                           then do! tryAcquireLock(lock)
                                 
            | None      -> let! lock = tryCreateAndAcquireLock()
                           currentLock <- Option.ofObj(lock)
        with
        | :? AggregateException as ex -> handleException(ex)
        
        if currentLock.IsSome then checkIfLeaderChanged(currentLock.Value)
    }