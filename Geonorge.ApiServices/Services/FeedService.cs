using Geonorge.ApiServices.Models;
using GeoNorgeAPI;

namespace Geonorge.ApiServices.Services
{
    public interface IFeedService
    {
        Task UpdateMetadata();
    }
    public class FeedService : IFeedService
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        private readonly ILogger<FeedService> _logger;
        private readonly IConfiguration _settings;
        IAtomFeedParser _atomFeedParser;

        public FeedService(IConfiguration settings, ILogger<FeedService> logger, IAtomFeedParser atomFeedParser)
        {
            _settings = settings;
            _logger = logger;
            _atomFeedParser = atomFeedParser;
        }

        public Task UpdateMetadata()
        {
            var datasetList = GetDatasets();

            var datasets = datasetList.OrderBy(x => x.Uuid)
               .GroupBy(x => x.Uuid)
               .Select(g => new {
                   Uuid = g.Key,
                   Datasets = g.Select(dataset => new {
                       dataset.Url,
                       dataset.Organization,
                       dataset.LastUpdated,
                       dataset.DistributionsFormats,
                       dataset.ReferenceSystems
                   })
               }).ToList();

            foreach (var dataset in datasets)
            {
                try
                {
                    var metadataInfo = new UpdateMetadataInformation();
                    metadataInfo.Uuid = dataset.Uuid.Trim();
                    metadataInfo.Distributions = new List<SimpleDistribution>();


                    foreach (var item in dataset.Datasets)
                    {
                        try
                        { metadataInfo.DatasetDateUpdated = DateTime.Parse(item.LastUpdated, System.Globalization.CultureInfo.InvariantCulture); }
                        catch (Exception e)
                        {
                            _logger.LogError("Error with LastUpdated: " + item.LastUpdated, e);
                        }

                        foreach (var distribution in item.DistributionsFormats)
                        {
                            SimpleDistribution simpleDistribution = new SimpleDistribution();
                            simpleDistribution.Organization = distribution.Organization;
                            simpleDistribution.Protocol = "W3C:AtomFeed";
                            simpleDistribution.URL = item.Url;
                            simpleDistribution.FormatName = distribution.FormatName;

                            metadataInfo.Distributions.Add(simpleDistribution);
                        }

                        metadataInfo.Projections = new List<SimpleReferenceSystem>();

                        foreach (var projection in item.ReferenceSystems)
                        {
                            SimpleReferenceSystem simpleReferenceSystem = new SimpleReferenceSystem();
                            simpleReferenceSystem.CoordinateSystem = projection.CoordinateSystem;


                            metadataInfo.Projections.Add(simpleReferenceSystem);
                        }
                    }

                    string server = _settings["GeoNetworkUrl"];
                    string usernameGeonetwork = _settings["GeoNetworkUsername"];
                    string password = _settings["GeoNetworkPassword"];
                    string geonorgeUsername = _settings["GeonorgeUsername"];


                    GeoNorge api = new GeoNorge(usernameGeonetwork, password, server);
                    api.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
                    api.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);

                    var metadata = api.GetRecordByUuid(metadataInfo.Uuid);

                    SimpleMetadata simpleMetadata = new SimpleMetadata(metadata);

                    List<SimpleDistribution> distributionFormats = simpleMetadata.DistributionsFormats;

                    List<SimpleDistribution> distributionFormatsUpdated = new List<SimpleDistribution>();

                    if (HasAtomFeed(metadataInfo.Distributions))
                    {

                        foreach (var distribution in distributionFormats)
                        {
                            if (distribution.Protocol != "W3C:AtomFeed")
                            {
                                distributionFormatsUpdated.Add(distribution);
                            }
                        }

                    }
                    else
                    {
                        distributionFormatsUpdated = distributionFormats;
                    }

                    distributionFormatsUpdated.AddRange(metadataInfo.Distributions);

                    simpleMetadata.DistributionsFormats = distributionFormatsUpdated;
                    simpleMetadata.DistributionDetails = new SimpleDistributionDetails
                    {
                        URL = distributionFormatsUpdated[0].URL,
                        Protocol = distributionFormatsUpdated[0].Protocol,
                        UnitsOfDistribution = distributionFormatsUpdated[0].UnitsOfDistribution
                    };

                    List<SimpleReferenceSystem> simpleReferenceSystems = new List<SimpleReferenceSystem>();
                    foreach (var projection in metadataInfo.Projections)
                    {
                        SimpleReferenceSystem refsys = new SimpleReferenceSystem();
                        refsys.CoordinateSystem = projection.CoordinateSystem;
                        simpleReferenceSystems.Add(refsys);
                    }
                    if (simpleMetadata.ReferenceSystems == null
                        || (simpleMetadata.ReferenceSystems != null && simpleMetadata.ReferenceSystems.Count == 0))
                        simpleMetadata.ReferenceSystems = simpleReferenceSystems;

                    simpleMetadata.DateMetadataUpdated = DateTime.Now;

                    if (metadataInfo.DatasetDateUpdated.HasValue)
                        simpleMetadata.DateUpdated = metadataInfo.DatasetDateUpdated;

                    api.MetadataUpdate(simpleMetadata.GetMetadata(), CreateAdditionalHeadersWithUsername(geonorgeUsername, "true"));
                    _logger.LogInformation($"Metadata updated for uuid: {metadataInfo.Uuid}");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error updating metadata uuid: " + dataset.Uuid + ", error: " + ex);
                }
            }

            return Task.CompletedTask;
        }
        public List<Dataset> GetDatasets()
        {
            List<Dataset> geonorgeDatasets;
            List<Dataset> nguDatasets;
            List<Dataset> nibioDatasets;
            List<Dataset> miljodirektoratetDatasets;

            geonorgeDatasets = GetDatasetsFromUrl("https://nedlasting.geonorge.no/geonorge/Tjenestefeed_daglig.xml");
            nguDatasets = GetDatasetsFromUrl("https://nedlasting.ngu.no/api/atomfeeds");
            nibioDatasets = GetDatasetsFromUrl("https://kartkatalog.nibio.no/api/atomfeeds");
            miljodirektoratetDatasets = GetDatasetsFromUrl("https://nedlasting.miljodirektoratet.no/miljodata/ATOM/Atom_TjenesteFeed.xml");

            return geonorgeDatasets.Concat(nguDatasets).Concat(nibioDatasets).Concat(miljodirektoratetDatasets).ToList();
        }

        public List<Dataset> GetDatasetsFromUrl(string url)
        {
            try
            {
                var getFeedTask = HttpClient.GetStringAsync(url);
                _logger.LogDebug("Fetch datasets from " + url);
                return _atomFeedParser.ParseDatasets(getFeedTask.Result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting dataset from url: " + url + " . Error: " + ex);
            }

            return new List<Dataset>();
        }

        private bool HasAtomFeed(List<SimpleDistribution> distributions)
        {
            for (int i = 0; i < distributions.Count; i++)
            {
                if (distributions[i].Protocol == "W3C:AtomFeed")
                {
                    return true;
                }
            }
            return false;
        }

        private void LogEventsDebug(string log)
        {

            _logger.LogDebug(log);
        }

        private void LogEventsError(string log, Exception ex)
        {
            _logger.LogError(log, ex);
        }

        public Dictionary<string, string> CreateAdditionalHeadersWithUsername(string username, string published = "")
        {
            Dictionary<string, string> header = new Dictionary<string, string> { { "GeonorgeUsername", username } };

            header.Add("GeonorgeOrganization", "Kartverket");
            header.Add("GeonorgeRole", "nd.metadata_admin");
            header.Add("published", published);

            return header;
        }
    }

    public class UpdateMetadataInformation
    {
        public string Uuid { get; set; }
        public List<SimpleDistribution> Distributions { get; set; }
        public List<SimpleReferenceSystem> Projections { get; set; }
        public DateTime? DatasetDateUpdated { get; set; }

    }

    public class Dataset
    {
        /// <summary>
        /// Dataset uuid
        /// </summary>
        public string Uuid { get; set; }

        /// <summary>
        /// Url to dataset in feed
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Date when dataset was updated
        /// </summary>
        public string LastUpdated { get; set; }

        /// <summary>
        /// Owner of dataset
        /// </summary>
        public string Organization { get; set; }

        public List<DatasetFile> DatasetFiles { get; set; }

        public List<SimpleDistribution> DistributionsFormats { get; set; }

        public List<SimpleReferenceSystem> ReferenceSystems { get; set; }
    }
}