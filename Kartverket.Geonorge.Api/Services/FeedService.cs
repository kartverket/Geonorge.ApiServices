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
                       dataset.Organization
                   })
               }).ToList();

            foreach(var dataset in datasets)
            {
                var uuid = dataset.Uuid;
                foreach (var item in dataset.Datasets)
                {
                    var url = item.Url;
                    var organization = item.Organization;

                    //Distribusjonstype=W3C:AtomFeed
                    //Format: gml 3.2.1?
                }

                //todo save to geonetwork
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