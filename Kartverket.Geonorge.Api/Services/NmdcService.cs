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
    public interface INmdcService
    {
        Task UpdateMetadata();
    }

    public class NmdcService : INmdcService
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
                       dataset.LastUpdated,
                       dataset.DistributionsFormats,
                       dataset.ReferenceSystems
                   })
               }).ToList();

            //foreach(var dataset in datasets)
            //{
            //    try
            //    { 
            //    var metadataInfo = new UpdateMetadataInformation();
            //    metadataInfo.Uuid = dataset.Uuid.Trim();
            //    metadataInfo.Distributions = new List<SimpleDistribution>();


            //    foreach (var item in dataset.Datasets)
            //        {
            //            try
            //            { metadataInfo.DatasetDateUpdated = DateTime.Parse(item.LastUpdated, System.Globalization.CultureInfo.InvariantCulture); }
            //            catch (Exception e)
            //            {
            //                Log.Error("Error with LastUpdated: " + item.LastUpdated, e);
            //            }

            //        foreach (var distribution in item.DistributionsFormats)
            //        { 
            //            SimpleDistribution simpleDistribution = new SimpleDistribution();
            //            simpleDistribution.Organization = distribution.Organization;
            //            simpleDistribution.Protocol = distribution.Protocol;
            //            simpleDistribution.URL = item.Url;
            //            simpleDistribution.FormatName = distribution.FormatName;

            //            metadataInfo.Distributions.Add(simpleDistribution);
            //        }

            //            metadataInfo.Projections = new List<SimpleReferenceSystem>();

            //            foreach (var projection in item.ReferenceSystems)
            //            {
            //                SimpleReferenceSystem simpleReferenceSystem = new SimpleReferenceSystem();
            //                simpleReferenceSystem.CoordinateSystem = projection.CoordinateSystem;
                           

            //                metadataInfo.Projections.Add(simpleReferenceSystem);
            //            }
            //        }

            //    System.Collections.Specialized.NameValueCollection settings = System.Web.Configuration.WebConfigurationManager.AppSettings;
            //    string server = settings["GeoNetworkUrl"];
            //    string usernameGeonetwork = settings["GeoNetworkUsername"];
            //    string password = settings["GeoNetworkPassword"];
            //    string geonorgeUsername = settings["GeonorgeUsername"];


            //    GeoNorge api = new GeoNorge(usernameGeonetwork, password, server);
            //    api.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
            //    api.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);

            //    var metadata = api.GetRecordByUuid(metadataInfo.Uuid);

            //    SimpleMetadata simpleMetadata = new SimpleMetadata(metadata);

            //    List<SimpleDistribution> distributionFormats = simpleMetadata.DistributionsFormats;

            //    List<SimpleDistribution> distributionFormatsUpdated = new List<SimpleDistribution>();

            //    distributionFormatsUpdated = distributionFormats;

            //    distributionFormatsUpdated.AddRange(metadataInfo.Distributions);

            //    simpleMetadata.DistributionsFormats = distributionFormatsUpdated;
            //    simpleMetadata.DistributionDetails = new SimpleDistributionDetails
            //    {
            //        URL = distributionFormatsUpdated[0].URL,
            //        Protocol = distributionFormatsUpdated[0].Protocol,
            //        UnitsOfDistribution = distributionFormatsUpdated[0].UnitsOfDistribution
            //    };

            //        List<SimpleReferenceSystem> simpleReferenceSystems = new List<SimpleReferenceSystem>();
            //        foreach (var projection in metadataInfo.Projections)
            //        {
            //            SimpleReferenceSystem refsys = new SimpleReferenceSystem();
            //            refsys.CoordinateSystem = projection.CoordinateSystem;
            //            simpleReferenceSystems.Add(refsys);
            //        }
            //        if(simpleMetadata.ReferenceSystems == null 
            //            || (simpleMetadata.ReferenceSystems != null && simpleMetadata.ReferenceSystems.Count == 0))
            //            simpleMetadata.ReferenceSystems = simpleReferenceSystems;

            //    simpleMetadata.DateMetadataUpdated = DateTime.Now;

            //        if (metadataInfo.DatasetDateUpdated.HasValue)
            //            simpleMetadata.DateUpdated = metadataInfo.DatasetDateUpdated;

            //    //api.MetadataUpdate(simpleMetadata.GetMetadata(), CreateAdditionalHeadersWithUsername(geonorgeUsername, "true"));
            //    Log.Info($"Metadata updated for uuid: {metadataInfo.Uuid}");
            //    }
            //    catch(Exception ex)
            //    {
            //        Log.Error("Error updating metadata uuid: " + dataset.Uuid + ", error: " + ex);
            //    }
            //}

            return Task.CompletedTask;
        }
        public List<Dataset> GetDatasets()
        {
            List<Dataset> mareanoDatasets;

            mareanoDatasets = GetDatasetsFromUrl("http://metadata.nmdc.no/OAIPMH-Provider/request/oaipmh?verb=ListRecords&metadataPrefix=dif&project=MAREANO");
            
            return mareanoDatasets.ToList();
        }

        public List<Dataset> GetDatasetsFromUrl(string url)
        {
            try
            {
                var getFeedTask = HttpClient.GetStringAsync(url);
                Log.Debug("Fetch datasets from " + url);
                return new NmdcParser().ParseDatasets(getFeedTask.Result);
            }
            catch (Exception ex)
            {
                Log.Error("Error getting dataset from url: " + url + " . Error: " + ex);
            }

            return new List<Dataset>();
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
}