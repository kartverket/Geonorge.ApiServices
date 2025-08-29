using Geonorge.ApiServices.Models;
using GeoNorgeAPI;
using System.Xml;

namespace Geonorge.ApiServices.Services
{
    public class AtomFeedParser : IAtomFeedParser
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        private readonly ILogger<AtomFeedParser> _logger;
        private readonly IConfiguration _settings;
        IAtomFeedParser _atomFeedParser;

        public AtomFeedParser(IConfiguration settings, ILogger<AtomFeedParser> logger, IAtomFeedParser atomFeedParser)
        {
            _settings = settings;
            _logger = logger;
            _atomFeedParser = atomFeedParser;
        }

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
                    if (alternate != null)
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

                    //dataset.Uuid = childrenNode.SelectSingleNode("inspire_dls:spatial_dataset_identifier_code", nsmgr)?.InnerXml; // data is not metadata uuid but datasetId

                    XmlNode uriDescribedby = null;
                    var describedby = childrenNode.SelectSingleNode("a:link[@rel='describedby']", nsmgr);
                    if (describedby != null)
                        uriDescribedby = describedby.Attributes.GetNamedItem("href");

                    if (uriDescribedby != null)
                    {
                        var uuid = uriDescribedby.Value.Split('=')?.Last();
                        if (uriDescribedby.Value.IndexOf('=') > 0 && !string.IsNullOrEmpty(uuid))
                            dataset.Uuid = uuid;
                    }

                    if (string.IsNullOrEmpty(dataset.Uuid))
                    {
                        if (dataset.Organization == "Miljødirektoratet")
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

                    dataset.DatasetFiles = GetDatasetFiles(dataset.Url, dataset);

                    var formats = dataset.DatasetFiles.Select(f => f.Format).Distinct();

                    List<SimpleDistribution> distributions = new List<SimpleDistribution>();
                    foreach (var format in formats)
                    {
                        distributions.Add(
                            new SimpleDistribution
                            {
                                Organization = dataset.Organization,
                                FormatName = FixFormat(format)
                            });
                    }

                    dataset.DistributionsFormats = distributions;

                    var projections = dataset.DatasetFiles.Select(f => f.Projection).Distinct();

                    List<SimpleReferenceSystem> referenceSystems = new List<SimpleReferenceSystem>();
                    foreach (var projection in projections)
                    {
                        var system = projection;
                        if (!system.StartsWith("http"))
                            system = "http://www.opengis.net/def/crs/EPSG/0/" + projection.Replace("EPSG:", "");
                        referenceSystems.Add(
                            new SimpleReferenceSystem
                            {
                                CoordinateSystem = system
                            });
                    }

                    dataset.ReferenceSystems = referenceSystems;

                    datasets.Add(dataset);
                }
            return datasets;
        }

        private string FixFormat(string format)
        {
            if (!string.IsNullOrEmpty(format))
            {
                format = format.Replace("Format:", "");
                format = format.Replace("-format", "");

                if (format.ToUpper() == "GEODATABASE_FILE")
                    format = "FGDB";
                else if (format.ToUpper() == "SHAPE")
                    format = "Shape";
                else if (format.ToLower() == "gdb")
                    format = "GDB";
                else if (format.ToLower() == "shp")
                    format = "Shape";
                else if (format.ToLower() == "sosi")
                    format = "SOSI";
                else if (format.ToUpper() == "GEOJSON")
                    format = "GeoJSON";
                else if (format == "FGDB-format")
                    format = "FGDB";
                else if (format == "gml")
                    format = "GML";
                else if (format.ToUpper() == "GEOJSON")
                    format = "GeoJSON";
                else if (format == "POSTGIS")
                    format = "PostGIS";

            }

            return format;
        }

        public List<DatasetFile> GetDatasetFiles(string url, Dataset dataset)
        {
            try
            {
                var getFeedTask = HttpClient.GetStringAsync(url);
                _logger.LogDebug("Fetch dataset files from " + url);
                List<DatasetFile> datasetFiles = _atomFeedParser.ParseDatasetFiles(getFeedTask.Result, dataset).OrderBy(d => d.Title).ToList();

                return datasetFiles;
            }
            catch (Exception e)
            {
                _logger.LogError("Could not get dataset files", e);
                return new List<DatasetFile>();
            }
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
            if (urlNode != null)
            {
                var hrefNode = urlNode.Attributes?.GetNamedItem("href");
                if (hrefNode != null)
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
            else
            {
                link = xmlNode.SelectSingleNode("a:link[@rel='section']", nsmgr)?.Attributes?.GetNamedItem("href");
                if (link != null)
                    url = link.InnerText;
            }

            return url;

        }

        public List<DatasetFile> ParseDatasetFiles(string xml, Dataset dataset)
        {
            var datasetFiles = new List<DatasetFile>();

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);

            string xpath = "//a:feed/a:entry";

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("a", "http://www.w3.org/2005/Atom");
            nsmgr.AddNamespace("inspire_dls", "http://inspire.ec.europa.eu/schemas/inspire_dls/1.0");
            nsmgr.AddNamespace("gn", "http://geonorge.no/Atom");

            var nodes = xmlDoc.SelectNodes(xpath, nsmgr);

            foreach (XmlNode childrenNode in nodes)
            {
                var datasetFile = new DatasetFile();
                datasetFile.Title = childrenNode.SelectSingleNode("a:title", nsmgr).InnerXml;
                datasetFile.Description = GetDescription(childrenNode, nsmgr);
                datasetFile.Url = GetUrl(childrenNode, nsmgr);
                datasetFile.LastUpdated = GetLastUpdated(childrenNode, nsmgr);
                datasetFile.Projection = GetProjection(childrenNode.SelectNodes("a:category", nsmgr));
                datasetFile.Format = GetFormat(childrenNode.SelectSingleNode("a:title", nsmgr), childrenNode.SelectNodes("a:category", nsmgr));
                datasetFile.AreaCode = GetAreaCode(childrenNode.SelectNodes("a:category", nsmgr));
                datasetFile.AreaLabel = GetAreaLabel(childrenNode.SelectNodes("a:category", nsmgr));
                datasetFile.Organization = GetOrganization(childrenNode, nsmgr, dataset);

                datasetFiles.Add(datasetFile);
            }
            return datasetFiles;
        }

        private string GetAreaCode(XmlNodeList xmlNodeList)
        {
            string areaCode = "";
            foreach (XmlNode node in xmlNodeList)
            {
                if (node.Attributes["scheme"] != null && node.Attributes["scheme"].Value.Contains("geografisk-distribusjonsinndeling"))
                {
                    areaCode = areaCode + node.Attributes["term"].Value + " ";
                }
            }

            return areaCode.Trim();
        }

        private string GetAreaLabel(XmlNodeList xmlNodeList)
        {
            string areaLabel = "";

            foreach (XmlNode node in xmlNodeList)
            {
                if (node.Attributes["scheme"] != null && node.Attributes["scheme"].Value.Contains("geografisk-distribusjonsinndeling"))
                {
                    areaLabel = areaLabel + node.Attributes["label"].Value + " ";
                }
            }

            return areaLabel.Trim();
        }
        private string GetProjection(XmlNodeList xmlNodeList)
        {
            foreach (XmlNode node in xmlNodeList)
            {
                var scheme = node.Attributes["scheme"]?.Value;
                bool hasProjection = false;
                if (scheme != null)
                {
                    if (scheme.StartsWith("http://www.opengis.net/def/crs/"))
                    {
                        hasProjection = true;
                    }
                    else if (scheme.StartsWith("https://register.geonorge.no/api/epsg-koder"))
                    {
                        hasProjection = true;
                    }
                }


                if (hasProjection)
                {
                    if (!string.IsNullOrEmpty(node.Attributes["term"]?.Value))
                    {
                        var term = node.Attributes["term"]?.Value;
                        if (!string.IsNullOrEmpty(term) && term.StartsWith("EPSG:"))
                            return node.Attributes["term"].Value;
                    }
                    else if (!string.IsNullOrEmpty(node.Attributes["label"]?.Value))
                    {
                        var label = node.Attributes["label"]?.Value;
                        if (!string.IsNullOrEmpty(label) && !label.StartsWith("EPSG/"))
                            return node.Attributes["label"].Value;
                    }
                    return node.Attributes["term"].Value;
                }
            }
            return null;
        }
        private string GetFormat(XmlNode xmlNode, XmlNodeList xmlNodeList)
        {
            foreach (XmlNode node in xmlNodeList)
            {
                if (node.Attributes["scheme"] != null && (node.Attributes["scheme"].Value.Contains("vektorformater") || node.Attributes["scheme"].Value.Contains("rasterformater")))
                {
                    return node.Attributes["term"].Value;
                }
            }

            foreach (XmlNode node in xmlNodeList)
            {
                if (node.Attributes["term"] != null && node.Attributes["term"].Value.Contains("vektorformater")
                    || node.Attributes["term"].Value.Contains("rasterformater"))
                {
                    return node.Attributes["label"].Value;
                }
            }

            if (xmlNode != null)
            {
                var format = xmlNode.InnerText;
                if (format.Contains(","))
                    return format.Split(',')[0];
            }

            return "";
        }

        private string GetLastUpdated(XmlNode xmlNode, XmlNamespaceManager nsmgr)
        {
            var lastUpdated = xmlNode.SelectSingleNode("a:updated", nsmgr)?.InnerXml;

            if (string.IsNullOrEmpty(lastUpdated))
            {
                var updated = xmlNode.SelectSingleNode("a:link[@rel='alternate']", nsmgr).Attributes.GetNamedItem("updated");
                if (updated != null)
                    lastUpdated = updated.InnerText;
            }

            if (lastUpdated == null)
                lastUpdated = "";

            return lastUpdated;
        }

        private string GetDescription(XmlNode xmlNode, XmlNamespaceManager nsmgr)
        {
            var description = xmlNode.SelectSingleNode("a:category", nsmgr)?.InnerXml;
            if (string.IsNullOrEmpty(description))
            {
                description = xmlNode.SelectSingleNode("a:content", nsmgr)?.InnerText;
            }

            if (description == null)
                description = "";

            return description;
        }

    }

    public interface IAtomFeedParser
    {
        List<Dataset> ParseDatasets(string xml);
        List<DatasetFile> ParseDatasetFiles(string xml, Dataset dataset);
    }
}