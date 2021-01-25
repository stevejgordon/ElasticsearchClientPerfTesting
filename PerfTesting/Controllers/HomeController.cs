using System;
using System.Buffers.Text;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nest;

namespace PerfTesting.Controllers
{
    [ApiController]
    [Route("")]
    public class HomeController : ControllerBase
    {
        private readonly ElasticClient _elasticClient;
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _clientFactory;
        private readonly Counter _counter;
        private readonly Sender _sender;

        public HomeController(ElasticClient elasticClient, ILogger<HomeController> logger, IConfiguration config, IHttpClientFactory clientFactory, Counter counter, Sender sender)
        {
            _elasticClient = elasticClient;
            _logger = logger;
            _config = config;
            _clientFactory = clientFactory;
            _counter = counter;
            _sender = sender;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var sw = Stopwatch.StartNew();

            var searchResponse = await _elasticClient.SearchAsync<Place>(s => s.Size(20));

            sw.Stop();

            _counter.Update(sw.ElapsedMilliseconds, searchResponse.Took);
            
            return Ok();
        }

        [HttpGet("count")]
        public IActionResult GetCount()
        {
            return Ok(_counter);
        }

        [HttpGet("reset")]
        public IActionResult ResetCount()
        {
            _counter.Reset();
            return Ok();
        }

        [HttpGet("factory")]
        public async Task<IActionResult> GetFactory()
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

            var response = await _clientFactory.CreateClient().SendAsync(req);

            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync();
            var searchResponse = await JsonSerializer.DeserializeAsync<SearchResponse>(stream);

            if (searchResponse is null) return BadRequest();
            
            sw.Stop();
            
            _counter.Update(sw.ElapsedMilliseconds, searchResponse.Took);

            return Ok();
        }

        [HttpGet("sender")]
        public async Task<IActionResult> GetSender()
        {
            await _sender.Get();
            return Ok();
        }

        [HttpGet("seed")]
        public async Task<IActionResult> Seed()
        {
            var place = new Place
            {
                Name = "A place",
                Address1 = "1 Some Road",
                Address2 = "Something",
                Town = "Eastbourne",
                County = "Sussex",
                Postcode = "BN011AA"
            };
            
            for (var i = 0; i < 25; i++)
            {
                await _elasticClient.IndexAsync(place, d => d.Index("test"));
            }
            
            return Ok();
        }
    }

    public class Place
    {
        public string Name { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string Town { get; set; }
        public string County { get; set; }
        public string Postcode { get; set; }
    }

    public class SearchResponse
    {
        public long Took { get; set; }
    }
}
