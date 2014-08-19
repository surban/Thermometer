namespace TWeb

open IntelliFactory.WebSharper
open Microsoft.FSharp.Data.TypeProviders
open System.Net.Sockets
open System.Threading
open System


module Remoting =
    [<Literal>] 
    let sql_connection = "Data Source=srv-db.brml.tum.de\SQL2012;Initial Catalog=surban;Integrated Security=SSPI;"  

    type dbSchema = SqlDataConnection<sql_connection>
    let db = dbSchema.GetDataContext()

    type DataPoint = { timestamp : DateTime; temperature : float; }
    type ClientDataPoint = {ticks: float; temperature: float;}

    let maxDataPoints = 300

    let splitList pntsPerSample lst =        
        let rec splitListRec lst part pntsToTake =
            seq {
                match lst with
                | head :: tail ->
                    if pntsToTake >= 1.0 then
                        yield! splitListRec tail (head::part) (pntsToTake - 1.0)
                    else
                        yield List.rev part
                        yield! splitListRec tail [] (pntsToTake + pntsPerSample)
                | [] -> if part <> [] then yield List.rev part
            }
        splitListRec lst [] pntsPerSample
        
    let average pntsPerSample lst =
        lst 
            |> splitList pntsPerSample
            |> Seq.map (fun lst ->  
            { 
                timestamp = DateTime(int64(List.averageBy (fun e -> float e.timestamp.Ticks) lst));
                temperature = List.averageBy (fun (e: DataPoint) -> e.temperature) lst
            })

    let ToJavaScriptTicks (t: DateTime) =
        (t.ToLocalTime() - DateTime(1970, 1, 1)).TotalMilliseconds

    [<Remote>]
    let GetData (fromTime: DateTime) (toTime: DateTime) (offset: TimeSpan) =
        async {
            let fromTimeUtc = fromTime.ToUniversalTime()
            let toTimeUtc = toTime.ToUniversalTime()
            let res = Seq.toList (query {
                for row in db.Temperatures do
                where (fromTimeUtc <= row.Timestamp && row.Timestamp <= toTimeUtc)
                select {timestamp = row.Timestamp; temperature = row.Temperature;}
            })
            let compacted = 
                if List.length res > maxDataPoints then
                    res |> average (float (List.length res) / float maxDataPoints) |> Seq.toList
                else
                    res
            let cf = [for dp in compacted -> {ticks=ToJavaScriptTicks (dp.timestamp + offset);
                                              temperature=dp.temperature;}]
            return cf
        }
