using GeoNorgeAPI;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Caching.Memory;
using Geonorge.ApiServices.Models;
using Geonorge.ApiServices.Services;
using HttpClientFactory = Kartverket.Geonorge.Utilities.Organization.HttpClientFactory;
using IHttpClientFactory = Kartverket.Geonorge.Utilities.Organization.IHttpClientFactory;

namespace Kartverket.Geonorge.Api.Services
{
    public class MetadataChecker : IMetadataChecker
    {
        private readonly ILogger<DcatService> _logger;
        private readonly IConfiguration _settings;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;

        string kartkatalogenUrl;

        IEnumerable<JToken> metadataSets;

        public List<MetadataEntry> metadataProblems = new List<MetadataEntry>();

        public MetadataChecker(IConfiguration settings, ILogger<DcatService> logger, IWebHostEnvironment env)
        {
            _settings = settings;
            _logger = logger;
            _env = env;
            kartkatalogenUrl = _settings["KartkatalogenUrl"];
            _httpClientFactory = new HttpClientFactory();
        }


        public void Check()
        {
            _logger.LogInformation("Start checking mismatch search index and metadata in geonetwork");
            metadataSets = GetSearchMetadata();
            CheckMetadata();
            _logger.LogInformation("End checking mismatch search index and metadata in geonetwork");
        }

        private void CheckMetadata()
        {   
            string server = _settings["GeoNetworkUrl"];

            GeoNorge api = new GeoNorge("","", server);
            api.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
            api.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);

            foreach (var metadata in metadataSets.ToList())
            {
                System.Diagnostics.Debug.WriteLine(metadata["Title"]);

                string uuid = metadata["Uuid"].ToString();
                string title = metadata["Title"].ToString();

                try
                {
                    var metadataGeonetwork = api.GetRecordByUuid(uuid);
                    if (metadataGeonetwork == null)
                        throw new Exception("Metadata not found in geonetwork");
                }
                catch (Exception ex)
                {
                    metadataProblems.Add(new MetadataEntry { Uuid = uuid, Title = title, Problem = "Uuid finnes ikke geonetwork" });
                    _logger.LogError("Uuid finnes ikke geonetwork: " + uuid + ", title: " + title, ex);
                    System.Diagnostics.Debug.WriteLine("Uuid finnes ikke geonetwork: " + uuid + "title: " + title);
                }
            }

        }

        public IEnumerable<JToken> GetSearchMetadata()
        {
            var httpClient = _httpClientFactory.GetHttpClient();
            var json = httpClient.GetStringAsync(kartkatalogenUrl + "api/search?limit=12000").Result;
            var response1 = Newtonsoft.Json.Linq.JObject.Parse(json);
            var result = response1.SelectToken("Results").ToList();

            return result;
        }

        private void LogEventsDebug(string log)
        {

            System.Diagnostics.Debug.Write(log);
            _logger.LogDebug(log);
        }

        private void LogEventsError(string log, Exception ex)
        {
            _logger.LogError(log, ex);
        }
    }

    public interface IMetadataChecker
    {
        public void Check();
    }
}