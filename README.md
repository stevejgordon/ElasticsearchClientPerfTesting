## Set Up

Add the following user secrets (replacing the values as appropriate):

{
  "password": "PasswordForElasticUser",
  "cloudId": "IfUsingACloudHostedInstanceTheId",
  "url": "TheElasticsearchURL"
}

## Running the Sample

1. Run the application in release mode.
2. Optionally attach dotnet counters ([install instructions](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters)) from a dedicated terminal:
    > dotnet-counters ps

    Get the process ID of your running instance to use in the next command (replacing 1234)

    > dotnet-counters monitor --process-id 1234 --counters System.Net.Http,System.Runtime
3. Use a load generator such as Bombardier to send load to the required endpoint (see below):
    > bombardier -c 50 -r 200 -n 2000 https://localhost:5001/
4. Check the count stats
    > http://localhost:5001/count
6. Reset the count stats
    > http://localhost:5001/reset


During the run you can monitor the counters which should include System.Net.Http at the top:

```
[System.Net.Http]
    Current Http 1.1 Connections                                  79
    Current Http 2.0 Connections                                   0
    Current Requests                                               0
    HTTP 1.1 Requests Queue Duration (ms)                          0
    HTTP 2.0 Requests Queue Duration (ms)                          0
    Requests Failed                                                0
    Requests Failed Rate (Count / 1 sec)                           0
    Requests Started                                             300
    Requests Started Rate (Count / 1 sec)                         45
```

Take note of:
- Current Http 1.1 Connections which should be less than the number of concurrent connections made by Bombardier. 
- Requests Started which should total the number of requests sent by Bombardier.
- HTTP 1.1 Requests Queue Duration (ms) which during the load test should not indicate a queuing delay.
- Requests Started Rate (Count / 1 sec) which during the load test should rougly be equivilent to the number of RPS from the load test. NOTE: This resets every second and will return to 0 when the load test ends.


### Available endpoints

| Endpoint                       | Description                                                |
|--------------------------------|------------------------------------------------------------|
| https://localhost:5001/        | Sends search requests using the Elasticsearch NEST client. |
| https://localhost:5001/factory | Sends requests using a client from `IHttpClientFactory`.   |
| https://localhost:5001/sender  | Sends requests using a singleton `HttpClient`.             |