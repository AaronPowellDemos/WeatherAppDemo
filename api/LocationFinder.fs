module LocationFinder

open FSharp.Data
open WeatherCache
open FSharp.Data.HttpRequestHeaders

type AddressLookup = JsonProvider<"./location-sample.json">

let private searchAddressUrl =
    sprintf
        "https://atlas.microsoft.com/search/address/json?api-version=1.0&subscription-key=%s&query=%s&countrySet=AU&limit=1"

let findAddressGeoCodes accessToken address =
    async {
        let! result = searchAddressUrl accessToken address |> AddressLookup.AsyncLoad

        return Array.tryHead result.Results }

type SpacialRequest = JsonProvider<"./spacial-request-sample.json">

type SpacialResponse = JsonProvider<"./spacial-response-sample.json">

let private closestPointUrl =
    sprintf "https://atlas.microsoft.com/spatial/closestPoint/json?subscription-key=%s&api-version=1.0&lat=%f42&lon=%f"

let findNearestLocation accessToken (observations: Observation seq) (location: AddressLookup.Position) =
    async {
        let features =
            observations
            |> Seq.map
                (fun o ->
                    SpacialRequest.Feature
                        ("Feature", SpacialRequest.Properties(o.Id),
                         SpacialRequest.Geometry("Point", [| o.Lon; o.Lat |])))
        let request = SpacialRequest.Root("FeatureCollection", features |> Seq.toArray)

        System.IO.File.WriteAllText("foo.json", sprintf "%O" request)

        let url = closestPointUrl accessToken location.Lat location.Lon
        let! rawResponse = Http.AsyncRequestString
                               (url, headers = [ ContentType HttpContentTypes.Json ],
                                body = TextRequest(sprintf "%O" request))

        let response = SpacialResponse.Parse rawResponse

        return response.Result |> Array.tryHead
    }
