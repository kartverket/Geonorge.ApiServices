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

namespace Kartverket.Geonorge.Api.Services
{
    public class ConstraintChecker
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        string kartkatalogenUrl = WebConfigurationManager.AppSettings["KartkatalogenUrl"];
        string downloadUrl = WebConfigurationManager.AppSettings["DownloadUrl"];

        IEnumerable<JToken> metadataSets;
        IEnumerable<JToken> downloadSets;

        List<MetadataEntry> metadataProblems = new List<MetadataEntry>();

        public List<MetadataEntry> Check()
        {
            try
            {
                metadataSets = GetMetadata();
                downloadSets = GetDownload();
                CheckConstraints();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            return metadataProblems;
        }

        private void CheckConstraints()
        {
            foreach (var downloadSet in downloadSets.ToList())
            {
                string downloadAccess = "";
                JToken downloadAccessObject = downloadSet["AccessConstraint"];
                if (downloadAccessObject != null)
                {
                    downloadAccess = downloadAccessObject.ToString();
                }

                var metadataSet = metadataSets.FirstOrDefault(d => (string)d["Uuid"] == (string)downloadSet["metadataUuid"]);
                JToken metadataAccessObject = null;
                string metadataAccess = "";
                if (metadataSet != null)
                {
                    metadataAccessObject = metadataSet["DataAccess"];
                }

                if (metadataAccessObject != null)
                    metadataAccess = metadataAccessObject.ToString();

                if (metadataAccess != downloadAccess)
                    metadataProblems.Add(new MetadataEntry { Uuid = downloadSet["metadataUuid"].ToString(), Title = downloadSet["Tittel"].ToString(), Problem = "Kartkatalogen har tilgangsrestriksjon: " + metadataAccess + ", nedlasting: " + downloadAccess });
            }

        }

        public IEnumerable<JToken> GetMetadata()
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

            var data1 = c.DownloadString(kartkatalogenUrl + "api/search?limit=10000&facets[0]name=type&facets[0]value=dataset");
            var response1 = Newtonsoft.Json.Linq.JObject.Parse(data1);
            var result = response1.SelectToken("Results").ToList();

            return result;
        }

        public IEnumerable<JToken> GetDownload()
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

            var data1 = c.DownloadString(downloadUrl + "api/internal/dataset");
            var response1 = Newtonsoft.Json.Linq.JArray.Parse(data1);
            var result = response1.Root.ToList();

            return result;
        }

    }

}