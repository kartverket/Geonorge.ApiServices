using GeoNorgeAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using www.opengis.net;
using Newtonsoft.Json.Linq;

namespace Kartverket.Geonorge.Api.Services
{
    public class DownloadService
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        //string geoNetworkendPoint = "srv/nor/csw-dataset?";

        GeoNorge geoNorge = new GeoNorge("", "", WebConfigurationManager.AppSettings["GeoNetworkUrl"]);

        SearchResultsType metadataSets;

        List<MetadataEntry> downloadProblems = new List<MetadataEntry>();

        public List<MetadataEntry> CheckDownload()
        {        
            metadataSets = GetDatasets();
            CheckDatasets();

            return downloadProblems;
        }

        private void CheckDatasets()
        {

            for (int d = 0; d < metadataSets.Items.Length; d++)
            {
                string uuid = ((www.opengis.net.DCMIRecordType)(metadataSets.Items[d])).Items[0].Text[0];
                MD_Metadata_Type md = geoNorge.GetRecordByUuid(uuid);
                var data = new SimpleMetadata(md);

                if ( data.DistributionDetails != null && !string.IsNullOrEmpty(data.DistributionDetails.Protocol)
                     && data.DistributionDetails.Protocol == "GEONORGE:DOWNLOAD")
                {
                    if(string.IsNullOrEmpty(data.DistributionDetails.URL))
                        downloadProblems.Add(new MetadataEntry { Uuid = uuid, Title = data.Title, Problem = "Distribusjons url er tom"});
                    else
                    GetDistribution(uuid, data.Title , data.DistributionDetails.URL);
                }
            }
        }

        private void GetDistribution(string uuid, string title, string uRL)
        {
            System.Net.WebClient c = new System.Net.WebClient();
            c.Encoding = System.Text.Encoding.UTF8;
            try
            {
                var data = c.DownloadString(uRL+uuid);
                var response = Newtonsoft.Json.Linq.JObject.Parse(data);
                var supportsPolygonSelection = response.SelectToken("supportsPolygonSelection");
                if (supportsPolygonSelection != null && !(bool)supportsPolygonSelection) { 
                    var links = response.SelectToken("_links").ToList();
                    foreach (var url in links)
                    {
                        if (url["rel"].ToString() == "http://rel.geonorge.no/download/area")
                            checkArea(uuid, title, url["href"].ToString());
                    }
                }
            } 
            catch(Exception ex)
            {
                downloadProblems.Add(new MetadataEntry { Uuid = uuid, Title = title, Problem = "Capabilities mangler" });
            }
        }

        private void checkArea(string uuid, string title, string uRL)
        {
            System.Net.WebClient cc = new System.Net.WebClient();
            cc.Encoding = System.Text.Encoding.UTF8;
            try
            {
                var dataArea = cc.DownloadString(uRL);
                var responseArea = Newtonsoft.Json.Linq.JArray.Parse(dataArea);
                if (!responseArea.HasValues)
                    throw new Exception("Fildata mangler");

            }
            catch (Exception ex)
            {
                downloadProblems.Add(new MetadataEntry { Uuid = uuid, Title = title, Problem = "Fildata mangler" });
            }
        }

        public SearchResultsType GetDatasets()
        {
            GeoNorge _geoNorge = new GeoNorge("", "", WebConfigurationManager.AppSettings["GeoNetworkUrl"]);
            _geoNorge.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
            _geoNorge.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);
            var filters = new object[]
            {
                    new PropertyIsLikeType
                        {
                            escapeChar = "\\",
                            singleChar = "_",
                            wildCard = "%",
                            PropertyName = new PropertyNameType {Text = new[] {"protocol"}},
                            Literal = new LiteralType {Text = new[] {"GEONORGE:DOWNLOAD"}}
                        }
            };

            var filterNames = new ItemsChoiceType23[]
            {
                        ItemsChoiceType23.PropertyIsLike,
            };

            var result = _geoNorge.SearchWithFilters(filters, filterNames, 1, 1000, false);
            return result;
        }

        private void LogEventsDebug(string log)
        {

            System.Diagnostics.Debug.Write(log);
            Log.Debug(log);
        }

        private void LogEventsError(string log, Exception ex)
        {
            Log.Error(log, ex);
        }
    }

    public class MetadataEntry
    {
        public string Uuid { get; set; }
        public string Title { get; set; }
        public string Problem { get; set; }
        public DateTime DateChecked { get; set; } = DateTime.Now;
    }
}