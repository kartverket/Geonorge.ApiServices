using GeoNorgeAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using www.opengis.net;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net;
using System.IO;
using System.Text;
using System.Runtime.Caching;
using System.Security.Policy;

namespace Kartverket.Geonorge.Api.Services
{
    public class MetadataChecker
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        string kartkatalogenUrl = WebConfigurationManager.AppSettings["KartkatalogenUrl"];

        IEnumerable<JToken> metadataSets;

        public List<MetadataEntry> metadataProblems = new List<MetadataEntry>();

        public void Check()
        {
            Log.Info("Start checking mismatch search index and metadata in geonetwork");
            metadataSets = GetSearchMetadata();
            CheckMetadata();
            MemoryCache memoryCache = MemoryCache.Default;
            memoryCache.Add("MetadataErrors", metadataProblems, new DateTimeOffset(DateTime.Now.AddDays(7)));
            Log.Info("End checking mismatch search index and metadata in geonetwork");
        }

        private void CheckMetadata()
        {
            System.Collections.Specialized.NameValueCollection settings = System.Web.Configuration.WebConfigurationManager.AppSettings;
            string server = settings["GeoNetworkUrl"];
            string usernameGeonetwork = settings["GeoNetworkUsername"];
            string password = settings["GeoNetworkPassword"];
            string geonorgeUsername = settings["GeonorgeUsername"];

            GeoNorge api = new GeoNorge(usernameGeonetwork, password, server);
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
                    if(metadataGeonetwork == null)
                        throw new Exception("Metadata not found in geonetwork");
                }
                catch (Exception ex)
                {
                    metadataProblems.Add(new MetadataEntry { Uuid = uuid, Title = title, Problem = "Uuid finnes ikke geonetwork" });
                    Log.Error("Uuid finnes ikke geonetwork: " + uuid + ", title: " + title, ex);
                    System.Diagnostics.Debug.WriteLine("Uuid finnes ikke geonetwork: " + uuid + "title: " + title);
                }
            }

        }

        public IEnumerable<JToken> GetSearchMetadata()
        {
            //Disable SSL sertificate errors
            System.Net.ServicePointManager.ServerCertificateValidationCallback +=
            delegate (object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate,
                                    System.Security.Cryptography.X509Certificates.X509Chain chain,
                                    System.Net.Security.SslPolicyErrors sslPolicyErrors)
            {
                return true; // **** Always accept
            };
            System.Net.WebClient c = new System.Net.WebClient();
            c.Encoding = System.Text.Encoding.UTF8;

            var data1 = c.DownloadString(kartkatalogenUrl + "api/search?limit=10000");
            var response1 = Newtonsoft.Json.Linq.JObject.Parse(data1);
            var result = response1.SelectToken("Results").ToList();


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

}