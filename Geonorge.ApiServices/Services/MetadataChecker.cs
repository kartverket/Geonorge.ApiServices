using GeoNorgeAPI;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Caching.Memory;
using Geonorge.ApiServices.Models;
using Geonorge.ApiServices.Services;
using HttpClientFactory = Kartverket.Geonorge.Utilities.Organization.HttpClientFactory;
using IHttpClientFactory = Kartverket.Geonorge.Utilities.Organization.IHttpClientFactory;
using Serilog;
using System.Text.RegularExpressions;
using www.opengis.net;
using System;
using Kartverket.Geonorge.Utilities;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Kartverket.Geonorge.Api.Services
{
    public class MetadataChecker : IMetadataChecker
    {
        private readonly ILogger<DcatService> _logger;
        private readonly IConfiguration _settings;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;

        string kartkatalogenUrl;
        GeoNorge _geoNorge;

        IEnumerable<JToken> metadataSets;

        public List<MetadataEntry> metadataProblems = new List<MetadataEntry>();

        public MetadataChecker(IConfiguration settings, ILogger<DcatService> logger, IWebHostEnvironment env)
        {
            _settings = settings;
            _logger = logger;
            _env = env;
            kartkatalogenUrl = _settings["KartkatalogenUrl"];
            _httpClientFactory = new HttpClientFactory();
            _geoNorge = new GeoNorge("", "", _settings["GeoNetworkUrl"]);
        }


        public void CheckSolr()
        {
            _logger.LogInformation("Start checking mismatch search index and metadata in geonetwork");
            metadataSets = GetSearchMetadata();
            CheckMetadata();
            _logger.LogInformation("End checking mismatch search index and metadata in geonetwork");
        }

        private void CheckMetadata()
        {
            _geoNorge.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
            _geoNorge.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);

            foreach (var metadata in metadataSets.ToList())
            {
                System.Diagnostics.Debug.WriteLine(metadata["Title"]);

                string uuid = metadata["Uuid"].ToString();
                string title = metadata["Title"].ToString();

                try
                {
                    var metadataGeonetwork = _geoNorge.GetRecordByUuid(uuid);
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

        public void CheckGeonetwork()
        {
            _logger.LogInformation("Start checking mismatch geonetwork and search index");
            RunSearch(1);
            _logger.LogInformation("End checking mismatch geonetwork and search index");
        }

        private void RunSearch(int startPosition)
        {
            _logger.LogInformation("Running search from start position: " + startPosition);
            SearchResultsType searchResult = null;
            try
            {
                searchResult = _geoNorge.SearchIso("", startPosition, 50, false);
                _logger.LogInformation("Next record: " + searchResult.nextRecord + " " + searchResult.numberOfRecordsReturned + " " + searchResult.numberOfRecordsMatched);
                
                foreach(var item in searchResult.Items) 
                {
                   MD_Metadata_Type metadata = (MD_Metadata_Type)item;
                   string uuid = metadata.fileIdentifier.CharacterString.ToString();
                    var httpClient = _httpClientFactory.GetHttpClient();
                    var json = httpClient.GetStringAsync(kartkatalogenUrl + "api/metadata/" + uuid).Result;
                    var response = Newtonsoft.Json.Linq.JObject.Parse(json);

                    var uuidResponse = response["Uuid"];
                    if (uuidResponse == null)
                        _logger.LogError("Metadata not found in kartkatalogen for uuid: " + uuid);

                }
            }
            catch (Exception exception)
            {
                Log.Error("Error in ISO format from Geonetwork position: " + startPosition, exception);
            }

            int nextRecord;
            int numberOfRecordsMatched;
            nextRecord = int.Parse(searchResult.nextRecord);
            numberOfRecordsMatched = int.Parse(searchResult.numberOfRecordsMatched);
            if (nextRecord < numberOfRecordsMatched)
            {
                if (nextRecord > 0)
                    RunSearch(nextRecord);
            }

        }
    }

    public interface IMetadataChecker
    {
        public void CheckSolr();
        public void CheckGeonetwork();
    }
}