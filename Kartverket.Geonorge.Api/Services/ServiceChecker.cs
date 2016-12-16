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

namespace Kartverket.Geonorge.Api.Services
{
    public class ServiceChecker
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        string kartkatalogenUrl = WebConfigurationManager.AppSettings["KartkatalogenUrl"];

        IEnumerable<JToken> metadataSets;

        public List<MetadataEntry> serviceProblems = new List<MetadataEntry>();

        public void Check()
        {
            metadataSets = GetServices();
            CheckServices();
            MemoryCache memoryCache = MemoryCache.Default;
            memoryCache.Add("ServiceErrors", serviceProblems, new DateTimeOffset(DateTime.Now.AddDays(7)));
        }

        private void CheckServices()
        {
            foreach (var metadata in metadataSets.ToList())
            {
                System.Diagnostics.Debug.WriteLine(metadata["Title"]);

                string distributionUrl= metadata["DistributionUrl"] != null ? metadata["DistributionUrl"].ToString() : "";
                string distributionProtocol = metadata["DistributionProtocol"] != null ? metadata["DistributionProtocol"].ToString() : "";

                if (!string.IsNullOrEmpty(distributionUrl))
                { 
                    Uri uriResult;
                    bool validUri = Uri.TryCreate(distributionUrl, UriKind.Absolute, out uriResult)
                        && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                    if (!validUri)
                    {
                        System.Diagnostics.Debug.WriteLine("Feil format på url");
                        serviceProblems.Add(new MetadataEntry { Uuid = metadata["Uuid"].ToString(), Title = metadata["Title"].ToString(), Problem = "Feil format på url: " + distributionUrl });
                    }
                    else
                    {
                        string url = "";
                        using (var client = new HttpClient { Timeout = new TimeSpan(0, 0, 20) })
                        {
                            client.DefaultRequestHeaders.Accept.Clear();
                            try
                            {
                                url = DistributionDetailsGetCapabilitiesUrl(distributionUrl, distributionProtocol);
                                if (shouldCheckUrl(url))
                                {
                                    System.Diagnostics.Debug.WriteLine(url);
                                    HttpResponseMessage response = client.GetAsync(new Uri(url)).Result;

                                    if (response.StatusCode != HttpStatusCode.OK)
                                    {
                                        System.Diagnostics.Debug.WriteLine("Http status kode:" + response.StatusCode);
                                        serviceProblems.Add(new MetadataEntry { Uuid = metadata["Uuid"].ToString(), Title = metadata["Title"].ToString(), Problem = "Http feil statuskode: " + response.StatusCode + ", " + url });
                                    }
                                    else
                                    {
                                        var text = response.Content.ReadAsStringAsync().Result;
                                        System.Diagnostics.Debug.WriteLine(text);
                                        if(text.Contains("<ServiceExceptionReport"))
                                            serviceProblems.Add(new MetadataEntry { Uuid = metadata["Uuid"].ToString(), Title = metadata["Title"].ToString(), Problem = "Tjenesten returnerer xml unntak: " + url });
                                        else if(!text.Contains("<?xml") && text.Contains("<html"))
                                            serviceProblems.Add(new MetadataEntry { Uuid = metadata["Uuid"].ToString(), Title = metadata["Title"].ToString(), Problem = "Tjenesten returnerer ikke xml: " + url });

                                    }

                                }
                            }
                            catch (Exception ex)
                            {
                                serviceProblems.Add(new MetadataEntry { Uuid = metadata["Uuid"].ToString(), Title = metadata["Title"].ToString(), Problem = "Kunne ikke koble opp mot: " + url });
                            }
                        }
                    } 
                }
                else{ System.Diagnostics.Debug.WriteLine("Distribusjons url er tom");
                    serviceProblems.Add(new MetadataEntry { Uuid = metadata["Uuid"].ToString(), Title = metadata["Title"].ToString(), Problem = "Distribusjons url er tom" });
                }

                System.Diagnostics.Debug.WriteLine("-------------------------------------");
            }

            System.Diagnostics.Debug.WriteLine(metadataSets.Count());

        }

        private bool shouldCheckUrl(string url)
        {
            if (url.Contains("gatekeeper1")) // Tjenester som ikke kan vises uten at en sender med "token".
                return false;
            else if (url.Contains("www.nd.matrikkel.no")) //Denne krever pålogging.
                return false;
            else
                return true;
        }

        public IEnumerable<JToken> GetServices()
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

            var data1 = c.DownloadString(kartkatalogenUrl + "api/search?limit=1000&facets[0]name=type&facets[0]value=service&facets[1]name=DistributionProtocols&facets[1]value=WMS-tjeneste");
            var response1 = Newtonsoft.Json.Linq.JObject.Parse(data1);
            var result1 = response1.SelectToken("Results").ToList();

            var data2 = c.DownloadString(kartkatalogenUrl + "api/search?limit=1000&facets[0]name=type&facets[0]value=service&facets[1]name=DistributionProtocols&facets[1]value=WFS-tjeneste");
            var response2 = Newtonsoft.Json.Linq.JObject.Parse(data2);
            var result2 = response2.SelectToken("Results").ToList();

            //var data3 = c.DownloadString(kartkatalogenUrl + "api/search?limit=1000&facets[0]name=type&facets[0]value=servicelayer&facets[1]name=DistributionProtocols&facets[1]value=WMS-tjeneste");
            //var response3 = Newtonsoft.Json.Linq.JObject.Parse(data3);
            //var result3 = response3.SelectToken("Results").ToList();
            

            var result = result1.Concat(result2);


            return result;
        }

        public String DistributionDetailsGetCapabilitiesUrl(string URL, string distributionProtocol)
        {

                string tmp = URL;
                int startQueryString = tmp.IndexOf("?");

                if (startQueryString != -1)
                    tmp = tmp.Substring(0, startQueryString + 1);
                else
                    tmp = tmp + "?";

                if (distributionProtocol == "OGC:WFS")
                    return tmp + "request=GetCapabilities&service=WFS";
                else 
                    return tmp + "request=GetCapabilities&service=WMS";          

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