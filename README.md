## Set Up

Add the following user secrets (replacing the values as appropriate):

{
  "password": "PasswordForElasticUser",
  "cloudId": "IfUsingACloudHostedInstanceTheId",
  "url": "TheElasticsearchURL"
}

## Running the Sample

1. Run the application in release mode.
2. Optionally attach dotnet counters:
    > dotnet-counters ps

    > dotnet-counters monitor --process-id IDFROMPS --counters System.Net.Http,System.Runtime
3. Use a load generator such as Bombardier to send load to the required endpoint (see below):
    > bombardier -c 50 -r 200 -n 2000 https://localhost:5001/
4. Check the count stats
    > http://localhost:5001/count
5. Reset the count stats
    > http://localhost:5001/reset

### Available endpoints

| Endpoint                       | Description                                                |
|--------------------------------|------------------------------------------------------------|
| https://localhost:5001/        | Sends search requests using the Elasticsearch NEST client. |
| https://localhost:5001/factory | Sends requests using a client from `IHttpClientFactory`.   |
| https://localhost:5001/sender  | Sends requests using a singleton `HttpClient`.             |