using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Xml;
using System.Linq;

namespace Kartverket.Geonorge.Api.Services
{
    public class AtomFeedParser
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public List<Dataset> ParseDatasets(string xml)
        {
            var datasets = new List<Dataset>();

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);

            string xpath = "//a:feed/a:entry";

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("a", "http://www.w3.org/2005/Atom");
            nsmgr.AddNamespace("inspire_dls", "http://inspire.ec.europa.eu/schemas/inspire_dls/1.0");
            nsmgr.AddNamespace("gn", "http://geonorge.no/Atom");

            var nodes = xmlDoc.SelectNodes(xpath, nsmgr);

            if (nodes != null)
                foreach (XmlNode childrenNode in nodes)
                {
                    var dataset = new Dataset();
                    var id = childrenNode.SelectSingleNode("a:id", nsmgr);


                    XmlNode uriAlternate = null;
                    var alternate = childrenNode.SelectSingleNode("a:link[@rel='alternate']", nsmgr);
                    if(alternate != null)
                        uriAlternate = alternate.Attributes.GetNamedItem("href");


                    XmlNode url = childrenNode.SelectSingleNode("a:link", nsmgr);

                    if (uriAlternate != null)
                    {
                        dataset.Url = uriAlternate.Value;
                    }
                    
                    else if (!string.IsNullOrEmpty(url.InnerXml))
                        dataset.Url = url.InnerXml;
                    else
                    {
                        dataset.Url = id.InnerXml;
                    }

                    dataset.Url = dataset.Url.Trim();

                    dataset.LastUpdated = childrenNode.SelectSingleNode("a:updated", nsmgr).InnerXml;

                    dataset.Organization = GetOrganization(childrenNode, nsmgr, dataset);

                    dataset.Uuid = childrenNode.SelectSingleNode("inspire_dls:spatial_dataset_identifier_code", nsmgr)?.InnerXml;

                    if (string.IsNullOrEmpty(dataset.Uuid))
                    {
                        if(dataset.Organization == "Miljødirektoratet")
                        {
                            dataset.Uuid = childrenNode.SelectSingleNode("gn:uuid", nsmgr)?.InnerXml;
                        }
                        else
                        {
                            var urlAsUuid = dataset.Url;
                            var uuid = urlAsUuid.Split('/')?.Last();
                            if (!string.IsNullOrEmpty(uuid))
                                dataset.Uuid = uuid;
                        }
                    }

                    datasets.Add(dataset);
                }
            return datasets;
        }



        private string GetOrganization(XmlNode childrenNode, XmlNamespaceManager nsmgr, Dataset dataset)
        {
            string organization = "";

            if (dataset != null && dataset.Url.Contains("miljodirektoratet"))
            {
                nsmgr.RemoveNamespace("gn", "http://geonorge.no/Atom");
                nsmgr.AddNamespace("gn", "http://geonorge.no/geonorge");
            }

            var organizationGN = childrenNode.SelectSingleNode("a:author/gn:organisation", nsmgr);

            if (organizationGN != null)
                organization = organizationGN.InnerXml;
            else if (childrenNode.SelectSingleNode("a:author/a:name", nsmgr) != null)
                organization = childrenNode.SelectSingleNode("a:author/a:name", nsmgr).InnerXml;
            else
                organization = "Kartverket";

            if (dataset != null && string.IsNullOrEmpty(dataset.Organization) && dataset.Url.Contains("ngu.no"))
                organization = "Norges geologiske undersøkelse";
            else if (dataset != null && string.IsNullOrEmpty(dataset.Organization) && dataset.Url.Contains("nibio.no"))
                organization = "Norsk institutt for bioøkonomi";

            if (organization == "Geonorge")
                organization = "Kartverket";

            return organization;
        }



        private string GetUrl(XmlNode xmlNode, XmlNamespaceManager nsmgr)
        {
            string url = "";
            var urlNode = xmlNode.SelectSingleNode("a:link[@rel='describedby']", nsmgr);
            if(urlNode != null) { 
              var hrefNode = urlNode.Attributes?.GetNamedItem("href");
                if(hrefNode != null)
                {
                    url = hrefNode.Value;
                }

            }
            if (!string.IsNullOrEmpty(urlNode?.Value))
            {
                url = urlNode.Value;
            }

            var link = xmlNode.SelectSingleNode("a:link[@rel='alternate']", nsmgr)?.Attributes?.GetNamedItem("href");
            if (link != null)
                url = link.InnerText;
            else {
                link = xmlNode.SelectSingleNode("a:link[@rel='section']", nsmgr)?.Attributes?.GetNamedItem("href");
                if (link != null)
                    url = link.InnerText;
            }

            return url;

        }
    }
}
