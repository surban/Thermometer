namespace TWeb

open System
open IntelliFactory.WebSharper
open IntelliFactory.WebSharper.Html
open IntelliFactory.WebSharper.Highcharts
open IntelliFactory.WebSharper.JQuery

[<JavaScript>]
module Client =
    let dayChart = Div []
    let weekChart = Div []
    let monthChart = Div []
    let cleaningChart = Div []

    let ShowAsTable (data: Remoting.DataPoint list) =
        Table [
            THead [TR [TH [Text "Time"]; TH [Text "Temperature"]]]
            TBody [for d in data -> 
                   TR [TD [Text (string d.timestamp)]; TD [Text (string d.temperature)]] 
                  ]                
        ]

    type DateTimeLabelFormatCfg = {hour: string; week: string; day: string;}
    let TimeFormat = {hour="%H:%M"; week="%e. %b"; day="%e. %b"; }

    let CreateChart 
        (target: Dom.Node) (timeUnit: string) (timeFormat: DateTimeLabelFormatCfg)
        (this: Remoting.ClientDataPoint list) (previous: Remoting.ClientDataPoint list) =
        let toHighchartsArray (data: Remoting.ClientDataPoint list) = 
            data 
            |> List.map (fun d -> [| d.ticks; d.temperature |])
            |> List.toArray

        let thisData = toHighchartsArray this
        let previousData = toHighchartsArray previous

        Highcharts.Create(JQuery.Of target,
            HighchartsCfg(
                Title = TitleCfg(Text="by " + timeUnit),
                XAxis = XAxisCfg(Type = "datetime", DateTimeLabelFormats=timeFormat),
                YAxis = YAxisCfg(Title = YAxisTitleCfg(Text = "Temperature (°C)")),
                Tooltip = TooltipCfg(ValueSuffix = "°C"), 
                Series = [| 
                    SeriesCfg(Name="this " + timeUnit, Data=As thisData);
                    SeriesCfg(Name="previous " + timeUnit, Data=As previousData);
                |],
                Colors = As [| "#FF0066"; "#FFCCE0" |],
                Credits = CreditsCfg(Enabled=false)
            )) |> ignore


    let RefreshData () =
        async {
            let noDiff = TimeSpan()

            let daySpan = TimeSpan(1, 0, 0, 0)
            let startOfToday = DateTime.Now - DateTime.Now.TimeOfDay
            let! todayData = Remoting.GetData startOfToday (startOfToday + daySpan) noDiff
            let! yesterdayData = Remoting.GetData (startOfToday - daySpan) startOfToday daySpan
            do CreateChart dayChart.Body "day" {TimeFormat with day="%H:%M"; hour="%H:%M"} todayData yesterdayData

            let weekSpan = TimeSpan(7, 0, 0, 0)
            let startOfWeek = startOfToday - TimeSpan(int startOfToday.DayOfWeek, 0, 0, 0)
            let! thisWeekData = Remoting.GetData startOfWeek (startOfWeek + weekSpan) noDiff
            let! lastWeekData = Remoting.GetData (startOfWeek - weekSpan) startOfWeek weekSpan
            do CreateChart weekChart.Body "week" {TimeFormat with day="%A"; hour=" "} thisWeekData lastWeekData

            let startOfMonth = startOfToday - TimeSpan(startOfToday.Day - 1, 0, 0, 0)
            let monthSpan = startOfMonth - startOfMonth.AddMonths(-1)
            let! thisMonthData = Remoting.GetData startOfMonth (startOfMonth.AddMonths(1)) noDiff
            let! lastMonthData = Remoting.GetData (startOfMonth - monthSpan) startOfMonth monthSpan
            do CreateChart monthChart.Body "month" {TimeFormat with day="%e"; week="%e"} thisMonthData lastMonthData

            let cleaningSpan = TimeSpan(0, 1, 0, 0)
            let startOfCleaning = startOfToday.AddHours(6.5)
            let! todayCleaning = Remoting.GetData startOfCleaning (startOfCleaning + cleaningSpan) noDiff
            let! yesterdayCleaning = Remoting.GetData (startOfCleaning - daySpan) (startOfCleaning + cleaningSpan-daySpan) daySpan
            do CreateChart cleaningChart.Body "day (between 6h30 and 7h30)" {TimeFormat with hour="%H:%M"} todayCleaning yesterdayCleaning
        } |> Async.Start

    let Main () =
        RefreshData()
        Div [] -< [dayChart; weekChart; monthChart]
        //Div [] -< [dayChart; weekChart; monthChart; cleaningChart]

