namespace TGather

open System
open System.ServiceProcess
open Microsoft.FSharp.Data.TypeProviders
open System.Net.Sockets
open System.Threading
open System.ComponentModel
open System.Configuration.Install
open System.IO.Ports


module Thermometer =
    [<Literal>] 
    let sql_connection = "Data Source=srv-db.brml.tum.de\SQL2012;Initial Catalog=surban;Integrated Security=SSPI;"  
    let thermometer_comport = "COM3"

    type dbSchema = SqlDataConnection<sql_connection>
    let db = dbSchema.GetDataContext()

    let getTemperature() =
        try
            use port = new SerialPort(thermometer_comport, 9600, Parity.None, 8, StopBits.One)
            port.Handshake <- Handshake.None

            port.Open()
            port.DtrEnable <- false
            port.RtsEnable <- false
            Thread.Sleep(10)
            port.DiscardInBuffer()
            port.DtrEnable <- true
            port.RtsEnable <- true
            Thread.Sleep(1000)

            let buffer : byte[] = Array.zeroCreate 20
            let n_read = port.Read(buffer, 0, Array.length buffer) 

            port.DtrEnable <- false
            port.RtsEnable <- false
            port.Close()

            let reply = System.Text.Encoding.ASCII.GetString(buffer)           
            //printfn "Thermometer (%d bytes):\n%A" n_read reply.[0::n_read]
            let tval = reply.Split('C') 
            tval |> Seq.head |> float |> Some
        with
            | e -> 
                printfn "Exception: %A" (e.ToString())
                None
       
    let logTemperature timestamp temperature =
        let row = new dbSchema.ServiceTypes.Temperatures(Timestamp=timestamp,
                                                         Temperature=temperature)
        db.Temperatures.InsertOnSubmit(row)
        db.DataContext.SubmitChanges()

    let acquireAndLogTemperate() =
        match getTemperature() with
            | Some(temperature) ->
                let timestamp = DateTime.UtcNow
                printfn "Temperature at time %A is %A C" timestamp temperature
                logTemperature timestamp temperature
            | None -> printfn "Temperature not avilable"

    let createDummyData() =
        let mutable ts = DateTime.UtcNow - TimeSpan(60, 0, 0, 0)
        while ts < DateTime.UtcNow do
            logTemperature ts (float ts.Day + float ts.Hour * 0.2)
            ts <- ts.AddHours(1.)

module Service = 
    let sampling_interval = 60000.
    //let sampling_interval = 1000.
    let timer = new System.Timers.Timer(sampling_interval)
    timer.AutoReset <- false
    timer.Elapsed.Add (fun _ -> 
                            Thermometer.acquireAndLogTemperate()
                            timer.Start())

    let startAcquire() =
        timer.Start()

    let stopAcquire() =
        timer.Stop()

    type ThermometerWindowsService() =
        inherit ServiceBase(ServiceName="BRMLThermometer")

        override x.OnStart(args) =
            startAcquire()

        override x.OnStop() =
            stopAcquire()

[<RunInstaller(true)>]
type ThermometerWindowsServiceInstaller() =
    inherit Installer()
    do
        new ServiceProcessInstaller(Account=ServiceAccount.NetworkService)
        |> base.Installers.Add |> ignore
        new ServiceInstaller(ServiceName="BRMLThermometer", DisplayName="BRML Server Room Thermometer",
                                StartType=ServiceStartMode.Automatic)
        |> base.Installers.Add |> ignore

module Main =
    if Environment.UserInteractive then
        //Thermometer.createDummyData()
        Service.startAcquire()
        Thread.Sleep(System.Threading.Timeout.InfiniteTimeSpan)
    else
        ServiceBase.Run [| new Service.ThermometerWindowsService() :> ServiceBase |]  

