namespace Company.Function

open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Azure.WebJobs.Extensions.DurableTask
open WeatherCache
open LocationFinder
open FSharp.Control.Tasks.V2
open System.Threading.Tasks
open System

module GetWeather =
    let accessToken = Environment.GetEnvironmentVariable "SearchToken"


    [<FunctionName("GetWeather")>]
    let getWeather ([<HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "weather/{location}")>] req: HttpRequest)
        ([<DurableClient>] client: IDurableEntityClient) (location: string) (log: ILogger) =
        task {
            log.LogInformation(sprintf "Requesting some weather data for '%s'" location)

            let entityId = EntityId(nameof WeatherCache, "Cache")

            let! state = client.ReadEntityStateAsync<WeatherCache> entityId

            if state.EntityExists then
                let! addressResult = findAddressGeoCodes accessToken location |> Async.StartAsTask
                return match addressResult with
                       | Some result ->
                           async {
                               let! nearestLocation = findNearestLocation accessToken
                                                          state.EntityState.Data.Observations result.Position
                               match nearestLocation with
                               | Some nl ->
                                   let observation =
                                       state.EntityState.Data.Observations |> Seq.find (fun o -> o.Id = nl.GeometryId)
                                   return OkObjectResult observation
                               | None -> return OkObjectResult(state.EntityState.Data.Observations |> Seq.head)
                           }
                           |> Async.RunSynchronously
                       | None -> OkObjectResult []

            else
                do! Task.Delay 5000

                let! state = client.ReadEntityStateAsync<WeatherCache> entityId
                return OkObjectResult state.EntityState.Data
        }

    [<FunctionName("PopulateWeather")>]
    let populateWeather ([<TimerTrigger("0 */15 * * * *")>] timer: TimerInfo)
        ([<DurableClient>] client: IDurableEntityClient) (log: ILogger) =
        task {
            log.LogInformation "Populating the entity"

            let entityId = EntityId(nameof WeatherCache, "Cache")

            do! client.SignalEntityAsync<IWeatherCache>
                    (entityId, (fun (proxy: IWeatherCache) -> proxy.Refresh() |> Task.WaitAll))

            log.LogInformation "Entity populated"
        }
