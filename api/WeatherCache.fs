module WeatherCache

open Newtonsoft.Json
open BomResult
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.DurableTask
open System.Threading.Tasks
open System.Net
open System.IO
open Microsoft.Extensions.Logging

type Observation =
    { Id: int
      Lat: decimal
      Lon: decimal
      Name: string
      Temp: decimal }

type Weather =
    { Observations: Observation seq }

type IWeatherCache =
    abstract Refresh: unit -> Task<unit>

let downloadFromFtp (url: string) =
    async {
        let req = WebRequest.Create url :?> FtpWebRequest
        req.Method <- WebRequestMethods.Ftp.DownloadFile
        let! response = req.GetResponseAsync() |> Async.AwaitTask

        use stream = response.GetResponseStream()
        use reader = new StreamReader(stream)
        return! reader.ReadToEndAsync() |> Async.AwaitTask
    }

[<JsonObject(MemberSerialization.OptIn)>]
type WeatherCache() =

    [<JsonProperty>]
    member val Data = Unchecked.defaultof<Weather> with get, set

    [<JsonProperty>]
    member val Ready = false with get, set

    interface IWeatherCache with
        member this.Refresh() =
            async {
                printfn "Starting to get updated weather data"
                let! nswXml = downloadFromFtp "ftp://ftp.bom.gov.au/anon/gen/fwo/IDN60920.xml"
                let nsw = BomObservationResult.Parse nswXml

                printfn "Got data for %A" nsw.Amoc.IssueTimeLocal

                let observations =
                    nsw.Observations.Stations
                    |> Array.map (fun station ->
                        let tempEl =
                            station.Period.Level.Elements |> Array.tryFind (fun el -> el.Type = "apparent_temp")
                        { Id = station.WmoId
                          Name = station.Description
                          Lat = station.Lat
                          Lon = station.Lon
                          Temp =
                              match tempEl with
                              | Some t -> defaultArg t.Number 0.0m
                              | None -> 0.0m })
                this.Data <- { Observations = observations }
                this.Ready <- true
            }
            |> Async.StartAsTask

    [<FunctionName(nameof (WeatherCache))>]
    member _.Run([<EntityTrigger>] ctx: IDurableEntityContext) = ctx.DispatchAsync<WeatherCache>()
