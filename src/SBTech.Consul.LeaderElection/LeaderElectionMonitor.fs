namespace rec SBTech.Consul.LeaderElection

open System
open System.Timers

open Consul
open FSharp.Control.Tasks.V2.ContextInsensitive

type ElectionMonitorConfig = {
    Client: ConsulClient
    LockOptions: LockOptions        
}
  with
  static member Default(serviceName, client) =
      let key = sprintf "services/%s/leader" serviceName      
      let lockOptions = LockOptions(key)      
      lockOptions.SessionName   <- "lock session for " + serviceName
      lockOptions.SessionTTL    <- TimeSpan.FromSeconds(10.)
      lockOptions.LockWaitTime  <- TimeSpan.FromSeconds(5.)
      lockOptions.LockRetryTime <- TimeSpan.FromSeconds(5.)
      lockOptions.LockTryOnce   <- true
      
      { LockOptions     = lockOptions        
        Client          = client }

type ElectionArgs = { IsLeader: bool }

type LeaderElectionMonitor(config) =
    
    let mutable isLeader  = false
    let mutable isWorking = false
    let mutable currentLock = Option<IDistributedLock>.None
    let lockOptions   = config.LockOptions
    let leaderChanged = Event<ElectionArgs>()
    let tryLockTimer  = new Timer(TimeSpan.FromSeconds(1.0).TotalMilliseconds)

    let runLockFlow (shouldTriggerEvent: bool) = task {
        isWorking <- true

        let tryCreateAndAcquireLock () = config.Client.AcquireLock(lockOptions)

        let tryAcquireLock (lock: IDistributedLock) = task {
            let! canclToken = lock.Acquire()
            return ()
        }

        let checkIfLeaderChanged (lock: IDistributedLock, shouldTrigger) =
            if lock.IsHeld <> isLeader then 
                isLeader <- lock.IsHeld
                if shouldTrigger then leaderChanged.Trigger({ IsLeader = isLeader })
        
        let handleException (ex: Exception) =
            match ex with            
            | :? LockMaxAttemptsReachedException -> () // this is expected for (LockTryOnce == true) if it couldn't acquire the lock within first attempts.                       
            | :? AggregateException as agex -> 
                match agex.InnerException with
                | :? LockMaxAttemptsReachedException -> ()                
                | ex -> ex.ToString() |> System.Diagnostics.Trace.TraceError            
            | ex -> ex.ToString() |> System.Diagnostics.Trace.TraceError

        try
            try
                match currentLock with
                | Some lock -> if lock.IsHeld <> true 
                               then do! tryAcquireLock(lock)

                | None      -> let! lock = tryCreateAndAcquireLock()
                               currentLock <- Option.ofObj(lock)
            with
            | ex -> handleException(ex)
        
            // if leader changed we need to trigger event
            if currentLock.IsSome then
                checkIfLeaderChanged(currentLock.Value, shouldTriggerEvent)
        finally
            isWorking <- false
    }

    do tryLockTimer.Elapsed.Add(fun _ -> if not isWorking then runLockFlow(true) |> ignore)

    [<CLIEvent>]
    member x.LeaderChanged = leaderChanged.Publish    
    member x.IsLeader = isLeader

    member x.Start() = task {
        if not tryLockTimer.Enabled then                 
            let! lockResult = runLockFlow(false)
            tryLockTimer.Start()          
    }

    member x.Stop() =
        try
            tryLockTimer.Stop()
            if currentLock.IsSome 
            then currentLock.Value.Release().Wait()
                 currentLock.Value.Destroy().Wait()                  
        with
        | ex -> ex.ToString() |> System.Diagnostics.Trace.TraceError