using GeoNorgeAPI;
using Kartverket.Geonorge.Utilities.Organization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Xml;
using www.opengis.net;
using Kartverket.Geonorge.Api.Models;
using HttpClientFactory = Kartverket.Geonorge.Utilities.Organization.HttpClientFactory;
using System.Linq;

namespace Kartverket.Geonorge.Api.Services
{
    public interface IFeedService
    {
        Task UpdateMetadata();
    }

    public class FeedService : IFeedService
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly HttpClient HttpClient = new HttpClient();

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
                       dataset.LastUpdated
                   })
               }).ToList();

            foreach(var dataset in datasets)
            {
                try
                { 
                var metadataInfo = new UpdateMetadataInformation();
                metadataInfo.Uuid = dataset.Uuid;
                metadataInfo.Distributions = new List<SimpleDistribution>();
                foreach (var item in dataset.Datasets)
                {
                    //metadataInfo.DatasetDateUpdated = Convert.ToDateTime(item.LastUpdated);

                    SimpleDistribution simpleDistribution = new SimpleDistribution();
                    simpleDistribution.Organization = item.Organization;
                    simpleDistribution.Protocol = "W3C:AtomFeed";
                    simpleDistribution.URL = item.Url;

                    metadataInfo.Distributions.Add(simpleDistribution);
                }

                System.Collections.Specialized.NameValueCollection settings = System.Web.Configuration.WebConfigurationManager.AppSettings;
                string server = settings["GeoNetworkUrl"];
                string usernameGeonetwork = settings["GeoNetworkUsername"];
                string password = settings["GeoNetworkPassword"];
                string geonorgeUsername = settings["GeonorgeUsername"];


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

                distributionFormatsUpdated.InsertRange(0, metadataInfo.Distributions);

                simpleMetadata.DistributionsFormats = distributionFormatsUpdated;
                simpleMetadata.DistributionDetails = new SimpleDistributionDetails
                {
                    URL = distributionFormatsUpdated[0].URL,
                    Protocol = distributionFormatsUpdated[0].Protocol,
                    UnitsOfDistribution = distributionFormatsUpdated[0].UnitsOfDistribution
                };


                simpleMetadata.DateMetadataUpdated = DateTime.Now;

                if (metadataInfo.DatasetDateUpdated.HasValue)
                    simpleMetadata.DateUpdated = metadataInfo.DatasetDateUpdated;

                api.MetadataUpdate(simpleMetadata.GetMetadata(), CreateAdditionalHeadersWithUsername(geonorgeUsername, "true"));
                Log.Info($"Metadata updated for uuid: {metadataInfo.Uuid}");
                }
                catch(Exception ex)
                {
                    Log.Error("Error updating metadata" + ex);
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
                Log.Debug("Fetch datasets from " + url);
                return new AtomFeedParser().ParseDatasets(getFeedTask.Result);
            }
            catch (Exception ex)
            {
                Log.Error("Error getting dataset from url: " + url + " . Error: " + ex);
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

            Log.Debug(log);
        }

        private void LogEventsError(string log, Exception ex)
        {
            Log.Error(log, ex);
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
    }
}