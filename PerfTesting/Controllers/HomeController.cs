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

            var searchResponse = await _elasticClient.SearchAsync<Place>(s => s
                .Query(qu => qu.Bool(boo => boo
                    .Must(mu => mu.DisMax(di => di
                        .TieBreaker(0)
                        .Queries(que => que
                            .QueryString(m => m
                                .Query("phố đà").DefaultOperator(Operator.And)
                                .Fields(fi => fi.Field("name_address").Field("address"))
                                .Analyzer("standard_analyzer").Escape()), que => que
                            .QueryString(m => m
                                .Query("phố đà").DefaultOperator(Operator.And)
                                .Fields(fi => fi.Field("name"))
                                .Analyzer("standard_analyzer").Escape()))))
                    .Should(mu => mu.DisMax(di => di.TieBreaker(0.1).Boost(4)
                        .Queries(que => que
                            .QueryString(m => m
                                .Query("\"phố\"").DefaultOperator(Operator.And)
                                .Fields(fi => fi.Field("name_address")
                                    .Field("address")).PhraseSlop(10)
                                .Analyzer("standard_analyzer").Boost(1).Escape()), que => que
                            .QueryString(m => m
                                .Query("\"phố\"").DefaultOperator(Operator.And)
                                .Fields(fi => fi.Field("name")).PhraseSlop(10)
                                .Analyzer("standard_analyzer").Boost(2).Escape()))))
                    .Filter(f => f.Term(t => t.Field("is_deleted").Value(false))))).From(0).Size(20));

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

        //[HttpGet("seed")]
        //public async Task<IActionResult> Seed()
        //{
        //    var place = new Place
        //    {
        //        Name = "A place",
        //        Address1 = "1 Some Road",
        //        Address2 = "Something",
        //        Town = "Eastbourne",
        //        County = "Sussex",
        //        Postcode = "BN011AA"
        //    };
            
        //    for (var i = 0; i < 25; i++)
        //    {
        //        await _elasticClient.IndexAsync(place, d => d.Index("test"));
        //    }
            
        //    return Ok();
        //}
    }

    public class Place
    {
        [Text(Name = "address")]
        public string Address { get; set; }

        [Text(Name = "end_date")]
        public DateTime EndDate { get; set; }

        [Text(Name = "is_deleted")]
        public bool IsDeleted { get; set; }

        [Text(Name = "name")]
        public string Name { get; set; }

        [Text(Name = "name_address")]
        public string NameAddress { get; set; }

        [Text(Name = "object_id")]
        public string ObjectId { get; set; }

        [Text(Name = "start_date")]
        public DateTime StartDate { get; set; }

        [Text(Name = "types")]
        public string[] Types { get; set; }

        [Text(Name = "location")]
        public double[] Location { get; set; }

        [Text(Name = "geometry")]
        public Geometry Geometry { get; set; }
    }

    public class Geometry
    {
        [Text(Name = "coordinates")]
        public double[] Coordinates { get; set; }
        
        [Text(Name = "type")]
        public string Type { get; set; }
    }

    public class SearchResponse
    {
        public long Took { get; set; }
    }
}
