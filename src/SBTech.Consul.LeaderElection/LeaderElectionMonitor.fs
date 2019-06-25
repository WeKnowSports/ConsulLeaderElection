namespace rec SBTech.Consul.LeaderElection

open System
open System.Threading

open Consul
open FSharp.Control.Tasks.V2.ContextInsensitive

type ElectionMonitorConfig = {
    Client: ConsulClient
    SessionOptions: SessionEntry
    LockOptions: KVPair
    TryAcquireLockInterval: TimeSpan
} with
  static member Default(serviceName, client) =            
      let key = sprintf "services/%s/leader" serviceName
      let lockOpts = KVPair(key)
      let sessionOptions = SessionEntry()
      sessionOptions.Name   <- "lock session for " + serviceName
      sessionOptions.TTL    <- Nullable<TimeSpan>(TimeSpan.FromSeconds 10.0)
      sessionOptions.Behavior <- SessionBehavior.Release       
      { Client         = client 
        SessionOptions = sessionOptions                
        LockOptions    = lockOpts
        TryAcquireLockInterval = TimeSpan.FromSeconds 3.0 }
      
type ElectionArgs = { IsLeader: bool }

type Session = {
    SessionId : string;
    RenewalWorker: Tasks.Task
} with
  static member internal Create(c: ConsulClient, sessionOptions: SessionEntry,
                                ct: CancellationToken) = task {
    let! r = c.Session.Create(sessionOptions, ct)
    let sessionId = r.Response
    let worker = c.Session.RenewPeriodic(sessionOptions.TTL.Value, sessionId, ct)
                          .ContinueWith(fun t -> 
                            if t.IsFaulted then
                                sprintf "Session autorenewal crashed. %s" (t.Exception.ToString())
                                |> System.Diagnostics.Trace.TraceError)

    return { SessionId = sessionId; RenewalWorker = worker}
}
      
      
type LeaderElectionMonitor(config: ElectionMonitorConfig) =
    
    let mutable isWorking = false
    let mutable currentIsLockHeldStatus = false
    let mutable currentSession = None
        
    let leaderChanged = Event<ElectionArgs>()
    let tryLockTimer  = new System.Timers.Timer(config.TryAcquireLockInterval.TotalMilliseconds)
    let cancelationTokenSrc = new CancellationTokenSource()

    let tryDestroySession(c: ConsulClient, sessionId: string) = task {
        let! isSessionDestroyed = c.Session.Destroy(sessionId)
        return isSessionDestroyed.Response
    }
    
    let tryReleaseLock(c: ConsulClient, sessionId: string, lockOpts: KVPair) = task {        
        lockOpts.Session <- sessionId 
        let! isLockReleased = c.KV.Release(lockOpts)            
        return isLockReleased.Response
    }             
    
    let tryAcquireLock (c: ConsulClient, sessionId: string, lockOpts: KVPair, ct: CancellationToken) = task {
        lockOpts.Session <- sessionId
        let! isLockHeld = c.KV.Acquire(lockOpts, ct)
        return isLockHeld.Response            
    }
    
    let checkIfLeaderChanged (newIsLockHeldStatus: bool, shouldTriggerEvent: bool) =
        if currentIsLockHeldStatus <> newIsLockHeldStatus then
            sprintf "IsLockHeld status changed to  %b" newIsLockHeldStatus
            |>  System.Diagnostics.Trace.TraceInformation

            if shouldTriggerEvent then                
                leaderChanged.Trigger({ IsLeader = newIsLockHeldStatus })
    
    let runLockFlow (shouldTriggerEvent: bool) = task {
        isWorking <- true                        
        try
            try
                if currentSession.IsNone || currentSession.Value.RenewalWorker.IsCompleted then
                    // Should initialize a session on the first run or crash                   
                    let! s = Session.Create(config.Client, config.SessionOptions, cancelationTokenSrc.Token)
                    
                    sprintf "Initialized new session with id %s" s.SessionId 
                    |>  System.Diagnostics.Trace.TraceInformation
                    
                    currentSession <- Some s
                   
                let! newIsLockHeldStatus = tryAcquireLock(config.Client, currentSession.Value.SessionId, config.LockOptions, cancelationTokenSrc.Token)
                // if leader changed we need to trigger event
                checkIfLeaderChanged(newIsLockHeldStatus,  shouldTriggerEvent)
                currentIsLockHeldStatus <- newIsLockHeldStatus
            with
            | ex -> ex.ToString() |> System.Diagnostics.Trace.TraceError
                    
        finally
            isWorking <- false
    }

    do tryLockTimer.Elapsed.Add(fun _ -> if not isWorking then runLockFlow(true) |> ignore)

    [<CLIEvent>]
    member x.LeaderChanged = leaderChanged.Publish    
    
    member x.IsLeader = currentIsLockHeldStatus

    member x.Start() = task {
        if not tryLockTimer.Enabled then                 
            let! lockResult = runLockFlow(false)
            tryLockTimer.Start()
    }

    member x.Stop() = task {
        try
            tryLockTimer.Stop()
 
            match currentSession with
            | Some s -> let! released = tryReleaseLock(config.Client, s.SessionId, config.LockOptions) // Ensure that we released the lock first
                        cancelationTokenSrc.Cancel(false)
                        let! destroyed =  tryDestroySession(config.Client, s.SessionId)
                        
                        sprintf "LeaderElectionMonitor stopped. lock released : %b, session destroyed: %b" released destroyed
                        |>  System.Diagnostics.Trace.TraceInformation                         
            | _ -> ()
        with
        | ex -> ex.ToString() |> System.Diagnostics.Trace.TraceError
    }    