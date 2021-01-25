using System;
using System.Buffers.Text;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using PerfTesting.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using HttpMethod = System.Net.Http.HttpMethod;

namespace PerfTesting
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddHttpClient();
            services.AddSingleton<Sender>();
            services.AddSingleton<Counter>();
            services.AddSingleton(_ =>
            {
                ConnectionSettings settings;
                if (!string.IsNullOrEmpty(Configuration["password"]))
                {
                    var credentials = new BasicAuthenticationCredentials("elastic", Configuration["password"]);
                    settings = new ConnectionSettings(new CloudConnectionPool(Configuration["cloudId"], credentials)).DefaultIndex("test");
                }
                else
                {
                    settings = new ConnectionSettings(new SingleNodeConnectionPool(new Uri(Configuration["url"]))).DefaultIndex("test");
                }

                return new ElasticClient(settings);
            });
        }
        
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }

    public class Counter
    {
        private long _slowCounter = 0;
        private long _totalCounter = 0;
        private long _msTotal = 0;

        public void Update(long requestMs, long tookMs)
        {
            if (requestMs - tookMs > 300)
                Interlocked.Increment(ref _slowCounter);

            Interlocked.Increment(ref _totalCounter);
            Interlocked.Add(ref _msTotal, requestMs);
        }

        public void Reset()
        {
            _slowCounter = 0;
            _totalCounter = 0;
            _msTotal = 0;
        }
        
        public long SlowCount => _slowCounter;
        public long TotalRequests => _totalCounter;
        public long AverageRequestTime
        {
            get
            {
                if (_totalCounter == 0) return 0;
                return _msTotal / _totalCounter;
            }
        }
    }

    public class Sender
    {
        private readonly Counter _counter;
        private readonly IConfiguration _config;

        public Sender(Counter counter, IConfiguration config)
        {
            _counter = counter;
            _config = config;
        }

        private readonly HttpClient _client = new(new SocketsHttpHandler {MaxConnectionsPerServer = 100, PooledConnectionLifetime = TimeSpan.FromMinutes(5)});

        public async Task Get()
        {
            var sw = Stopwatch.StartNew();

            var req = new HttpRequestMessage(HttpMethod.Post, $"{_config["url"]}/test/_search")
            {
                Content = new StringContent(@"{""size"": 20}", Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrEmpty(_config["password"]))
            {
                var buffer = new byte[256];
                Base64.EncodeToUtf8(Encoding.UTF8.GetBytes($"elastic:{_config["password"]}"), buffer, out _, out var written);
                req.Headers.Authorization = AuthenticationHeaderValue.Parse($"Basic {Encoding.UTF8.GetString(buffer.AsSpan().Slice(0, written))}");
            }

            var response = await _client.SendAsync(req);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync();
            var searchResponse = await JsonSerializer.DeserializeAsync<SearchResponse>(stream);

            if (searchResponse is null) throw new Exception();

            sw.Stop();

            _counter.Update(sw.ElapsedMilliseconds, searchResponse.Took);
        }
    }
}
