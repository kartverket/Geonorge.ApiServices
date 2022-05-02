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
            var datasets = GetDatasets();

            foreach (var dataset in datasets)
            {
                try
                {

                    System.Collections.Specialized.NameValueCollection settings = System.Web.Configuration.WebConfigurationManager.AppSettings;
                    string server = settings["GeoNetworkUrl"];
                    string usernameGeonetwork = settings["GeoNetworkUsername"];
                    string password = settings["GeoNetworkPassword"];
                    string geonorgeUsername = settings["GeonorgeUsername"];


                    GeoNorge api = new GeoNorge(usernameGeonetwork, password, server);
                    api.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
                    api.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);

                    SimpleMetadata simpleMetadata = null;

                    bool insertMetadata = false;

                    try
                    {
                        var metadata = api.GetRecordByUuid(dataset.Uuid);
                        simpleMetadata = new SimpleMetadata(metadata);
                    }
                    catch 
                    {
                        simpleMetadata = SimpleMetadata.CreateDataset(dataset.Uuid);
                        insertMetadata = true;
                    }

                    if (dataset.DateMetadataUpdated.HasValue)
                        simpleMetadata.DateMetadataUpdated = dataset.DateMetadataUpdated.Value;

                    if (dataset.LastUpdated.HasValue)
                        simpleMetadata.DateUpdated = dataset.LastUpdated.Value;

                    if (!string.IsNullOrEmpty(dataset.StartDate) && !string.IsNullOrEmpty(dataset.StopDate)) 
                    { 
                        simpleMetadata.ValidTimePeriod = new SimpleValidTimePeriod 
                        { 
                            ValidFrom = dataset.StartDate ,
                            ValidTo = dataset.StopDate
                        } ;
                    }


                    simpleMetadata.Title = dataset.Title;
                    simpleMetadata.Abstract = dataset.Abstract;
                    simpleMetadata.ContactPublisher = new SimpleContact
                    {
                        Name = dataset.OrganizationPersonnelName,
                        Email = dataset.OrganizationPersonnelEmail,
                        Organization = dataset.Organization,
                        Role = "publisher"
                    };


                    if (!string.IsNullOrWhiteSpace(dataset.BBoxEastBoundLongitude))
                    {
                        if (dataset.BBoxWestBoundLongitude == null)
                            dataset.BBoxWestBoundLongitude = dataset.BBoxEastBoundLongitude;

                        if (dataset.BBoxSouthBoundLatitude == null)
                            dataset.BBoxSouthBoundLatitude = dataset.BBoxNorthBoundLatitude;

                        simpleMetadata.BoundingBox = new SimpleBoundingBox
                        {
                            EastBoundLongitude = dataset.BBoxEastBoundLongitude,
                            WestBoundLongitude = dataset.BBoxWestBoundLongitude,
                            NorthBoundLatitude = dataset.BBoxNorthBoundLatitude,
                            SouthBoundLatitude = dataset.BBoxSouthBoundLatitude
                        };
                    }

                    if(!string.IsNullOrEmpty(dataset.ProcessHistory))
                        simpleMetadata.ProcessHistory = dataset.ProcessHistory;

                    if (!string.IsNullOrEmpty(dataset.Status))
                        simpleMetadata.Status = dataset.Status;

                    if (!string.IsNullOrEmpty(dataset.MetadataName))
                        simpleMetadata.MetadataStandard = dataset.MetadataName;

                    if (!string.IsNullOrEmpty(dataset.MetadataVersion))
                        simpleMetadata.MetadataStandardVersion = dataset.MetadataVersion;

                    if(dataset.TopicCategories != null && dataset.TopicCategories.Count > 0) 
                        simpleMetadata.TopicCategories = dataset.TopicCategories;

                    //List<SimpleDistribution> distributionFormats = simpleMetadata.DistributionsFormats;

                    //List<SimpleDistribution> distributionFormatsUpdated = new List<SimpleDistribution>();

                    //distributionFormatsUpdated = distributionFormats;

                    //distributionFormatsUpdated.AddRange(metadataInfo.Distributions);

                    //simpleMetadata.DistributionsFormats = distributionFormatsUpdated;
                    //simpleMetadata.DistributionDetails = new SimpleDistributionDetails
                    //{
                    //    URL = distributionFormatsUpdated[0].URL,
                    //    Protocol = distributionFormatsUpdated[0].Protocol,
                    //    UnitsOfDistribution = distributionFormatsUpdated[0].UnitsOfDistribution
                    //};

                    //List<SimpleReferenceSystem> simpleReferenceSystems = new List<SimpleReferenceSystem>();
                    //foreach (var projection in metadataInfo.Projections)
                    //{
                    //    SimpleReferenceSystem refsys = new SimpleReferenceSystem();
                    //    refsys.CoordinateSystem = projection.CoordinateSystem;
                    //    simpleReferenceSystems.Add(refsys);
                    //}
                    //if (simpleMetadata.ReferenceSystems == null
                    //    || (simpleMetadata.ReferenceSystems != null && simpleMetadata.ReferenceSystems.Count == 0))
                    //    simpleMetadata.ReferenceSystems = simpleReferenceSystems;

                    simpleMetadata.Keywords = dataset.GetAllKeywords();

                    simpleMetadata.DateMetadataUpdated = DateTime.Now;


                    //if (insertMetadata)
                    //    api.MetadataInsert(simpleMetadata.GetMetadata(), CreateAdditionalHeadersWithUsername(geonorgeUsername, "true"));
                    //else
                    //    api.MetadataUpdate(simpleMetadata.GetMetadata(), CreateAdditionalHeadersWithUsername(geonorgeUsername, "true"));

                    Log.Info($"Metadata updated for uuid: {dataset.Uuid}");
                }
                catch (Exception ex)
                {
                    Log.Error("Error updating metadata uuid: " + dataset.Uuid + ", error: " + ex);
                }
            }

            return Task.CompletedTask;
        }
        public List<DatasetNMDC> GetDatasets()
        {
            List<DatasetNMDC> mareanoDatasets;

            mareanoDatasets = GetDatasetsFromUrl("http://metadata.nmdc.no/OAIPMH-Provider/request/oaipmh?verb=ListRecords&metadataPrefix=dif&project=MAREANO");
            
            return mareanoDatasets.ToList();
        }

        public List<DatasetNMDC> GetDatasetsFromUrl(string url)
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

            return new List<DatasetNMDC>();
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

    public class DatasetNMDC
    {
        /// <summary>
        /// Dataset uuid
        /// </summary>
        public string Uuid { get; set; }

        /// <summary>
        /// Dataset title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Dataset abstract
        /// </summary>
        public string Abstract { get; set; }

        /// <summary>
        /// Date when dataset was updated
        /// </summary>
        public DateTime? LastUpdated { get; set; }


        /// <summary>
        /// Date when metadata was updated
        /// </summary>
        public DateTime? DateMetadataUpdated { get; set; }

        public string StartDate { get; set; }
        public string StopDate { get; set; }

        /// <summary>
        /// Owner of dataset
        /// </summary>
        public string Organization { get; set; }
        public string OrganizationPersonnelName { get; set; }
        public string OrganizationPersonnelEmail { get; set; }

        public string BBoxEastBoundLongitude { get; set; }
        public string BBoxNorthBoundLatitude { get; set; }
        public string BBoxSouthBoundLatitude { get; set; }
        public string BBoxWestBoundLongitude { get; set; }

        public string ProcessHistory { get; set; }

        public string Status { get; set; }

        public List<SimpleDistribution> DistributionsFormats { get; set; }

        public List<SimpleReferenceSystem> ReferenceSystems { get; set; }
        public string MetadataName { get; internal set; }
        public string MetadataVersion { get; internal set; }
        public List<string> TopicCategories { get; internal set; }

        public List<String> KeywordsTheme { get; set; }
        public List<String> KeywordsNationalTheme { get; set; }
        public List<string> KeywordsGlobalChangeMasterDirectory { get; set; }

        internal List<SimpleKeyword> GetAllKeywords()
        {
            List<SimpleKeyword> allKeywords = new List<SimpleKeyword>();

            allKeywords.AddRange(CreateKeywords(KeywordsTheme, "Theme", SimpleKeyword.TYPE_THEME, null));
            allKeywords.AddRange(CreateKeywords(KeywordsNationalTheme, "NationalTheme", null, SimpleKeyword.THESAURUS_NATIONAL_THEME));
            allKeywords.AddRange(CreateKeywords(KeywordsGlobalChangeMasterDirectory, "GlobalChangeMasterDirectory", null, SimpleKeyword.THESAURUS_GLOBAL_CHANGE_MASTER_DIRECTORY));

            return allKeywords;
        }

        internal List<SimpleKeyword> CreateKeywords(List<string> inputList, string prefix, string type = null, string thesaurus = null)
        {
            List<SimpleKeyword> output = new List<SimpleKeyword>();

            if (inputList != null)
            {
                inputList = inputList.Distinct().ToList();

                foreach (var keyword in inputList)
                {
                    string keywordString = keyword;
                    string keywordLink = null;
                    if (keyword.Contains("|"))
                    {
                        keywordString = keyword.Split('|')[0];
                        keywordLink = keyword.Split('|')[1];
                        if (!keywordLink.StartsWith("http")) 
                        {
                            keywordString = keyword;
                            keywordLink = null;
                        }
                    }

                    output.Add(new SimpleKeyword
                    {
                        Keyword = keywordString,
                        KeywordLink = keywordLink,
                        Thesaurus = thesaurus,
                        Type = type,
                        EnglishKeyword = keywordString
                    });
                }
            }
            return output;
        }
    }

}