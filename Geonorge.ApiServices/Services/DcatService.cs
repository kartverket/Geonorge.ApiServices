using Geonorge.ApiServices.Models;
using GeoNorgeAPI;
using Kartverket.Geonorge.Api.Services;
using Kartverket.Geonorge.Utilities.Organization;
using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web;
using System.Xml;
using www.opengis.net;
using HttpClientFactory = Kartverket.Geonorge.Utilities.Organization.HttpClientFactory;
using IHttpClientFactory = Kartverket.Geonorge.Utilities.Organization.IHttpClientFactory;

namespace Geonorge.ApiServices.Services
{
    public interface IDcatService
    {
        void GenerateDcat();
        List<RecordType> GetDatasets();
        List<RecordType> GetServices();
        Dictionary<string, string> GetOrganizationsLink();
        Dictionary<string, DcatService.DistributionType> GetDistributionTypes();
    }
    public class DcatService : IDcatService
    {
        private readonly ILogger<DcatService> _logger;
        private readonly IConfiguration _settings;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;

        public DcatService(IConfiguration settings, ILogger<DcatService> logger, IWebHostEnvironment env)
        {
            _settings = settings;
            _logger = logger;
            _env = env;
            kartkatalogenUrl = _settings["KartkatalogenUrl"];
            geoNetworkUrl = _settings["GeoNetworkUrl"];
            registryUrl = _settings["RegistryUrl"];
            _organizationService = new OrganizationService(registryUrl, new HttpClientFactory());
            geoNorge = new GeoNorge("", "", geoNetworkUrl);
            _httpClientFactory = new HttpClientFactory();
        }

        const string xmlnsRdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        const string xmlnsFoaf = "http://xmlns.com/foaf/0.1/";
        const string xmlnsGco = "http://www.isotc211.org/2005/gco";
        const string xmlnsVoid = "http://www.w3.org/TR/void/";
        const string xmlnsSkos = "http://www.w3.org/2004/02/skos/core#";
        const string xmlnsDc = "http://purl.org/dc/elements/1.1/";
        const string xmlnsDct = "http://purl.org/dc/terms/";
        const string xmlnsDctype = "http://purl.org/dc/dcmitype/";
        const string xmlnsDcat = "http://www.w3.org/ns/dcat#";
        const string xmlnsVcard = "http://www.w3.org/2006/vcard/ns#";
        const string xmlnsAdms = "http://www.w3.org/ns/adms#";
        const string xmlnsXslUtils = "java:org.fao.geonet.util.XslUtil";
        const string xmlnsGmd = "http://www.isotc211.org/2005/gmd";
        const string xmlnsRdfs = "http://www.w3.org/2000/01/rdf-schema#";
        const string xmlnsOwl = "http://www.w3.org/2002/07/owl#";
        const string xmlnsLocn = "http://www.w3.org/ns/locn#";
        const string xmlnsGml = "http://www.opengis.net/gml";
        const string xmlnsDcatAp = "http://data.europa.eu/r5r/";

        //string geoNetworkendPoint = "srv/nor/csw-dataset?";

        string kartkatalogenUrl;
        string geoNetworkUrl;
        string registryUrl;

        XmlDocument doc;
        XmlDocument docService;
        XmlDocument conceptsDoc;
        XmlNamespaceManager nsmgr;

        List<RecordType> metadataSets;
        List<RecordType> metadataServices;

        GeoNorge geoNorge;

        DateTime? catalogLastModified;

        private readonly OrganizationService _organizationService;

        Dictionary<string, string> OrganizationsLink;
        Dictionary<string, string> ConceptObjects = new Dictionary<string, string>();
        Dictionary<string, string> MediaTypes;
        Dictionary<string, string> FormatUrls;
        Dictionary<string, DistributionType> DistributionTypes;
        Dictionary<string, XmlNode> dataServices = new Dictionary<string, XmlNode>();

        List<string> distributionFormats = new List<string>();

        public void GenerateDcat()
        {
            _logger.LogInformation("Generating DCAT");

            try
            {
                FormatUrls = GetFormatUrls();
                MediaTypes = GetMediaTypes();
                OrganizationsLink = GetOrganizationsLink();
                DistributionTypes = GetDistributionTypes();
                metadataServices = GetServices();
                metadataSets = GetDatasets();

                XmlElement root = Setup();
                XmlElement rootService = SetupService();

                GetConcepts();

                XmlElement catalog = CreateCatalog(root);
                XmlElement catalogService = CreateCatalogService(rootService);

                dataServices = CreateDataServices(metadataServices);

                CreateDatasets(root, catalog);

                AddCreatedDataServicesFromDatasets(dataServices);

                AddDataServicesToCatalog(catalogService);

                AddServesDatasetRelations(root, rootService);

                UpdateDataServiceTitleIfServiceServes1Dataset(root, rootService);

                AddConcepts(root);

                Finalize(root, catalog);
                FinalizeService(rootService, catalogService);

                var path = _settings["DcatFolder"] + "\\geonorge_dcat.rdf";
                doc.Save(path);

                var pathService = _settings["DcatFolder"] + "\\geonorge_dcat_service.rdf";
                docService.Save(pathService);

                _logger.LogInformation("Finished generating DCAT");
            }
            catch (Exception e)
            {
                _logger.LogError($"Error generating DCAT: {e}");
                throw new Exception($"Error generating DCAT: {e}");
            }
}

        // Append dataset title to service title when the service serves exactly one dataset
        private void UpdateDataServiceTitleIfServiceServes1Dataset(XmlElement rootDatasets, XmlElement rootServices)
        {
            _logger.LogInformation($"Processing UpdateDataServiceTitleIfServiceServes1Dataset");

            if (doc == null || docService == null || nsmgr == null) return;

            // For each DataService in the services RDF
            var dataServicesNodes = docService.DocumentElement?
                .SelectNodes("//dcat:DataService", nsmgr)
                ?.Cast<XmlElement>() ?? Enumerable.Empty<XmlElement>();

            foreach (var ds in dataServicesNodes)
            {
                // Count servesDataset entries
                var serves = ds.SelectNodes("./dcat:servesDataset", nsmgr)?.Cast<XmlElement>()?.ToList() ?? new List<XmlElement>();
                if (serves.Count != 1) continue;

                var datasetUri = serves[0].GetAttribute("resource", xmlnsRdf);
                if (string.IsNullOrEmpty(datasetUri)) continue;

                // Find the dataset by rdf:about and read its dct:title (prefer nb/no, else first)
                var datasetNode = doc.DocumentElement?
                    .SelectNodes($"//dcat:Dataset[@rdf:about='{datasetUri}']", nsmgr)
                    ?.Cast<XmlElement>()
                    .FirstOrDefault();
                if (datasetNode == null) continue;

                var datasetTitles = datasetNode.SelectNodes("./dct:title", nsmgr)?.Cast<XmlElement>()?.ToList() ?? new List<XmlElement>();
                if (datasetTitles.Count == 0) continue;

                string? datasetTitle =
                    datasetTitles.FirstOrDefault(t => string.Equals(t.GetAttribute("xml:lang"), "nb", StringComparison.OrdinalIgnoreCase))?.InnerText
                    ?? datasetTitles.FirstOrDefault(t => string.Equals(t.GetAttribute("xml:lang"), "no", StringComparison.OrdinalIgnoreCase))?.InnerText
                    ?? datasetTitles.First().InnerText;

                if (string.IsNullOrWhiteSpace(datasetTitle)) continue;

                // Find/ensure a Norwegian title element on the DataService
                var serviceTitles = ds.SelectNodes("./dct:title", nsmgr)?.Cast<XmlElement>()?.ToList() ?? new List<XmlElement>();
                XmlElement? nbTitleEl = serviceTitles.FirstOrDefault(t => string.Equals(t.GetAttribute("xml:lang"), "nb", StringComparison.OrdinalIgnoreCase))
                                        ?? serviceTitles.FirstOrDefault(t => string.Equals(t.GetAttribute("xml:lang"), "no", StringComparison.OrdinalIgnoreCase))
                                        ?? serviceTitles.FirstOrDefault();

                if (nbTitleEl == null)
                {
                    nbTitleEl = docService.CreateElement("dct", "title", xmlnsDct);
                    nbTitleEl.SetAttribute("xml:lang", "nb");
                    nbTitleEl.InnerText = "Dataservice";
                    ds.AppendChild(nbTitleEl);
                }

                // Append dataset title if not already included
                var currentTitle = nbTitleEl.InnerText ?? string.Empty;
                if (!currentTitle.Contains(datasetTitle, StringComparison.Ordinal))
                {
                    nbTitleEl.InnerText = string.IsNullOrEmpty(currentTitle)
                        ? datasetTitle
                        : $"{currentTitle} - {datasetTitle}";
                }
            }

            _logger.LogInformation($"Processing UpdateDataServiceTitleIfServiceServes1Dataset ended");
        }

        // Build dcat:servesDataset relations based on dcat:accessService and dcat:accessURL
        // Rule: If a Distribution has accessService @rdf:resource = S and accessURL @rdf:resource = U,
        // and there exists a dcat:DataService in docService with rdf:about = U, then add:
        //   <dcat:servesDataset rdf:resource="dataset-about"/>
        // to that DataService node.
        private void AddServesDatasetRelations(XmlElement rootDatasets, XmlElement rootServices)
        {
            _logger.LogInformation($"Processing AddServesDatasetRelations");

            if (doc == null || docService == null || nsmgr == null) return;

            // Helper local normalizer to reduce mismatch risks
            static string NormalizeUrl(string? u)
            {
                if (string.IsNullOrWhiteSpace(u)) return string.Empty;
                // apply same sanitization as used when creating DataService/about
                u = u.Replace("{", "%7B").Replace("}", "%7D");
                // trim trailing slash for matching consistency
                if (u.Length > 1 && u.EndsWith("/")) u = u.Substring(0, u.Length - 1);
                return u;
            }

            var distributions = doc.DocumentElement?
                .SelectNodes("//dcat:Distribution", nsmgr)
                ?.Cast<XmlElement>() ?? Enumerable.Empty<XmlElement>();

            foreach (var distribution in distributions)
            {
                var distributionAbout = distribution.GetAttribute("about", xmlnsRdf);
                if (string.IsNullOrEmpty(distributionAbout)) continue;

                // Find dataset owning this distribution
                var datasetEl = doc.DocumentElement?
                    .SelectNodes($"//dcat:Dataset[dcat:distribution/@rdf:resource='{distributionAbout}']", nsmgr)
                    ?.Cast<XmlElement>()
                    .FirstOrDefault();
                if (datasetEl == null) continue;

                var datasetAbout = datasetEl.GetAttribute("about", xmlnsRdf);
                if (string.IsNullOrEmpty(datasetAbout)) continue;

                // Read accessService and accessURL
                var accessServiceResRaw = distribution
                    .SelectNodes("./dcat:accessService", nsmgr)
                    ?.Cast<XmlElement>()
                    .Select(a => a.GetAttribute("resource", xmlnsRdf))
                    .FirstOrDefault(r => !string.IsNullOrEmpty(r));

                var accessUrlResRaw = distribution
                    .SelectNodes("./dcat:accessURL", nsmgr)
                    ?.Cast<XmlElement>()
                    .Select(a => a.GetAttribute("resource", xmlnsRdf))
                    .FirstOrDefault(r => !string.IsNullOrEmpty(r));

                var accessServiceRes = NormalizeUrl(accessServiceResRaw);
                var accessUrlRes = NormalizeUrl(accessUrlResRaw);

                // Try match DataService by accessService first, then accessURL
                XmlElement? matchingService = null;
                if (!string.IsNullOrEmpty(accessServiceRes))
                {
                    matchingService = docService.DocumentElement?
                        .SelectNodes($"//dcat:DataService[@rdf:about='{accessServiceRes}']", nsmgr)
                        ?.Cast<XmlElement>()
                        .FirstOrDefault();
                    // if not found, attempt with trailing slash alternative
                    if (matchingService == null)
                    {
                        var alt = accessServiceRes + "/";
                        matchingService = docService.DocumentElement?
                            .SelectNodes($"//dcat:DataService[@rdf:about='{alt}']", nsmgr)
                            ?.Cast<XmlElement>()
                            .FirstOrDefault();
                    }
                }
                if (matchingService == null && !string.IsNullOrEmpty(accessUrlRes))
                {
                    matchingService = docService.DocumentElement?
                        .SelectNodes($"//dcat:DataService[@rdf:about='{accessUrlRes}']", nsmgr)
                        ?.Cast<XmlElement>()
                        .FirstOrDefault();
                    if (matchingService == null)
                    {
                        var alt = accessUrlRes + "/";
                        matchingService = docService.DocumentElement?
                            .SelectNodes($"//dcat:DataService[@rdf:about='{alt}']", nsmgr)
                            ?.Cast<XmlElement>()
                            .FirstOrDefault();
                    }
                }
                if (matchingService == null) continue;

                // Avoid duplicates
                bool alreadyServes = matchingService
                    .SelectNodes("./dcat:servesDataset", nsmgr)
                    ?.Cast<XmlElement>()
                    .Any(sd => sd.GetAttribute("resource", xmlnsRdf) == datasetAbout) == true;
                if (alreadyServes) continue;

                var servesDataset = docService.CreateElement("dcat", "servesDataset", xmlnsDcat);
                servesDataset.SetAttribute("resource", xmlnsRdf, datasetAbout);
                matchingService.AppendChild(servesDataset);
            }

            _logger.LogInformation($"Processing AddServesDatasetRelations ended");
        }

        private Dictionary<string, string> GetMediaTypes()
        {
            return new Dictionary<string, string>()
            {
               { "Shape", "https://www.iana.org/assignments/media-types/application/vnd.shp" },
               { "SOSI", "https://www.iana.org/assignments/media-types/text/vnd.sosi" },
               { "GML", "https://www.iana.org/assignments/media-types/application/gml+xml" },
               { "CSV", "https://www.iana.org/assignments/media-types/text/csv" },
               { "GeoJSON", "https://www.iana.org/assignments/media-types/application/geo+json" },
               { "GeoPackage", "https://www.iana.org/assignments/media-types/application/geopackage+sqlite3" },
               { "TIFF", "https://www.iana.org/assignments/media-types/image/tiff" },
               { "PDF", "https://www.iana.org/assignments/media-types/application/pdf" },
               //{ "FGDB", "http://publications.europa.eu/resource/authority/file-type/GDB" }, //not found iana
               { "PostGIS", "https://www.iana.org/assignments/media-types/application/sql" },
               { "LAS", "https://www.iana.org/assignments/media-types/application/vnd.las" },
               { "LAZ", "https://www.iana.org/assignments/media-types/application/vnd.laszip" },
               //{ "JPEG", "http://publications.europa.eu/resource/authority/file-type/JPEG" }, //not found iana, empty?
               { "KML", "https://www.iana.org/assignments/media-types/application/vnd.google-earth.kml+xml" },
               { "KMZ", "https://www.iana.org/assignments/media-types/application/vnd.google-earth.kmz+xml" },
               { "ZIP", "https://www.iana.org/assignments/media-types/application/zip" },
               //{ "PPTX", "http://publications.europa.eu/resource/authority/file-type/PPTX" } //not found iana ppt

            };
        }

        private Dictionary<string, string> GetFormatUrls()
        {
            return new Dictionary<string, string>()
            {
               { "Shape", "http://publications.europa.eu/resource/authority/file-type/SHP" },
               { "SOSI", "http://publications.europa.eu/resource/authority/file-type/TXT" },
               { "GML", "http://publications.europa.eu/resource/authority/file-type/GML" },
               { "CSV", "http://publications.europa.eu/resource/authority/file-type/CSV" },
               { "GeoJSON", "http://publications.europa.eu/resource/authority/file-type/GEOJSON" },
               { "GeoPackage", "http://publications.europa.eu/resource/authority/file-type/GPKG" },
               { "TIFF", "http://publications.europa.eu/resource/authority/file-type/TIFF" },
               { "PDF", "http://publications.europa.eu/resource/authority/file-type/PDF" },
               { "FGDB", "http://publications.europa.eu/resource/authority/file-type/GDB" },
               { "PostGIS", "http://publications.europa.eu/resource/authority/file-type/SQL" },
               { "LAS", "http://publications.europa.eu/resource/authority/file-type/LAS" },
               { "LAZ", "http://publications.europa.eu/resource/authority/file-type/LAZ" },
               { "JPEG", "http://publications.europa.eu/resource/authority/file-type/JPEG" },
               { "KML", "http://publications.europa.eu/resource/authority/file-type/KML" },
               { "KMZ", "http://publications.europa.eu/resource/authority/file-type/KMZ" },
               { "PPTX", "http://publications.europa.eu/resource/authority/file-type/PPTX" },
               { "WMS", "http://publications.europa.eu/resource/authority/file-type/WMS_SRVC" },
               { "ZIP", "http://publications.europa.eu/resource/authority/file-type/ZIP" },
               { "PNG", "http://publications.europa.eu/resource/authority/file-type/PNG" },

            };
        }

        private readonly List<string> DataserviceProtocols = new List<string>
        {
            "GEONORGE:DOWNLOAD",
            "OGC:API-Coverages",
            "OGC:API-EDR",
            "OGC:API-Features",
            "OGC:API-Maps",
            "OGC:API-Styles",
            "OGC:API-Tiles",
            "OGC:CSW",
            "OPENDAP:OPENDAP",
            "W3C:REST",
            "OGC:SOS",
            "OGC:WCS",
            "W3C:WS",
            "OGC:WFS",
            "WMS-C",
            "OGC:WMS",
            "OGC:WMTS",
            "OGC:WPS",
            "OGC:API-Processes",
            "CNCF:CLOUDEVENTS-HTTP"
        };

        private void GetConcepts()
        {
            conceptsDoc = new XmlDocument();
            conceptsDoc.Load(Path.Combine(_env.ContentRootPath, "", "Concepts.xml"));
        }

        private string GetConcept(string prefLabel)
        {
            var concept = conceptsDoc.SelectSingleNode("//skos:Concept[skos:prefLabel='" + prefLabel + "']", nsmgr);
            if (concept != null)
            {
                var about = concept.Attributes.GetNamedItem("rdf:about");
                if (about != null)
                    return about.Value;
            }

            return null;
        }

        private void AddConcepts(XmlElement root)
        {
            var conceptSchemes = conceptsDoc.SelectNodes("//skos:ConceptScheme", nsmgr);
            foreach (XmlNode conceptScheme in conceptSchemes)
            {
                XmlNode import = doc.ImportNode(conceptScheme, true);
                root.AppendChild(import);
            }

            var concepts = conceptsDoc.SelectNodes("//skos:Concept", nsmgr);
            foreach (XmlNode concept in concepts)
            {
                XmlNode import = doc.ImportNode(concept, true);
                root.AppendChild(import);
            }
        }

        private void AddDataServicesToCatalog(XmlElement catalogService)
        {
            if (catalogService == null || dataServices == null || dataServices.Count == 0)
                return;

            // Add references based on dataServices keys
            foreach (var kv in dataServices)
            {
                var resource = kv.Key;
                var serviceRef = docService.CreateElement("dcat", "service", xmlnsDcat);
                serviceRef.SetAttribute("resource", xmlnsRdf, resource);
                catalogService.AppendChild(serviceRef);
            }
        }

        // Helper: ensure foaf:Agent and vcard:Organization nodes exist with rdf:about
        // Optional: agentIdentifier/orgIdentifier should be the organization number or similar stable identifier.
        private void EnsureAgentAndContactPointNodes(
            string? publisherUri,
            string? contactPointUri,
            string? organizationName,
            string? agentIdentifier = null,
            string? orgIdentifier = null,
            string? contactEmail = null)
        {
            // Publisher foaf:Agent (unchanged except signature)
            if (!string.IsNullOrEmpty(publisherUri))
            {
                var existingAgents = docService.DocumentElement?
                    .SelectNodes("//foaf:Agent", nsmgr)
                    ?.Cast<XmlElement>() ?? Enumerable.Empty<XmlElement>();

                var publisherAgent = existingAgents.FirstOrDefault(el => el.GetAttribute("about", xmlnsRdf) == publisherUri);

                if (publisherAgent == null)
                {
                    var agent = docService.CreateElement("foaf", "Agent", xmlnsFoaf);
                    agent.SetAttribute("about", xmlnsRdf, publisherUri);

                    var agentType = docService.CreateElement("dct", "type", xmlnsDct);
                    agentType.SetAttribute("resource", xmlnsRdf, "http://purl.org/adms/publishertype/NationalAuthority");
                    agent.AppendChild(agentType);

                    if (!string.IsNullOrEmpty(agentIdentifier))
                    {
                        var idEl = docService.CreateElement("dct", "identifier", xmlnsDct);
                        idEl.InnerText = agentIdentifier;
                        agent.AppendChild(idEl);
                    }
                    else if (publisherUri.EndsWith("/kartverket", StringComparison.OrdinalIgnoreCase))
                    {
                        var idEl = docService.CreateElement("dct", "identifier", xmlnsDct);
                        idEl.InnerText = "971040238";
                        agent.AppendChild(idEl);
                    }

                    if (publisherUri.EndsWith("/kartverket", StringComparison.OrdinalIgnoreCase))
                    {
                        var agentSameAs = docService.CreateElement("owl", "sameAs", xmlnsOwl);
                        agentSameAs.InnerText = "https://data.brreg.no/enhetsregisteret/api/enheter/971040238";
                        agent.AppendChild(agentSameAs);
                    }

                    if (!string.IsNullOrEmpty(organizationName))
                    {
                        var nameEl = docService.CreateElement("foaf", "name", xmlnsFoaf);
                        nameEl.InnerText = organizationName;
                        agent.AppendChild(nameEl);
                    }

                    docService.DocumentElement?.AppendChild(agent);
                }
                else
                {
                    var hasType = publisherAgent.SelectNodes("./dct:type", nsmgr)
                        ?.Cast<XmlElement>()
                        .Any(t => t.GetAttribute("resource", xmlnsRdf) == "http://purl.org/adms/publishertype/NationalAuthority") == true;

                    if (!hasType)
                    {
                        var agentType = docService.CreateElement("dct", "type", xmlnsDct);
                        agentType.SetAttribute("resource", xmlnsRdf, "http://purl.org/adms/publishertype/NationalAuthority");
                        publisherAgent.AppendChild(agentType);
                    }

                    var hasIdentifier = publisherAgent.SelectNodes("./dct:identifier", nsmgr)?.Count > 0;
                    if (!hasIdentifier)
                    {
                        if (!string.IsNullOrEmpty(agentIdentifier) || publisherUri.EndsWith("/kartverket", StringComparison.OrdinalIgnoreCase))
                        {
                            var idEl = docService.CreateElement("dct", "identifier", xmlnsDct);
                            idEl.InnerText = !string.IsNullOrEmpty(agentIdentifier) ? agentIdentifier : "971040238";
                            publisherAgent.AppendChild(idEl);
                        }
                    }
                }
            }

            // ContactPoint vcard:Organization (+ fn + hasEmail)
            if (!string.IsNullOrEmpty(contactPointUri))
            {
                var existingVcards = docService.DocumentElement?
                    .SelectNodes("//vcard:Organization", nsmgr)
                    ?.Cast<XmlElement>() ?? Enumerable.Empty<XmlElement>();

                var vcardOrg = existingVcards.FirstOrDefault(el => el.GetAttribute("about", xmlnsRdf) == contactPointUri);

                if (vcardOrg == null)
                {
                    vcardOrg = docService.CreateElement("vcard", "Organization", xmlnsVcard);
                    vcardOrg.SetAttribute("about", xmlnsRdf, contactPointUri);

                    if (!string.IsNullOrEmpty(organizationName))
                    {
                        var unit = docService.CreateElement("vcard", "organization-unit", xmlnsVcard);
                        unit.InnerText = organizationName;
                        vcardOrg.AppendChild(unit);
                    }

                    var orgType = docService.CreateElement("dct", "type", xmlnsDct);
                    orgType.SetAttribute("resource", xmlnsRdf, "http://purl.org/adms/publishertype/NationalAuthority");
                    vcardOrg.AppendChild(orgType);

                    if (!string.IsNullOrEmpty(orgIdentifier))
                    {
                        var idEl = docService.CreateElement("dct", "identifier", xmlnsDct);
                        idEl.InnerText = orgIdentifier;
                        vcardOrg.AppendChild(idEl);
                    }

                    // vcard:fn nb = Kundesenteret
                    var fn = docService.CreateElement("vcard", "fn", xmlnsVcard);
                    fn.SetAttribute("xml:lang", "nb");
                    fn.InnerText = "Kundesenteret";
                    vcardOrg.AppendChild(fn);

                    // vcard:hasEmail if provided
                    if (!string.IsNullOrEmpty(contactEmail))
                    {
                        var hasEmail = docService.CreateElement("vcard", "hasEmail", xmlnsVcard);
                        hasEmail.SetAttribute("resource", xmlnsRdf, $"mailto:{contactEmail}");
                        vcardOrg.AppendChild(hasEmail);
                    }

                    docService.DocumentElement?.AppendChild(vcardOrg);
                }
                else
                {
                    var hasType = vcardOrg.SelectNodes("./dct:type", nsmgr)
                        ?.Cast<XmlElement>()
                        .Any(t => t.GetAttribute("resource", xmlnsRdf) == "http://purl.org/adms/publishertype/NationalAuthority") == true;

                    if (!hasType)
                    {
                        var orgType = docService.CreateElement("dct", "type", xmlnsDct);
                        orgType.SetAttribute("resource", xmlnsRdf, "http://purl.org/adms/publishertype/NationalAuthority");
                        vcardOrg.AppendChild(orgType);
                    }

                    var hasIdentifier = vcardOrg.SelectNodes("./dct:identifier", nsmgr)?.Count > 0;
                    if (!hasIdentifier && !string.IsNullOrEmpty(orgIdentifier))
                    {
                        var idEl = docService.CreateElement("dct", "identifier", xmlnsDct);
                        idEl.InnerText = orgIdentifier;
                        vcardOrg.AppendChild(idEl);
                    }

                    // Ensure vcard:fn nb = Kundesenteret
                    var hasFn = vcardOrg.SelectNodes("./vcard:fn", nsmgr)
                        ?.Cast<XmlElement>()
                        .Any(e =>
                        {
                            var lang = e.GetAttribute("xml:lang");
                            return string.Equals(lang, "nb", StringComparison.OrdinalIgnoreCase)
                                   && string.Equals(e.InnerText, "Kundesenteret", StringComparison.Ordinal);
                        }) == true;

                    if (!hasFn)
                    {
                        var fn = docService.CreateElement("vcard", "fn", xmlnsVcard);
                        fn.SetAttribute("xml:lang", "nb");
                        fn.InnerText = "Kundesenteret";
                        vcardOrg.AppendChild(fn);
                    }

                    // Ensure only ONE vcard:hasEmail, applying precedence-chosen contactEmail if provided
                    if (!string.IsNullOrEmpty(contactEmail))
                    {
                        // Remove all existing hasEmail to avoid duplicates
                        var existingHasEmails = vcardOrg.SelectNodes("./vcard:hasEmail", nsmgr)?.Cast<XmlElement>()?.ToList() ?? new List<XmlElement>();
                        foreach (var he in existingHasEmails)
                            vcardOrg.RemoveChild(he);

                        var hasEmail = docService.CreateElement("vcard", "hasEmail", xmlnsVcard);
                        hasEmail.SetAttribute("resource", xmlnsRdf, $"mailto:{contactEmail}");
                        vcardOrg.AppendChild(hasEmail);
                    }
                }
            }
        }

        private Dictionary<string, XmlNode> CreateDataServices(List<RecordType> servicesMetadata)
        {
            var result = new Dictionary<string, XmlNode>();
            if (servicesMetadata == null || servicesMetadata.Count == 0)
                return result;

            foreach (var metadata in servicesMetadata)
            {
                string uuid = metadata.Items[0].Text[0];

                _logger.LogInformation($"Processing dataset service: [uuid={uuid}]");

                MD_Metadata_Type md = geoNorge.GetRecordByUuid(uuid);
                var data = new SimpleMetadata(md);

                string? endpointUrl = null;
                string? protocol = null;
                string? format = null;

                var firstDistro = data?.DistributionsFormats?.FirstOrDefault(df => !string.IsNullOrEmpty(df?.URL));
                if (firstDistro != null)
                {
                    endpointUrl = SanitizeUrlForRdf(firstDistro.URL);
                    protocol = firstDistro.Protocol;
                    format = firstDistro.FormatName;
                }

                bool isDataService = !string.IsNullOrEmpty(protocol) && DataserviceProtocols.Contains(protocol);
                if (!isDataService && string.IsNullOrEmpty(endpointUrl))
                    continue;

                var about = endpointUrl;

                XmlElement dataService = docService.CreateElement("dcat", "DataService", xmlnsDcat);
                dataService.SetAttribute("about", xmlnsRdf, about);

                XmlElement titleEl = docService.CreateElement("dct", "title", xmlnsDct);
                titleEl.SetAttribute("xml:lang", "nb");
                titleEl.InnerText = string.IsNullOrEmpty(data?.Title) ? uuid : data.Title;
                dataService.AppendChild(titleEl);

                XmlElement description = docService.CreateElement("dct", "description", xmlnsDct);
                description.SetAttribute("xml:lang", "no");
                if (!string.IsNullOrEmpty(data?.Abstract))
                    description.InnerText = data.Abstract;
                dataService.AppendChild(description);

                if (!string.IsNullOrEmpty(endpointUrl))
                {
                    XmlElement endpoint = docService.CreateElement("dcat", "endpointURL", xmlnsDcat);
                    endpoint.SetAttribute("resource", xmlnsRdf, endpointUrl);
                    dataService.AppendChild(endpoint);
                }

                // Resolve Organization.Number for publisher and contact
                string? publisherIdentifier = null;
                string? contactIdentifier = null;

                Organization? publisherOrg = null;
                Organization? contactOrg = null;

                if (!string.IsNullOrEmpty(data?.ContactPublisher?.Organization))
                {
                    try
                    {
                        publisherOrg = _organizationService.GetOrganizationByName(data.ContactPublisher.Organization).Result;
                        publisherIdentifier = publisherOrg?.Number;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"Organization lookup failed for publisher [{data.ContactPublisher.Organization}]: {ex.Message}");
                    }
                }
                if (!string.IsNullOrEmpty(data?.ContactMetadata?.Organization))
                {
                    try
                    {
                        contactOrg = _organizationService.GetOrganizationByName(data.ContactMetadata.Organization).Result;
                        contactIdentifier = contactOrg?.Number;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"Organization lookup failed for contact [{data.ContactMetadata.Organization}]: {ex.Message}");
                    }
                }

                if (data?.ContactPublisher != null && !string.IsNullOrEmpty(data.ContactPublisher.Organization)
                    && OrganizationsLink != null && OrganizationsLink.TryGetValue(data.ContactPublisher.Organization, out var orgLink))
                {
                    var publisherUri = orgLink.Replace("organisasjoner/kartverket/", "organisasjoner/");
                    if (!string.IsNullOrEmpty(data.ContactPublisher.Email))
                        publisherUri = publisherUri + "/" + GetUsernameFromEmail(data.ContactPublisher.Email);

                    XmlElement publisher = docService.CreateElement("dct", "publisher", xmlnsDct);
                    publisher.SetAttribute("resource", xmlnsRdf, publisherUri);
                    dataService.AppendChild(publisher);

                    string? contactPointUri = null;
                    string? orgName = null;

                    if (data.ContactMetadata != null && !string.IsNullOrEmpty(data.ContactMetadata.Organization)
                        && OrganizationsLink.TryGetValue(data.ContactMetadata.Organization, out var cpOrgLink))
                    {
                        contactPointUri = cpOrgLink.Replace("organisasjoner/kartverket/", "organisasjoner/");
                        if (!string.IsNullOrEmpty(data.ContactMetadata.Email))
                            contactPointUri = contactPointUri + "/" + GetUsernameFromEmail(data.ContactMetadata.Email);
                        orgName = data.ContactMetadata.Organization;
                    }
                    else
                    {
                        contactPointUri = publisherUri;
                        orgName = data.ContactPublisher.Organization;
                    }

                    if (!string.IsNullOrEmpty(contactPointUri))
                    {
                        var contactPoint = docService.CreateElement("dcat", "contactPoint", xmlnsDcat);
                        contactPoint.SetAttribute("resource", xmlnsRdf, contactPointUri);
                        dataService.AppendChild(contactPoint);
                    }

                    string emailIdentifier = data?.ContactMetadata?.Email;
                    if (string.IsNullOrEmpty(emailIdentifier))
                        emailIdentifier = data?.ContactOwner?.Email;
                    if (string.IsNullOrEmpty(emailIdentifier))
                        emailIdentifier = data?.ContactPublisher?.Email;

                    // Ensure about-nodes with identifiers
                    EnsureAgentAndContactPointNodes(
                        publisherUri,
                        contactPointUri,
                        orgName,
                        agentIdentifier: publisherIdentifier,
                        orgIdentifier: contactIdentifier,
                        contactEmail: emailIdentifier
                    );
                }

                if (data?.Constraints != null && !string.IsNullOrEmpty(data.Constraints.UseConstraintsLicenseLink))
                {
                    XmlElement license = docService.CreateElement("dct", "license", xmlnsDct);
                    license.SetAttribute("resource", xmlnsRdf, MapLicense(data.Constraints.UseConstraintsLicenseLink));
                    dataService.AppendChild(license);
                }

                if (!string.IsNullOrEmpty(format))
                {
                    XmlElement formatEl = docService.CreateElement("dct", "format", xmlnsDct);
                    if (FormatUrls.ContainsKey(format))
                        formatEl.SetAttribute("resource", xmlnsRdf, FormatUrls[format]);
                    else
                        formatEl.InnerText = format;
                    dataService.AppendChild(formatEl);
                }

                docService.DocumentElement?.AppendChild(dataService);

                var key = !string.IsNullOrEmpty(endpointUrl) ? endpointUrl : about;
                if (!result.ContainsKey(key))
                    result.Add(key, dataService);
            }

            return result;
        }

        private void AddCreatedDataServicesFromDatasets(Dictionary<string, XmlNode> discoveredDataServices)
        {
            if (discoveredDataServices == null || discoveredDataServices.Count == 0)
                return;

            foreach (var kv in discoveredDataServices)
            {
                var endpointUrl = kv.Key;                         // key is the endpoint URL
                var dataServiceNode = kv.Value as XmlElement;

                // Does a service with this about/endpoint already exist in the RDF doc?
                bool existsInDoc =
                    docService.DocumentElement?
                        .SelectNodes("//dcat:DataService", nsmgr)
                        ?.Cast<XmlElement>()
                        .Any(el => el.GetAttribute("about", xmlnsRdf) == endpointUrl) == true;

                if (existsInDoc)
                    continue;

                // Prefer appending the full node we already built (it already has dct:title)
                if (dataServiceNode != null)
                {
                    var nodeToAppend = dataServiceNode.OwnerDocument == docService
                        ? dataServiceNode
                        : (XmlElement)docService.ImportNode(dataServiceNode, true);

                    // Ensure dcat:contactPoint exists; fallback to dct:publisher resource
                    var contactPointExisting = nodeToAppend.SelectNodes("./dcat:contactPoint", nsmgr)?.Count > 0;
                    if (!contactPointExisting)
                    {
                        var publisherRes = nodeToAppend
                            .SelectNodes("./dct:publisher", nsmgr)
                            ?.Cast<XmlElement>()
                            .Select(p => p.GetAttribute("resource", xmlnsRdf))
                            .FirstOrDefault(u => !string.IsNullOrEmpty(u));

                        if (!string.IsNullOrEmpty(publisherRes))
                        {
                            var contactPoint = docService.CreateElement("dcat", "contactPoint", xmlnsDcat);
                            contactPoint.SetAttribute("resource", xmlnsRdf, publisherRes);
                            nodeToAppend.AppendChild(contactPoint);

                            // Create/patch foaf:Agent and vcard:Organization for this contact point
                            EnsureAgentAndContactPointNodes(
                                publisherRes,
                                publisherRes,
                                organizationName: null,
                                agentIdentifier: null,
                                orgIdentifier: null,
                                contactEmail: null);
                        }
                    }

                    docService.DocumentElement?.AppendChild(nodeToAppend);
                    continue;
                }

                // Fallback: create a minimal DataService; try to reuse a provided title if present
                var ds = docService.CreateElement("dcat", "DataService", xmlnsDcat);
                ds.SetAttribute("about", xmlnsRdf, endpointUrl);

                var endpoint = docService.CreateElement("dcat", "endpointURL", xmlnsDcat);
                endpoint.SetAttribute("resource", xmlnsRdf, endpointUrl);
                ds.AppendChild(endpoint);

                var titleEl = docService.CreateElement("dct", "title", xmlnsDct);
                titleEl.SetAttribute("xml:lang", "nb");
                var providedTitle = (kv.Value as XmlElement)?.SelectSingleNode("./dct:title", nsmgr)?.InnerText;
                titleEl.InnerText = !string.IsNullOrEmpty(providedTitle) ? providedTitle : "Dataservice";
                ds.AppendChild(titleEl);

                // If the provided node had a publisher, use it as contactPoint
                var providedPublisher = (kv.Value as XmlElement)?
                    .SelectNodes("./dct:publisher", nsmgr)
                    ?.Cast<XmlElement>()
                    .Select(p => p.GetAttribute("resource", xmlnsRdf))
                    .FirstOrDefault(u => !string.IsNullOrEmpty(u));

                if (!string.IsNullOrEmpty(providedPublisher))
                {
                    var publisherRef = docService.CreateElement("dct", "publisher", xmlnsDct);
                    publisherRef.SetAttribute("resource", xmlnsRdf, providedPublisher);
                    ds.AppendChild(publisherRef);

                    var contactPoint = docService.CreateElement("dcat", "contactPoint", xmlnsDcat);
                    contactPoint.SetAttribute("resource", xmlnsRdf, providedPublisher);
                    ds.AppendChild(contactPoint);

                    EnsureAgentAndContactPointNodes(
                        providedPublisher,
                        providedPublisher,
                        organizationName: null,
                        agentIdentifier: null,
                        orgIdentifier: null,
                        contactEmail: null);
                }

                docService.DocumentElement?.AppendChild(ds);
            }
        }

        private void CreateDatasets(XmlElement root, XmlElement catalog)
        {

            Dictionary<string, XmlNode> foafAgents = new Dictionary<string, XmlNode>();
            Dictionary<string, XmlNode> vcardKinds = new Dictionary<string, XmlNode>();
            Dictionary<string, XmlNode> services = new Dictionary<string, XmlNode>();

            foreach (var metadata in metadataSets)
            {
                try
                {
                    string uuid = metadata.Items[0].Text[0];
                    _logger.LogInformation($"Processing dataset: [uuid={uuid}]");
                    MD_Metadata_Type md = geoNorge.GetRecordByUuid(uuid);
                    var data = new SimpleMetadata(md);

                    //if (data.DistributionFormats != null && data.DistributionFormats.Count > 0
                    //    && !string.IsNullOrEmpty(data.DistributionFormats[0].Name) &&
                    //    data.DistributionDetails != null && !string.IsNullOrEmpty(data.DistributionDetails.Protocol))
                    //{
                    _logger.LogInformation($"Processing dataset: [title={data.Title}], [uuid={uuid}]");

                    //Map dataset to catalog
                    XmlElement catalogDataset = doc.CreateElement("dcat", "dataset", xmlnsDcat);
                    catalogDataset.SetAttribute("resource", xmlnsRdf, kartkatalogenUrl + "Metadata/uuid/" + data.Uuid);
                    catalog.AppendChild(catalogDataset);

                    XmlElement dataset = doc.CreateElement("dcat", "Dataset", xmlnsDcat);
                    dataset.SetAttribute("about", xmlnsRdf, kartkatalogenUrl + "Metadata/uuid/" + data.Uuid);
                    root.AppendChild(dataset);

                    XmlElement datasetIdentifier = doc.CreateElement("dct", "identifier", xmlnsDct);
                    datasetIdentifier.InnerText = data.Uuid.ToString();
                    dataset.AppendChild(datasetIdentifier);

                    XmlElement datasetTitle = doc.CreateElement("dct", "title", xmlnsDct);
                    datasetTitle.SetAttribute("xml:lang", "no");
                    datasetTitle.InnerText = data.Title;
                    dataset.AppendChild(datasetTitle);


                    XmlElement datasetDescription = doc.CreateElement("dct", "description", xmlnsDct);
                    datasetDescription.SetAttribute("xml:lang", "no");
                    if (!string.IsNullOrEmpty(data.Abstract))
                        datasetDescription.InnerText = data.Abstract;
                    dataset.AppendChild(datasetDescription);

                    XmlElement landingPage = doc.CreateElement("dcat", "landingPage", xmlnsDcat);
                    landingPage.SetAttribute("resource", xmlnsRdf, kartkatalogenUrl + "Metadata/uuid/" + data.Uuid);
                    dataset.AppendChild(landingPage);

                    foreach (var keyword in data.Keywords)
                    {

                        XmlElement datasetKeyword = doc.CreateElement("dcat", "keyword", xmlnsDcat);
                        datasetKeyword.SetAttribute("xml:lang", "no");
                        datasetKeyword.InnerText = keyword.Keyword;
                        dataset.AppendChild(datasetKeyword);

                    }

                    //High value dataset
                    var highValueDatasetCategories = SimpleKeyword.Filter(data.Keywords, null, SimpleKeyword.THESAURUS_HIGHVALUE_DATASET);

                    bool hasHighValueDataset = highValueDatasetCategories != null && highValueDatasetCategories.Count > 0;

                    if (hasHighValueDataset)
                    {
                        foreach (var highValueDatasetCategory in highValueDatasetCategories)
                        {
                            var aboutHighValueDatasetCategory = highValueDatasetCategory.KeywordLink;

                            if (!string.IsNullOrEmpty(aboutHighValueDatasetCategory))
                            {
                                XmlElement datasetHighValueCategory = doc.CreateElement("dcatap", "hvdCategory", xmlnsDcatAp);
                                datasetHighValueCategory.SetAttribute("resource", xmlnsRdf, aboutHighValueDatasetCategory);
                                dataset.AppendChild(datasetHighValueCategory);
                            }
                        }

                        XmlElement applicableLegislation = doc.CreateElement("dcatap", "applicableLegislation", xmlnsDcatAp);
                        applicableLegislation.SetAttribute("resource", xmlnsRdf, "http://data.europa.eu/eli/reg_impl/2023/138/oj");
                        dataset.AppendChild(applicableLegislation);
                    }

                    //Place
                    // URI for the geographic identifier
                    var places = SimpleKeyword.Filter(data.Keywords, null, SimpleKeyword.THESAURUS_ADMIN_UNITS);

                    foreach (var place in places)
                    {
                        var aboutPlace = place.KeywordLink;

                        if (!string.IsNullOrEmpty(aboutPlace))
                        {
                            XmlElement datasetLocation = doc.CreateElement("dct", "spatial", xmlnsDct);
                            datasetLocation.SetAttribute("resource", xmlnsRdf, aboutPlace);
                            dataset.AppendChild(datasetLocation);
                        }
                    }

                    //Resource metadata in GeoDCAT - AP using a geographic bounding box
                    if (data.BoundingBox != null)
                    {
                        XmlElement datasetSpatial = doc.CreateElement("dct", "spatial", xmlnsDct);
                        datasetSpatial.SetAttribute("parseType", xmlnsRdf, "Resource");

                        XmlElement spatialLocn = doc.CreateElement("locn", "geometry", xmlnsLocn);
                        spatialLocn.SetAttribute("datatype", xmlnsRdf, "http://www.opengis.net/ont/geosparql#gmlLiteral");

                        //var cdata = doc.CreateCDataSection("<gml:Envelope srsName=\"http://www.opengis.net/def/crs/OGC/1.3/CRS84\"><gml:lowerCorner>" + data.BoundingBox.WestBoundLongitude + " " + data.BoundingBox.SouthBoundLatitude + "</gml:lowerCorner><gml:upperCorner>" + data.BoundingBox.EastBoundLongitude + " " + data.BoundingBox.NorthBoundLatitude + "</gml:upperCorner></gml:Envelope>");
                        spatialLocn.InnerText = "<gml:Envelope srsName=\"http://www.opengis.net/def/crs/OGC/1.3/CRS84\"><gml:lowerCorner>" + data.BoundingBox.WestBoundLongitude + " " + data.BoundingBox.SouthBoundLatitude + "</gml:lowerCorner><gml:upperCorner>" + data.BoundingBox.EastBoundLongitude + " " + data.BoundingBox.NorthBoundLatitude + "</gml:upperCorner></gml:Envelope>";
                        //spatialLocn.AppendChild(cdata);

                        datasetSpatial.AppendChild(spatialLocn);

                        dataset.AppendChild(datasetSpatial);
                    }

                    List<string> themes = new List<string>();

                    string euLink = "http://publications.europa.eu/resource/authority/data-theme/";

                    //National theme
                    var nationalThemes = SimpleKeyword.Filter(data.Keywords, null, SimpleKeyword.THESAURUS_NATIONAL_THEME);

                    foreach (var theme in nationalThemes)
                    {
                        var aboutConcept = GetConcept(theme.Keyword);

                        if (!string.IsNullOrEmpty(aboutConcept))
                        {
                            themes.Add(aboutConcept);
                        }

                        if (Mappings.ThemeNationalToEU.ContainsKey(theme.Keyword))
                        {
                            themes.Add(euLink + Mappings.ThemeNationalToEU[theme.Keyword]);
                        }
                    }

                    //Inspire thene
                    var themeInspires = SimpleKeyword.Filter(data.Keywords, null, SimpleKeyword.THESAURUS_GEMET_INSPIRE_V1);

                    foreach (var themeInspire in themeInspires)
                    {
                        if (Mappings.ThemeInspireToEU.ContainsKey(themeInspire.Keyword))
                        {
                            if (!themes.Contains(euLink + Mappings.ThemeInspireToEU[themeInspire.Keyword]))
                                themes.Add(euLink + Mappings.ThemeInspireToEU[themeInspire.Keyword]);
                        }
                    }

                    //Concepts
                    var conceptThemes = SimpleKeyword.Filter(data.Keywords, null, SimpleKeyword.THESAURUS_CONCEPT);

                    foreach (var theme in conceptThemes)
                    {
                        var aboutConcept = theme.KeywordLink;

                        if (!string.IsNullOrEmpty(aboutConcept))
                        {

                            if (!ConceptObjects.ContainsKey(aboutConcept))
                            {
                                ConceptObjects.Add(aboutConcept, aboutConcept);
                                themes.Add(aboutConcept);
                            }
                        }
                    }

                    foreach (var theme in themes)
                    {
                        XmlElement datasetTheme;
                        if (theme.Contains("objektkatalog.geonorge.no"))
                            datasetTheme = doc.CreateElement("dct", "subject", xmlnsDct);
                        else
                            datasetTheme = doc.CreateElement("dcat", "theme", xmlnsDcat);
                        datasetTheme.SetAttribute("resource", xmlnsRdf, theme);
                        dataset.AppendChild(datasetTheme);
                    }


                    if (data.Thumbnails != null && data.Thumbnails.Count > 0)
                    {
                        XmlElement datasetThumbnail = doc.CreateElement("foaf", "thumbnail", xmlnsFoaf);
                        datasetThumbnail.SetAttribute("resource", xmlnsRdf, EncodeUrl(data.Thumbnails[0].URL));
                        dataset.AppendChild(datasetThumbnail);
                    }


                    XmlElement datasetUpdated = doc.CreateElement("dct", "updated", xmlnsDct);
                    datasetUpdated.SetAttribute("datatype", xmlnsRdf, "http://www.w3.org/2001/XMLSchema#date");
                    XmlElement datasetModified = doc.CreateElement("dct", "modified", xmlnsDct);
                    datasetModified.SetAttribute("datatype", xmlnsRdf, "http://www.w3.org/2001/XMLSchema#date");

                    if (data.DateUpdated.HasValue)
                    {
                        datasetUpdated.InnerText = data.DateUpdated.Value.ToString("yyyy-MM-dd");
                        datasetModified.InnerText = datasetUpdated.InnerText;

                        dataset.AppendChild(datasetUpdated);
                        dataset.AppendChild(datasetModified);

                        if (!catalogLastModified.HasValue || data.DateUpdated > catalogLastModified)
                            catalogLastModified = data.DateUpdated;
                    }

                    XmlElement datasetIssued = doc.CreateElement("dct", "issued", xmlnsDct);
                    datasetIssued.SetAttribute("datatype", xmlnsRdf, "http://www.w3.org/2001/XMLSchema#date");
                    if (data.DateCreated.HasValue || data.DatePublished.HasValue)
                    {
                        if (data.DateCreated.HasValue)
                            datasetIssued.InnerText = data.DateCreated.Value.ToString("yyyy-MM-dd");
                        else if (data.DatePublished.HasValue)
                            datasetIssued.InnerText = data.DatePublished.Value.ToString("yyyy-MM-dd");

                        dataset.AppendChild(datasetIssued);
                    }

                    XmlElement temporal = doc.CreateElement("dct", "temporal", xmlnsDct);
                    XmlElement periodOfTime = doc.CreateElement("dct", "PeriodOfTime", xmlnsDct);

                    string dateFrom = data.ValidTimePeriod.ValidFrom;
                    if (string.IsNullOrEmpty(dateFrom) || dateFrom == "0001-01-01")
                    {
                        if (data.DatePublished.HasValue)
                            dateFrom = data.DatePublished.Value.ToString("yyyy-MM-dd");
                        else if (data.DateCreated.HasValue)
                            dateFrom = data.DateCreated.Value.ToString("yyyy-MM-dd");
                        else if (data.DateUpdated.HasValue)
                            dateFrom = data.DateUpdated.Value.ToString("yyyy-MM-dd");
                        else if (data.DateMetadataUpdated.HasValue)
                            dateFrom = data.DateMetadataUpdated.Value.ToString("yyyy-MM-dd");
                    }
                    string dateTo = data.ValidTimePeriod.ValidTo;

                    XmlElement startDate = doc.CreateElement("dcat", "startDate", xmlnsDcat);
                    startDate.SetAttribute("datatype", xmlnsRdf, "http://www.w3.org/2001/XMLSchema#dateTime");
                    startDate.InnerText = dateFrom + "T00:00:00Z";
                    periodOfTime.AppendChild(startDate);

                    if (!string.IsNullOrEmpty(dateTo) && dateTo != "0001-01-01")
                    {
                        XmlElement endDate = doc.CreateElement("dcat", "endDate", xmlnsDcat);
                        endDate.SetAttribute("datatype", xmlnsRdf, "http://www.w3.org/2001/XMLSchema#dateTime");
                        endDate.InnerText = dateTo + "T00:00:00Z";
                        periodOfTime.AppendChild(endDate);
                    }

                    temporal.AppendChild(periodOfTime);
                    dataset.AppendChild(temporal);

                    Organization organization = null;

                    if (data.ContactOwner != null)
                    {
                        _logger.LogInformation("Looking up organization: " + data.ContactOwner.Organization);
                        Task<Organization> getOrganizationTask = _organizationService.GetOrganizationByName(data.ContactOwner.Organization);
                        organization = getOrganizationTask.Result;
                    }

                    string organizationUri = null;
                    string publisherUri = null;

                    //dct:creator => Referanse til akt�ren som er produsent av datasettet => ContactOwner.Email => foaf:Agent
                    if (data.ContactOwner != null && !string.IsNullOrEmpty(data.ContactOwner.Organization))
                    {
                        organizationUri = OrganizationsLink[data.ContactOwner.Organization].Replace("organisasjoner/kartverket/", "organisasjoner/");
                        if (!string.IsNullOrEmpty(data.ContactOwner.Email))
                        {
                            organizationUri = organizationUri + "/" + GetUsernameFromEmail(data.ContactOwner.Email);
                        }
                        XmlElement datasetCreator = doc.CreateElement("dct", "creator", xmlnsDct);
                        datasetCreator.SetAttribute("resource", xmlnsRdf, organizationUri);

                        dataset.AppendChild(datasetCreator);

                        XmlElement agent = doc.CreateElement("foaf", "Agent", xmlnsFoaf);
                        agent.SetAttribute("about", xmlnsRdf, organizationUri);

                        XmlElement agentType = doc.CreateElement("dct", "type", xmlnsDct);
                        agentType.SetAttribute("resource", xmlnsRdf, "http://purl.org/adms/publishertype/NationalAuthority");
                        agent.AppendChild(agentType);


                        if (organization != null && !string.IsNullOrEmpty(organization.Number))
                        {
                            XmlElement agentIdentifier = doc.CreateElement("dct", "identifier", xmlnsDct);
                            agentIdentifier.InnerText = organization.Number;
                            agent.AppendChild(agentIdentifier);
                        }
                        else
                        {
                            //Use number for Norwegian Mapping Authority, mostly for Geovekst
                            XmlElement agentIdentifier = doc.CreateElement("dct", "identifier", xmlnsDct);
                            agentIdentifier.InnerText = "971040238";
                            agent.AppendChild(agentIdentifier);
                        }

                        XmlElement agentName = doc.CreateElement("foaf", "name", xmlnsFoaf);
                        if (organization != null)
                            agentName.InnerText = organization.Name;
                        agent.AppendChild(agentName);

                        if (data.ContactOwner != null && !string.IsNullOrEmpty(data.ContactOwner.Email))
                        {
                            XmlElement agentMbox = doc.CreateElement("foaf", "mbox", xmlnsFoaf);
                            agentMbox.InnerText = data.ContactOwner.Email;
                            agent.AppendChild(agentMbox);
                        }

                        if (organization != null && !string.IsNullOrEmpty(organization.Number))
                        {
                            XmlElement agentSameAs = doc.CreateElement("owl", "sameAs", xmlnsOwl);
                            agentSameAs.InnerText = "http://data.brreg.no/enhetsregisteret/enhet/" + organization.Number;
                            agent.AppendChild(agentSameAs);
                        }

                        if (!foafAgents.ContainsKey(organizationUri))
                            foafAgents.Add(organizationUri, agent);
                    }

                    //dct:publisher => Referanse til en aktør (organisasjon) som er ansvarlig for å gjøre datatjenesten tilgjengelig => ContactPublisher.Email => foaf:Agent
                    if (data.ContactPublisher != null && !string.IsNullOrEmpty(data.ContactPublisher.Organization))
                    {
                        _logger.LogInformation("Looking up organization: " + data.ContactPublisher.Organization);
                        Task<Organization> getOrganizationTask = _organizationService.GetOrganizationByName(data.ContactPublisher.Organization);
                        organization = getOrganizationTask.Result;


                        organizationUri = OrganizationsLink[data.ContactPublisher.Organization].Replace("organisasjoner/kartverket/", "organisasjoner/");
                        if (!string.IsNullOrEmpty(data.ContactPublisher.Email))
                        {
                            organizationUri = organizationUri + "/" + GetUsernameFromEmail(data.ContactPublisher.Email);
                        }

                        XmlElement datasetPublisher = doc.CreateElement("dct", "publisher", xmlnsDct);
                        datasetPublisher.SetAttribute("resource", xmlnsRdf, organizationUri);

                        dataset.AppendChild(datasetPublisher);

                        XmlElement agent = doc.CreateElement("foaf", "Agent", xmlnsFoaf);
                        agent.SetAttribute("about", xmlnsRdf, organizationUri);

                        XmlElement agentType = doc.CreateElement("dct", "type", xmlnsDct);
                        agentType.SetAttribute("resource", xmlnsRdf, "http://purl.org/adms/publishertype/NationalAuthority");
                        agent.AppendChild(agentType);


                        if (organization != null && !string.IsNullOrEmpty(organization.Number))
                        {
                            XmlElement agentIdentifier = doc.CreateElement("dct", "identifier", xmlnsDct);
                            agentIdentifier.InnerText = organization.Number;
                            agent.AppendChild(agentIdentifier);
                        }
                        else
                        {
                            //Use number for Norwegian Mapping Authority, mostly for Geovekst
                            XmlElement agentIdentifier = doc.CreateElement("dct", "identifier", xmlnsDct);
                            agentIdentifier.InnerText = "971040238";
                            agent.AppendChild(agentIdentifier);
                        }

                        XmlElement agentName = doc.CreateElement("foaf", "name", xmlnsFoaf);
                        if (organization != null)
                            agentName.InnerText = organization.Name;
                        agent.AppendChild(agentName);

                        if (data.ContactPublisher != null && !string.IsNullOrEmpty(data.ContactPublisher.Email))
                        {
                            XmlElement agentMbox = doc.CreateElement("foaf", "mbox", xmlnsFoaf);
                            agentMbox.InnerText = data.ContactPublisher.Email;
                            agent.AppendChild(agentMbox);
                        }

                        if (organization != null && !string.IsNullOrEmpty(organization.Number))
                        {
                            XmlElement agentSameAs = doc.CreateElement("owl", "sameAs", xmlnsOwl);
                            agentSameAs.InnerText = "http://data.brreg.no/enhetsregisteret/enhet/" + organization.Number;
                            agent.AppendChild(agentSameAs);
                        }

                        if (!foafAgents.ContainsKey(organizationUri))
                            foafAgents.Add(organizationUri, agent);

                        publisherUri = organizationUri;

                    }

                    //dcat:contactPoint => Referanse til kontaktpunkt med kontaktopplysninger. Disse kan brukes til � sende kommentarer om datatjenesten. => ContactMetadata.Email => vcard:Kind
                    if (data.ContactMetadata != null && !string.IsNullOrEmpty(data.ContactMetadata.Organization))
                    {

                        _logger.LogInformation("Looking up organization: " + data.ContactMetadata.Organization);
                        Task<Organization> getOrganizationTask = _organizationService.GetOrganizationByName(data.ContactMetadata.Organization);
                        organization = getOrganizationTask.Result;

                        organizationUri = OrganizationsLink[data.ContactMetadata.Organization].Replace("organisasjoner/kartverket/", "organisasjoner/");
                        if (!string.IsNullOrEmpty(data.ContactMetadata.Email))
                        {
                            organizationUri = organizationUri + "/" + GetUsernameFromEmail(data.ContactMetadata.Email);
                        }
                        XmlElement datasetContactPoint = doc.CreateElement("dcat", "contactPoint", xmlnsDcat);
                        datasetContactPoint.SetAttribute("resource", xmlnsRdf, organizationUri);
                        dataset.AppendChild(datasetContactPoint);

                        XmlElement datasetKind = doc.CreateElement("vcard", "Organization", xmlnsVcard);
                        datasetKind.SetAttribute("about", xmlnsRdf, organizationUri);

                        XmlElement datasetOrganizationName = doc.CreateElement("vcard", "organization-unit", xmlnsVcard);
                        datasetOrganizationName.SetAttribute("xml:lang", "");
                        if (organization != null)
                            datasetOrganizationName.InnerText = organization.Name;
                        datasetKind.AppendChild(datasetOrganizationName);

                        if (data.ContactMetadata != null && !string.IsNullOrEmpty(data.ContactMetadata.Email))
                        {
                            XmlElement datasetHasEmail = doc.CreateElement("vcard", "hasEmail", xmlnsVcard);
                            datasetHasEmail.SetAttribute("resource", xmlnsRdf, "mailto:" + data.ContactMetadata.Email);
                            datasetKind.AppendChild(datasetHasEmail);
                        }
                        if (!vcardKinds.ContainsKey(organizationUri))
                            vcardKinds.Add(organizationUri, datasetKind);

                    }

                    XmlElement datasetAccrualPeriodicity = doc.CreateElement("dct", "accrualPeriodicity", xmlnsDct);
                    if (!string.IsNullOrEmpty(data.MaintenanceFrequency))
                        datasetAccrualPeriodicity.SetAttribute("resource", xmlnsRdf, MapMaintenanceFrequency(data.MaintenanceFrequency));
                    dataset.AppendChild(datasetAccrualPeriodicity);

                    XmlElement datasetGranularity = doc.CreateElement("dcat", "granularity", xmlnsDcat);
                    if (!string.IsNullOrEmpty(data.ResolutionScale))
                        datasetGranularity.InnerText = data.ResolutionScale;
                    dataset.AppendChild(datasetGranularity);

                    XmlElement datasetLicense = doc.CreateElement("dct", "license", xmlnsDct);
                    if (data.Constraints != null && !string.IsNullOrEmpty(data.Constraints.UseConstraintsLicenseLink))
                        datasetLicense.SetAttribute("resource", xmlnsRdf, MapLicense(data.Constraints.UseConstraintsLicenseLink));
                    dataset.AppendChild(datasetLicense);

                    var accessConstraint = "PUBLIC";
                    XmlElement datasetAccess = doc.CreateElement("dct", "accessRights", xmlnsDct);
                    if (data.Constraints != null
                        && !string.IsNullOrEmpty(data.Constraints.AccessConstraints))
                    {
                        if (data.Constraints.AccessConstraints.ToLower() == "restricted")
                            accessConstraint = "NON-PUBLIC";
                        else if (data.Constraints.AccessConstraints == "norway digital restricted" || (data.Constraints.AccessConstraints == "otherRestrictions" && !string.IsNullOrEmpty(data.Constraints.OtherConstraintsAccess)
                            && data.Constraints.OtherConstraintsAccess.ToLower() == "norway digital restricted"))
                        {
                            accessConstraint = "RESTRICTED";
                        }
                    }
                    datasetAccess.SetAttribute("resource", xmlnsRdf, "http://publications.europa.eu/resource/authority/access-right/" + accessConstraint);
                    dataset.AppendChild(datasetAccess);


                    XmlElement datasetDataQuality = doc.CreateElement("dcat", "dataQuality", xmlnsDcat);
                    if (!string.IsNullOrEmpty(data.ProcessHistory))
                        datasetDataQuality.InnerText = data.ProcessHistory;
                    dataset.AppendChild(datasetDataQuality);

                    string org = null;
                    if (organization != null) org = organization?.Name;


                    //Distribution
                    if (data.DistributionsFormats != null)
                    {
                        distributionFormats = new List<string>();

                        foreach (var distro in data.DistributionsFormats)
                        {

                            if (string.IsNullOrEmpty(distro.FormatName))
                                distro.FormatName = "ZIP";

                            if (!string.IsNullOrEmpty(distro.FormatName) && !distributionFormats.Contains(distro.FormatName))
                            {

                                bool isDataService = !string.IsNullOrEmpty(distro.Protocol) && DataserviceProtocols.Contains(distro.Protocol);


                                    //Map distribution to dataset
                                    XmlElement distributionDataset = doc.CreateElement("dcat", "distribution", xmlnsDcat);
                                    distributionDataset.SetAttribute("resource", xmlnsRdf, kartkatalogenUrl + "Metadata/uuid/" + data.Uuid + "/" + HttpUtility.UrlEncode(distro.FormatName));
                                    dataset.AppendChild(distributionDataset);

                                    XmlElement distribution = doc.CreateElement("dcat", "Distribution", xmlnsDcat);
                                    distribution.SetAttribute("about", xmlnsRdf, kartkatalogenUrl + "Metadata/uuid/" + data.Uuid + "/" + HttpUtility.UrlEncode(distro.FormatName));
                                    root.AppendChild(distribution);

                                    XmlElement distributionTitle = doc.CreateElement("dct", "title", xmlnsDct);
                                    distributionTitle.SetAttribute("xml:lang", "no");
                                    if (distro.Protocol != null && !string.IsNullOrEmpty(distro.Protocol))
                                        distributionTitle.InnerText = GetDistributionTitle(distro.Protocol);

                                    XmlElement distributionDescription = doc.CreateElement("dct", "description", xmlnsDct);
                                    if (distro.Protocol != null && !string.IsNullOrEmpty(distro.Protocol))
                                        distributionDescription.InnerText = GetDistributionDescription(distro.Protocol);
                                    distribution.AppendChild(distributionDescription);

                                    XmlElement distributionFormat = doc.CreateElement("dct", "format", xmlnsDct);
                                    if (FormatUrls.ContainsKey(distro.FormatName))
                                    {
                                        distributionFormat.SetAttribute("resource", xmlnsRdf, FormatUrls[distro.FormatName]);
                                    }
                                    else
                                    {
                                        distributionFormat.SetAttribute("resource", xmlnsRdf, "http://publications.europa.eu/resource/authority/file-type/OCTET");
                                        distributionTitle.InnerText = distributionTitle.InnerText + " " + distro.FormatName;
                                    }
                                    distribution.AppendChild(distributionFormat);

                                    XmlElement distributionMediaType = doc.CreateElement("dcat", "mediaType", xmlnsDcat);
                                    if (MediaTypes.ContainsKey(distro.FormatName))
                                    {
                                        distributionMediaType.SetAttribute("resource", xmlnsRdf, MediaTypes[distro.FormatName]);
                                    }
                                    else
                                    {
                                        distributionMediaType.SetAttribute("resource", xmlnsRdf, "https://www.iana.org/assignments/media-types/application/octet-stream");
                                    }
                                    distribution.AppendChild(distributionMediaType);

                                    distribution.AppendChild(distributionTitle);

                                    XmlElement distributionAccessURL = doc.CreateElement("dcat", "accessURL", xmlnsDcat);
                                    if (!string.IsNullOrEmpty(distro.URL) && distro.Protocol != null && (distro.Protocol == "GEONORGE:FILEDOWNLOAD" || distro.Protocol == "WWW:DOWNLOAD-1.0-http--download"))
                                        distributionAccessURL.SetAttribute("resource", xmlnsRdf, distro.URL);
                                    else
                                        distributionAccessURL.SetAttribute("resource", xmlnsRdf, kartkatalogenUrl + "metadata/uuid/" + uuid);

                                    distribution.AppendChild(distributionAccessURL);

                                    if (!string.IsNullOrEmpty(distro.URL) && distro.Protocol != null && (distro.Protocol == "GEONORGE:FILEDOWNLOAD" || distro.Protocol == "WWW:DOWNLOAD-1.0-http--download"))
                                    {
                                        XmlElement downloadURL = doc.CreateElement("dcat", "downloadURL", xmlnsDcat);
                                        downloadURL.SetAttribute("resource", xmlnsRdf, distro.URL);
                                        distribution.AppendChild(downloadURL);
                                    }

                                    XmlElement distributionLicense = doc.CreateElement("dct", "license", xmlnsDct);
                                    if (data.Constraints != null && !string.IsNullOrEmpty(data.Constraints.UseConstraintsLicenseLink))
                                        distributionLicense.SetAttribute("resource", xmlnsRdf, MapLicense(data.Constraints.UseConstraintsLicenseLink));
                                    distribution.AppendChild(distributionLicense);

                                    //XmlElement distributionStatus = doc.CreateElement("adms", "status", xmlnsAdms);
                                    //if (!string.IsNullOrEmpty(data.Status))
                                    //    distributionStatus.SetAttribute("resource", xmlnsRdf, "http://purl.org/adms/status/" + data.Status);
                                    //distribution.AppendChild(distributionStatus);

                                    if (hasHighValueDataset)
                                    {

                                        XmlElement applicableLegislation = doc.CreateElement("dcatap", "applicableLegislation", xmlnsDcatAp);
                                        applicableLegislation.SetAttribute("resource", xmlnsRdf, "http://data.europa.eu/eli/reg_impl/2023/138/oj");
                                        distribution.AppendChild(applicableLegislation);

                                    }

                                if (isDataService) {
                                    XmlElement accessService = doc.CreateElement("dcat", "accessService", xmlnsDcat);
                                    accessService.SetAttribute("resource", xmlnsRdf, distro.URL);
                                    distribution.AppendChild(accessService);

                                    if (!dataServices.ContainsKey(distro.URL))
                                    {
                                        distribution = CreateXmlElementForDataservice(dataset, data, null, distro.Protocol,
                                        null, distro.URL, distro.FormatName, publisherUri, organizationUri, org, vcardKinds);

                                        dataServices.Add(distro.URL, distribution);
                                    }
                                }

                                distributionFormats.Add(distro.FormatName);
                                }
                        }

                        // Dataset distributions and services
                        AddServiceAndDistributions(uuid, dataset, data, services, publisherUri, organizationUri, org, vcardKinds);

                    }
                    //}

                }
                catch (Exception e)
                {
                    _logger.LogError($"Error processing dataset: {e}");
                }
            }

            foreach (var foafAgent in foafAgents)
            {
                root.AppendChild(foafAgent.Value);
            }

            foreach (var vcardKind in vcardKinds)
            {
                root.AppendChild(vcardKind.Value);
            }

            foreach (var service in services)
            {
                root.AppendChild(service.Value);
            }

            AppendConcepts(root);

        }

        private string GetUsernameFromEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
                return "";

            if (email.Contains("@"))
                email = email.Split('@')[0];

            email = email.Replace("@", "_").Replace(".", "_");

            email = email.ToLower();

            return email;
        }

        private string MapMaintenanceFrequency(string maintenanceFrequency)
        {
            if (maintenanceFrequency == "asNeeded")
                return "http://publications.europa.eu/resource/authority/frequency/AS_NEEDED";
            else if (maintenanceFrequency == "weekly")
                return "http://publications.europa.eu/resource/authority/frequency/WEEKLY";
            else if (maintenanceFrequency == "notPlanned")
                return "http://publications.europa.eu/resource/authority/frequency/NOT_PLANNED";
            else if (maintenanceFrequency == "asNeeded")
                return "http://publications.europa.eu/resource/authority/frequency/AS_NEEDED";
            else if (maintenanceFrequency == "biannually")
                return "http://publications.europa.eu/resource/authority/frequency/DECENNIAL";
            else if (maintenanceFrequency == "fortnightly")
                return "http://publications.europa.eu/resource/authority/frequency/WEEKLY_2";
            else if (maintenanceFrequency == "UNKNOWN")
                return "http://publications.europa.eu/resource/authority/frequency/UNKNOWN";
            else if (maintenanceFrequency == "continual")
                return "http://publications.europa.eu/resource/authority/frequency/UPDATE_CONT";
            else if (maintenanceFrequency == "quarterly")
                return "http://publications.europa.eu/resource/authority/frequency/QUARTERLY";
            else if (maintenanceFrequency == "irregular")
                return "http://publications.europa.eu/resource/authority/frequency/IRREG";
            else if (maintenanceFrequency == "monthly")
                return "http://publications.europa.eu/resource/authority/frequency/MONTHLY";
            else if (maintenanceFrequency == "daily")
                return "http://publications.europa.eu/resource/authority/frequency/DAILY";
            else if (maintenanceFrequency == "annually")
                return "http://publications.europa.eu/resource/authority/frequency/ANNUAL";
            else if (maintenanceFrequency == "biannually")
                return "http://publications.europa.eu/resource/authority/frequency/ANNUAL2";
            else
                return "http://publications.europa.eu/resource/authority/frequency/UNKNOWN";
        }

        private string MapLicense(string link)
        {
            if (link == "http://data.norge.no/nlod/no/1.0")
                link = "http://publications.europa.eu/resource/authority/licence/NLOD_1_0";
            else if (link == "https://data.norge.no/nlod/no/2.0")
                link = "http://publications.europa.eu/resource/authority/licence/NLOD_2_0";
            else if (link == "https://creativecommons.org/publicdomain/zero/1.0/")
                link = "http://publications.europa.eu/resource/authority/licence/CC0";
            else if (link == "http://creativecommons.org/licenses/by/3.0/no/")
                link = "http://publications.europa.eu/resource/authority/licence/CC_BY_3_0";
            else if (link == "https://creativecommons.org/licenses/by/4.0/" || link == "https://creativecommons.org/licenses/by/4.0/deed.no")
                link = "http://publications.europa.eu/resource/authority/licence/CC_BY_4_0";
            else if (link == "https://creativecommons.org/licenses/by-nc/4.0/")
                link = "http://publications.europa.eu/resource/authority/licence/CC_BYNC_4_0";

            return link;
        }

        private void AddServiceAndDistributions(string uuid, XmlElement dataset, SimpleMetadata data, Dictionary<string, XmlNode> services
            , string publisherUri, string organizationUri, string organization, Dictionary<string, XmlNode> vcardKinds)
        {
            // Get distribution from index in kartkatalog 
            string metadataUrl = _settings["KartkatalogenUrl"] + "api/getdata/" + uuid;

            try
            {
                _logger.LogInformation("Looking up distributions");

                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var httpClient = _httpClientFactory.GetHttpClient();
                var json = httpClient.GetStringAsync(metadataUrl).Result;
                stopwatch.Stop();
                _logger.LogDebug($"Distribution lookup for [uuid={uuid}] [timespent={stopwatch.ElapsedMilliseconds}ms]");

                dynamic metadata = Newtonsoft.Json.Linq.JObject.Parse(json);

                if (metadata != null && metadata != null && metadata.Distributions.RelatedViewServices != null)
                {
                    foreach (var related in metadata.Distributions.RelatedViewServices)
                    {
                        var uuidService = related.Uuid.Value;

                        var protocol = related.Protocol.Value;
                        var serviceDistributionUrl = related.DistributionUrl.Value;

                        var distroFormats = related?.DistributionFormats;
                        if (distroFormats is not null)
                        {
                            var format = distroFormats[0]?.Name?.Value;

                            if(format == null || distributionFormats.Contains(format))
                                continue;

                            distributionFormats.Add(format);

                            if (dataServices.ContainsKey(serviceDistributionUrl))
                                continue;

                            string protocolName = protocol;
                            if (protocolName.Contains("-"))
                                protocolName = protocolName.Split('-')[0];

                            protocol = "OGC:" + protocolName;

                            if (protocol == "OGC:WMS" || protocol == "OGC:WFS" || protocol == "OGC:WCS" || protocol == "OGC:API-Features" || protocol == "OGC:API-Processes")
                            {
                                var distribution = CreateXmlElementForDistribution(dataset, data, uuidService, protocol,
                                            protocolName, serviceDistributionUrl, format);
                                if (!services.ContainsKey(serviceDistributionUrl))
                                    services.Add(serviceDistributionUrl, distribution);

                                if (!dataServices.ContainsKey(serviceDistributionUrl)) 
                                {
                                    distribution = CreateXmlElementForDataservice(dataset, data, uuidService, protocol,
                                    protocolName, serviceDistributionUrl, format, publisherUri, organizationUri, organization, vcardKinds);

                                    dataServices.Add(serviceDistributionUrl, distribution);
                                }
                            }
                        }
                    }
                }
                if (metadata != null && metadata != null && metadata.DistributionsFormats != null)
                {
                    foreach (var distro in metadata.DistributionsFormats)
                    {
                        if (distro.Protocol == "W3C:AtomFeed" && distro.FormatName == "GML")
                        {
                            var serviceDistributionUrl = kartkatalogenUrl + "Metadata/uuid/" + metadata.Uuid.Value + "/atom/GML";

                            string? urlDownload = distro?.URL?.Value;
                            if(string.IsNullOrEmpty(urlDownload))
                                continue;
                            if (!services.ContainsKey(serviceDistributionUrl))
                            {
                                var distribution = CreateXmlElementForDistributionAtomFeed(dataset, data, serviceDistributionUrl, urlDownload);

                                //Map distribution to dataset
                                XmlElement distributionDataset = doc.CreateElement("dcat", "distribution", xmlnsDcat);
                                distributionDataset.SetAttribute("resource", xmlnsRdf, serviceDistributionUrl);
                                dataset.AppendChild(distributionDataset);

                                services.Add(serviceDistributionUrl, distribution);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Unable to fetch distributions from: [url={metadataUrl}], [message={e.Message}]");
            }
        }

        private XmlNode CreateXmlElementForDistributionAtomFeed(XmlElement dataset, SimpleMetadata data, dynamic serviceDistributionUrl, string urlDownload)
        {
            XmlElement distribution = doc.CreateElement("dcat", "Distribution", xmlnsDcat);
            distribution.SetAttribute("about", xmlnsRdf, serviceDistributionUrl);

            XmlElement distributionTitle = doc.CreateElement("dct", "title", xmlnsDct);
            distributionTitle.SetAttribute("xml:lang", "no");
            distributionTitle.InnerText = "Atom Feed";
            distribution.AppendChild(distributionTitle);

            XmlElement distributionDescription = doc.CreateElement("dct", "description", xmlnsDct);
            distributionDescription.SetAttribute("xml:lang", "no");
            distributionDescription.InnerText = "Nedlasting gjennom Atom Feed";
            distribution.AppendChild(distributionDescription);

            XmlElement distributionFormat = doc.CreateElement("dct", "format", xmlnsDct);
            distributionFormat.SetAttribute("resource", xmlnsRdf, "http://publications.europa.eu/resource/authority/file-type/XML");
            distribution.AppendChild(distributionFormat);

            XmlElement mediaType = doc.CreateElement("dcat", "mediaType", xmlnsDcat);
            mediaType.SetAttribute("resource", xmlnsRdf, "https://www.iana.org/assignments/media-types/application/octet-stream");
            distribution.AppendChild(mediaType);

            XmlElement distributionAccessURL = doc.CreateElement("dcat", "accessURL", xmlnsDcat);
            distributionAccessURL.SetAttribute("resource", xmlnsRdf, kartkatalogenUrl + "Metadata/uuid/" + data.Uuid);
            distribution.AppendChild(distributionAccessURL);

            XmlElement downloadURL = doc.CreateElement("dcat", "downloadURL", xmlnsDcat);
            downloadURL.SetAttribute("resource", xmlnsRdf, urlDownload);
            distribution.AppendChild(downloadURL);

            XmlElement distributionLicense = doc.CreateElement("dct", "license", xmlnsDct);
            if (data.Constraints != null && !string.IsNullOrEmpty(data.Constraints.OtherConstraintsLink))
                distributionLicense.SetAttribute("resource", xmlnsRdf, data.Constraints.OtherConstraintsLink);
            distribution.AppendChild(distributionLicense);

            XmlElement distributionStatus = doc.CreateElement("adms", "status", xmlnsAdms);
            distributionStatus.SetAttribute("resource", xmlnsRdf, "http://purl.org/adms/status/historicalArchive");
            distribution.AppendChild(distributionStatus);

            return distribution;
        }

        private static string SanitizeUrlForRdf(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            // Percent-encode characters that are illegal in URIs but may appear in templated endpoints
            // Already handled elsewhere: spaces, commas, [ and ]
            return url
                .Replace("{", "%7B")
                .Replace("}", "%7D");
        }

        private XmlElement CreateXmlElementForDataservice(XmlElement dataset, SimpleMetadata data, dynamic uuidService,
                dynamic protocol, string protocolName, dynamic serviceDistributionUrl, string format, string publisherUri, string organizationUri,
                string organization, Dictionary<string, XmlNode> vcardKinds)
        {
            serviceDistributionUrl = SanitizeUrlForRdf(serviceDistributionUrl);

            XmlElement dataService = docService.CreateElement("dcat", "DataService", xmlnsDcat);
            dataService.SetAttribute("about", xmlnsRdf, serviceDistributionUrl);

            // dcat:servesDataset
            XmlElement servesDataset = docService.CreateElement("dcat", "servesDataset", xmlnsDcat);
            servesDataset.SetAttribute("resource", xmlnsRdf, kartkatalogenUrl + "Metadata/uuid/" + data.Uuid);
            dataService.AppendChild(servesDataset);

            // dcat:endpointURL
            XmlElement endpoint = docService.CreateElement("dcat", "endpointURL", xmlnsDcat);
            endpoint.SetAttribute("resource", xmlnsRdf, serviceDistributionUrl);
            dataService.AppendChild(endpoint);

            // dct:title
            XmlElement titleEl = docService.CreateElement("dct", "title", xmlnsDct);
            titleEl.SetAttribute("xml:lang", "nb");
            titleEl.InnerText = GetDistributionTitle(protocol);
            dataService.AppendChild(titleEl);

            // dct:publisher
            if (string.IsNullOrEmpty(publisherUri))
            {
                publisherUri = organizationUri;
            }
            XmlElement publisher = docService.CreateElement("dct", "publisher", xmlnsDct);
            publisher.SetAttribute("resource", xmlnsRdf, publisherUri);
            dataService.AppendChild(publisher);

            // contactPoint selection
            string contactPointUri = string.IsNullOrEmpty(organization)
                ? organizationUri
                : vcardKinds.FirstOrDefault(v => v.Value.InnerText == organization).Key;

            if (string.IsNullOrEmpty(contactPointUri))
            {
                contactPointUri = organizationUri;
            }

            XmlElement contactPoint = docService.CreateElement("dcat", "contactPoint", xmlnsDcat);
            contactPoint.SetAttribute("resource", xmlnsRdf, contactPointUri);
            dataService.AppendChild(contactPoint);

            // dct:license
            XmlElement license = docService.CreateElement("dct", "license", xmlnsDct);
            if (data.Constraints != null && !string.IsNullOrEmpty(data.Constraints.UseConstraintsLicenseLink))
                license.SetAttribute("resource", xmlnsRdf, MapLicense(data.Constraints.UseConstraintsLicenseLink));
            dataService.AppendChild(license);

            // dct:format 
            XmlElement distributionFormat = docService.CreateElement("dct", "format", xmlnsDct);
            if (FormatUrls.ContainsKey(format))
            {
                distributionFormat.SetAttribute("resource", xmlnsRdf, FormatUrls[format]);
            }
            else { distributionFormat.InnerText = format; }

            dataService.AppendChild(distributionFormat);

            distributionFormats.Add(format);

            // Resolve Organization.Number (identifier) from organization name (if present)
            string? agentIdentifier = null;
            string? orgIdentifier = null;
            string emailIdentifier = data?.ContactMetadata?.Email;
            if (string.IsNullOrEmpty(emailIdentifier))
                emailIdentifier = data?.ContactOwner?.Email;
            if (string.IsNullOrEmpty(emailIdentifier))
                emailIdentifier = data?.ContactPublisher?.Email;

            try
            {
                if (!string.IsNullOrEmpty(organization))
                {
                    var orgInfo = _organizationService.GetOrganizationByName(organization).Result;
                    agentIdentifier = orgInfo?.Number;
                    orgIdentifier = orgInfo?.Number;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Organization lookup failed for [{organization}]: {ex.Message}");
            }

            // Ensure foaf:Agent and vcard:Organization include identifiers and type
            EnsureAgentAndContactPointNodes(
                publisherUri,
                contactPointUri,
                organization,
                agentIdentifier: agentIdentifier,
                orgIdentifier: orgIdentifier,
                contactEmail: emailIdentifier);

            return dataService;

        }

        private XmlElement CreateXmlElementForDistribution(XmlElement dataset, SimpleMetadata data, dynamic uuidService,
            dynamic protocol, string protocolName, dynamic serviceDistributionUrl, string format)
        {
            serviceDistributionUrl = SanitizeUrlForRdf(serviceDistributionUrl);

            XmlElement distributionDataset = doc.CreateElement("dcat", "distribution", xmlnsDcat);
            distributionDataset.SetAttribute("resource", xmlnsRdf,
                kartkatalogenUrl + "Metadata/uuid/" + uuidService + "/" + HttpUtility.UrlEncode(format));
            dataset.AppendChild(distributionDataset);

            XmlElement distribution = doc.CreateElement("dcat", "Distribution", xmlnsDcat);
            distribution.SetAttribute("about", xmlnsRdf, kartkatalogenUrl + "Metadata/uuid/" + uuidService + "/" + HttpUtility.UrlEncode(format));

            XmlElement distributionTitle = doc.CreateElement("dct", "title", xmlnsDct);
            distributionTitle.SetAttribute("xml:lang", "no");
            distributionTitle.InnerText = GetDistributionTitle(protocol);
            distribution.AppendChild(distributionTitle);

            distributionTitle = doc.CreateElement("dct", "title", xmlnsDct);
            distributionTitle.SetAttribute("xml:lang", "en");
            distributionTitle.InnerText = protocol;
            distribution.AppendChild(distributionTitle);

            XmlElement distributionDescription = doc.CreateElement("dct", "description", xmlnsDct);
            distributionDescription.SetAttribute("xml:lang", "no");
            distributionDescription.InnerText = GetDistributionDescription(protocol);
            distribution.AppendChild(distributionDescription);

            distributionDescription = doc.CreateElement("dct", "description", xmlnsDct);
            distributionDescription.SetAttribute("xml:lang", "en");
            distributionDescription.InnerText = "View Service (" + protocolName + ")";
            distribution.AppendChild(distributionDescription);

            XmlElement distributionFormat = doc.CreateElement("dct", "format", xmlnsDct);

            if (FormatUrls.ContainsKey(protocolName))
            {
                distributionFormat.SetAttribute("resource", xmlnsRdf, FormatUrls[protocolName]);
            }
            else { distributionFormat.InnerText = protocolName; }

            distribution.AppendChild(distributionFormat);

            XmlElement distributionAccessURL = doc.CreateElement("dcat", "accessURL", xmlnsDcat);
            distributionAccessURL.SetAttribute("resource", xmlnsRdf, serviceDistributionUrl);
            distribution.AppendChild(distributionAccessURL);

            XmlElement distributionLicense = doc.CreateElement("dct", "license", xmlnsDct);
            if (data.Constraints != null && !string.IsNullOrEmpty(data.Constraints.OtherConstraintsLink))
                distributionLicense.SetAttribute("resource", xmlnsRdf, data.Constraints.OtherConstraintsLink);
            distribution.AppendChild(distributionLicense);

            XmlElement accessService = doc.CreateElement("dcat", "accessService", xmlnsDcat);
            accessService.SetAttribute("resource", xmlnsRdf, serviceDistributionUrl);
            distribution.AppendChild(accessService);

            return distribution;
        }

        private void AppendConcepts(XmlElement root)
        {
            var httpClient = _httpClientFactory.GetHttpClient();

            foreach (var conceptObject in ConceptObjects)
            {
                string url = conceptObject.Value;
                _logger.LogInformation($"Looking up concept from [url={url}]");
                try
                {
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Accept", "application/rdf+xml");
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    HttpResponseMessage response = httpClient.SendAsync(request).Result;
                    stopwatch.Stop();

                    _logger.LogDebug($"Concept lookup for [url={url}] [timespent={stopwatch.ElapsedMilliseconds}ms]");

                    if (response.IsSuccessStatusCode)
                    {
                        string data = response.Content.ReadAsStringAsync().Result;

                        var conceptObjectDoc = new XmlDocument();
                        conceptObjectDoc.LoadXml(data);
                        var concepts = conceptObjectDoc.SelectNodes("//skos:Concept", nsmgr);
                        foreach (XmlNode concept in concepts)
                        {
                            XmlNode import = doc.ImportNode(concept, true);
                            root.AppendChild(import);
                        }
                    }
                    else
                    {
                        _logger.LogError(
                            $"Unable to fetch concept from [url={url}], [responseStatusCode={response.StatusCode}], [responseContent={response.Content.ReadAsStringAsync().Result}");
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"Unable to fetch concept from [url={url}], [message={e.Message}]", e);
                }
            }
        }

        private XmlElement CreateCatalog(XmlElement root)
        {
            //Catalog info
            XmlElement catalog = doc.CreateElement("dcat", "Catalog", xmlnsDcat);
            catalog.SetAttribute("about", xmlnsRdf, "http://www.geonorge.no/geonetwork");
            root.AppendChild(catalog);

            XmlElement catalogTitle = doc.CreateElement("dct", "title", xmlnsDct);
            catalogTitle.SetAttribute("xml:lang", "no");
            catalogTitle.InnerText = "Geonorge";
            catalog.AppendChild(catalogTitle);

            XmlElement catalogIdentifier = doc.CreateElement("dct", "identifier", xmlnsDct);
            catalogIdentifier.InnerText = "http://www.geonorge.no/geonetwork";
            catalog.AppendChild(catalogIdentifier);

            XmlElement catalogDescription = doc.CreateElement("dct", "description", xmlnsDct);
            catalogDescription.InnerText = "GeoNorge er den nasjonale katalogen for geografisk informasjon";
            catalog.AppendChild(catalogDescription);

            XmlElement catalogIssued = doc.CreateElement("dct", "issued", xmlnsDct);
            catalogIssued.SetAttribute("datatype", xmlnsRdf, "http://www.w3.org/2001/XMLSchema#date");
            catalogIssued.InnerText = DateTime.Now.ToString("yyyy-MM-dd");
            catalog.AppendChild(catalogIssued);


            XmlElement catalogLabel = doc.CreateElement("rdfs", "label", xmlnsRdfs);
            catalogLabel.SetAttribute("xml:lang", "no");
            catalogLabel.InnerText = "GeoNorge";
            catalog.AppendChild(catalogLabel);

            //XmlElement catalogHomePage = doc.CreateElement("foaf", "homepage", xmlnsFoaf);
            //catalogHomePage.InnerText = "http://www.geonorge.no/geonetwork";
            //catalog.AppendChild(catalogHomePage);

            XmlElement catalogOpenSearchDescription = doc.CreateElement("void", "openSearchDescription", xmlnsVoid);
            catalogOpenSearchDescription.InnerText = "http://www.geonorge.no/geonetwork/srv/nor/portal.opensearch";
            catalog.AppendChild(catalogOpenSearchDescription);

            XmlElement catalogUriLookupEndpoint = doc.CreateElement("void", "uriLookupEndpoint", xmlnsVoid);
            catalogUriLookupEndpoint.InnerText = "http://www.geonorge.no/geonetwork/srv/nor/rdf.search?any=";
            catalog.AppendChild(catalogUriLookupEndpoint);

            XmlElement catalogPublisher = doc.CreateElement("dct", "publisher", xmlnsDct);

            catalogPublisher.SetAttribute("resource", xmlnsRdf, "https://register.geonorge.no/organisasjoner/kartverket");

            catalog.AppendChild(catalogPublisher);

            XmlElement catalogLicense = doc.CreateElement("dct", "license", xmlnsDct);
            catalogLicense.SetAttribute("resource", xmlnsRdf, "http://publications.europa.eu/resource/authority/licence/CC_BY_4_0");
            catalog.AppendChild(catalogLicense);

            XmlElement catalogLanguage = doc.CreateElement("dct", "language", xmlnsDct);
            catalogLanguage.SetAttribute("resource", xmlnsRdf, "http://publications.europa.eu/resource/authority/language/NOR");
            catalog.AppendChild(catalogLanguage);

            XmlElement catalogThemeTaxonomy = doc.CreateElement("dcat", "themeTaxonomy", xmlnsDcat);
            catalogThemeTaxonomy.SetAttribute("resource", xmlnsRdf, "http://www.eionet.europa.eu/gemet/inspire_themes");
            catalog.AppendChild(catalogThemeTaxonomy);

            catalogThemeTaxonomy = doc.CreateElement("dcat", "themeTaxonomy", xmlnsDcat);
            catalogThemeTaxonomy.SetAttribute("resource", xmlnsRdf, "http://publications.europa.eu/mdr/resource/authority/data-theme/html/data-theme-eng.html");
            catalog.AppendChild(catalogThemeTaxonomy);

            catalogThemeTaxonomy = doc.CreateElement("dcat", "themeTaxonomy", xmlnsDcat);
            catalogThemeTaxonomy.SetAttribute("resource", xmlnsRdf, "https://register.geonorge.no/metadata-kodelister/nasjonal-temainndeling");
            catalog.AppendChild(catalogThemeTaxonomy);

            return catalog;
        }

        private XmlElement CreateCatalogService(XmlElement rootService)
        {
            //Catalog info
            XmlElement catalog = docService.CreateElement("dcat", "Catalog", xmlnsDcat);
            catalog.SetAttribute("about", xmlnsRdf, "https://kartkatalog.geonorge.no?type=service");
            rootService.AppendChild(catalog);

            XmlElement catalogTitle = docService.CreateElement("dct", "title", xmlnsDct);
            catalogTitle.SetAttribute("xml:lang", "no");
            catalogTitle.InnerText = "Data services Geonorge";
            catalog.AppendChild(catalogTitle);

            XmlElement catalogIdentifier = docService.CreateElement("dct", "identifier", xmlnsDct);
            catalogIdentifier.InnerText = "https://kartkatalog.geonorge.no?type=service";
            catalog.AppendChild(catalogIdentifier);

            XmlElement catalogDescription = docService.CreateElement("dct", "description", xmlnsDct);
            catalogDescription.InnerText = "GeoNorge er den nasjonale katalogen for geografisk informasjon";
            catalog.AppendChild(catalogDescription);

            XmlElement catalogIssued = docService.CreateElement("dct", "issued", xmlnsDct);
            catalogIssued.SetAttribute("datatype", xmlnsRdf, "http://www.w3.org/2001/XMLSchema#date");
            catalogIssued.InnerText = DateTime.Now.ToString("yyyy-MM-dd");
            catalog.AppendChild(catalogIssued);

            // Add foaf:Agent for Kartverket
            XmlElement foafAgent = docService.CreateElement("foaf", "Agent", xmlnsFoaf);
            foafAgent.SetAttribute("about", xmlnsRdf, "https://register.geonorge.no/organisasjoner/kartverket");

            XmlElement agentIdentifier = docService.CreateElement("dct", "identifier", xmlnsDct);
            agentIdentifier.InnerText = "971040238";
            foafAgent.AppendChild(agentIdentifier);

            // dct:type -> NationalAuthority
            var agentType = docService.CreateElement("dct", "type", xmlnsDct);
            agentType.SetAttribute("resource", xmlnsRdf, "http://purl.org/adms/publishertype/NationalAuthority");
            foafAgent.AppendChild(agentType);

            XmlElement agentSameAs = docService.CreateElement("owl", "sameAs", xmlnsOwl);
            agentSameAs.InnerText = "https://data.brreg.no/enhetsregisteret/api/enheter/971040238";
            foafAgent.AppendChild(agentSameAs);

            rootService.AppendChild(foafAgent);

            XmlElement catalogPublisher = docService.CreateElement("dct", "publisher", xmlnsDct);
            catalogPublisher.SetAttribute("resource", xmlnsRdf, "https://register.geonorge.no/organisasjoner/kartverket");
            catalog.AppendChild(catalogPublisher);

            XmlElement catalogLanguage = docService.CreateElement("dct", "language", xmlnsDct);
            catalogLanguage.SetAttribute("resource", xmlnsRdf, "http://publications.europa.eu/resource/authority/language/NOR");
            catalog.AppendChild(catalogLanguage);

            return catalog;
        }

        private XmlElement Setup()
        {
            doc = new XmlDocument();

            nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("rdf", xmlnsRdf);
            nsmgr.AddNamespace("skos", xmlnsSkos);
            nsmgr.AddNamespace("dct", xmlnsDct);

            XmlDeclaration dec = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            doc.AppendChild(dec);
            XmlElement root = doc.CreateElement("rdf", "RDF", xmlnsRdf);
            root.SetAttribute("xmlns:foaf", xmlnsFoaf);
            root.SetAttribute("xmlns:gco", xmlnsGco);
            root.SetAttribute("xmlns:void", xmlnsVoid);
            root.SetAttribute("xmlns:skos", xmlnsSkos);
            root.SetAttribute("xmlns:dc", xmlnsDc);
            root.SetAttribute("xmlns:dct", xmlnsDct);
            root.SetAttribute("xmlns:dctype", xmlnsDctype);
            root.SetAttribute("xmlns:dcat", xmlnsDcat);
            root.SetAttribute("xmlns:vcard", xmlnsVcard);
            root.SetAttribute("xmlns:adms", xmlnsAdms);
            root.SetAttribute("xmlns:xslUtils", xmlnsXslUtils);
            root.SetAttribute("xmlns:gmd", xmlnsGmd);
            root.SetAttribute("xmlns:rdfs", xmlnsRdfs);
            root.SetAttribute("xmlns:owl", xmlnsOwl);
            root.SetAttribute("xmlns:locn", xmlnsLocn);
            root.SetAttribute("xmlns:gml", xmlnsGml);
            root.SetAttribute("xmlns:dcatap", xmlnsDcatAp);

            doc.AppendChild(root);
            return root;
        }

        private XmlElement SetupService()
        {
            docService = new XmlDocument();

            nsmgr = new XmlNamespaceManager(docService.NameTable);
            nsmgr.AddNamespace("rdf", xmlnsRdf);
            nsmgr.AddNamespace("skos", xmlnsSkos);
            nsmgr.AddNamespace("dct", xmlnsDct);
            nsmgr.AddNamespace("dcat", xmlnsDcat);
            nsmgr.AddNamespace("foaf", xmlnsFoaf);   
            nsmgr.AddNamespace("vcard", xmlnsVcard); 
            nsmgr.AddNamespace("owl", xmlnsOwl);    

            XmlDeclaration dec = docService.CreateXmlDeclaration("1.0", "UTF-8", null);
            docService.AppendChild(dec);
            XmlElement root = docService.CreateElement("rdf", "RDF", xmlnsRdf);
            root.SetAttribute("xmlns:foaf", xmlnsFoaf);
            root.SetAttribute("xmlns:gco", xmlnsGco);
            root.SetAttribute("xmlns:void", xmlnsVoid);
            root.SetAttribute("xmlns:skos", xmlnsSkos);
            root.SetAttribute("xmlns:dc", xmlnsDc);
            root.SetAttribute("xmlns:dct", xmlnsDct);
            root.SetAttribute("xmlns:dctype", xmlnsDctype);
            root.SetAttribute("xmlns:dcat", xmlnsDcat);
            root.SetAttribute("xmlns:vcard", xmlnsVcard);
            root.SetAttribute("xmlns:adms", xmlnsAdms);
            root.SetAttribute("xmlns:xslUtils", xmlnsXslUtils);
            root.SetAttribute("xmlns:gmd", xmlnsGmd);
            root.SetAttribute("xmlns:rdfs", xmlnsRdfs);
            root.SetAttribute("xmlns:owl", xmlnsOwl);
            root.SetAttribute("xmlns:locn", xmlnsLocn);
            root.SetAttribute("xmlns:gml", xmlnsGml);
            root.SetAttribute("xmlns:dcatap", xmlnsDcatAp);

            docService.AppendChild(root);
            return root;
        }

        private void Finalize(XmlElement root, XmlElement catalog)
        {
            XmlElement catalogModified = doc.CreateElement("dct", "modified", xmlnsDct);
            catalogModified.SetAttribute("datatype", xmlnsRdf, "http://www.w3.org/2001/XMLSchema#date");
            if (catalogLastModified.HasValue)
                catalogModified.InnerText = catalogLastModified.Value.ToString("yyyy-MM-dd");
            catalog.AppendChild(catalogModified);
        }

        private void FinalizeService(XmlElement rootService, XmlElement catalogService)
        {
            XmlElement catalogModified = docService.CreateElement("dct", "modified", xmlnsDct);
            catalogModified.SetAttribute("datatype", xmlnsRdf, "http://www.w3.org/2001/XMLSchema#date");
            if (catalogLastModified.HasValue)
                catalogModified.InnerText = catalogLastModified.Value.ToString("yyyy-MM-dd");
            catalogService.AppendChild(catalogModified);
        }

        public List<RecordType> GetDatasets()
        {
            GeoNorge _geoNorge = new GeoNorge("", "", _settings["GeoNetworkUrl"]);
            _geoNorge.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
            _geoNorge.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);
            var filters = new object[]
                      {

                    new BinaryLogicOpType()
                        {
                            Items = new object[]
                                {
                                    new PropertyIsLikeType
                                    {
                                        escapeChar = "\\",
                                        singleChar = "_",
                                        wildCard = "%",
                                        PropertyName = new PropertyNameType {Text = new[] {"type"}},
                                        Literal = new LiteralType {Text = new[] { "dataset" }}
                                    },
                                    new PropertyIsLikeType
                                    {
                                        escapeChar = "\\",
                                        singleChar = "_",
                                        wildCard = "%",
                                        PropertyName = new PropertyNameType {Text = new[] {"type"}},
                                        Literal = new LiteralType {Text = new[] { "series" }}
                                    }
                                },

                                ItemsElementName = new ItemsChoiceType22[]
                                    {
                                        ItemsChoiceType22.PropertyIsLike, ItemsChoiceType22.PropertyIsLike
                                    }
                        },

                      };

            var filterNames = new ItemsChoiceType23[]
                {
                    ItemsChoiceType23.Or
                };

            //test use only 1 dataset todo remove
            //string searchString = "779a554b-fc3e-48a6-b202-561b07e9d4c2";
            //var filters = new object[]
            //          {

            //        new BinaryLogicOpType()
            //            {
            //                Items = new object[]
            //                    {
            //                        new PropertyIsLikeType
            //                        {
            //                            escapeChar = "\\",
            //                            singleChar = "_",
            //                            wildCard = "%",
            //                            PropertyName = new PropertyNameType {Text = new[] {"AnyText"}},
            //                            Literal = new LiteralType {Text = new[] { searchString }}
            //                        },
            //                        new PropertyIsLikeType
            //                        {
            //                            escapeChar = "\\",
            //                            singleChar = "_",
            //                            wildCard = "%",
            //                            PropertyName = new PropertyNameType {Text = new[] {"type"}},
            //                            Literal = new LiteralType {Text = new[] { "dataset" }}
            //                        }
            //                    },

            //                    ItemsElementName = new ItemsChoiceType22[]
            //                        {
            //                            ItemsChoiceType22.PropertyIsLike, ItemsChoiceType22.PropertyIsLike
            //                        }
            //            },

            //          };

            //var filterNames = new ItemsChoiceType23[]
            //    {
            //        ItemsChoiceType23.And
            //    };


            var stopwatch = new Stopwatch();
            var datasets = new List<RecordType>();
            stopwatch.Start();
            int startPosition = 1;
            int limit = 100;
            var result = _geoNorge.SearchWithFilters(filters, filterNames, startPosition, limit, false);
            var resultItems = result.Items.Cast<RecordType>().ToList();
            datasets.AddRange(resultItems);
            var nextRecord = int.Parse(result.nextRecord);
            startPosition = nextRecord;
            var numberOfRecordsMatched = int.Parse(result.numberOfRecordsMatched);
            while (nextRecord < numberOfRecordsMatched && nextRecord > 0)
            {
                result = _geoNorge.SearchWithFilters(filters, filterNames, startPosition, limit, false);
                resultItems = result.Items.Cast<RecordType>().ToList();
                datasets.AddRange(resultItems);

                nextRecord = int.Parse(result.nextRecord);
                startPosition = nextRecord;
                numberOfRecordsMatched = int.Parse(result.numberOfRecordsMatched);
            }

            stopwatch.Stop();
            _logger.LogDebug($"Looking up metadata from GeonorgeApi [timespent={stopwatch.ElapsedMilliseconds}ms]");
            return datasets;
        }


        public List<RecordType> GetServices()
        {
            GeoNorge _geoNorge = new GeoNorge("", "", _settings["GeoNetworkUrl"]);
            _geoNorge.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
            _geoNorge.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);
            var filters = new object[]
                      {

                    new BinaryLogicOpType()
                        {
                            Items = new object[]
                                {
                                    new PropertyIsLikeType
                                    {
                                        escapeChar = "\\",
                                        singleChar = "_",
                                        wildCard = "%",
                                        PropertyName = new PropertyNameType {Text = new[] {"type"}},
                                        Literal = new LiteralType {Text = new[] { "service" }}
                                    },
                                    new PropertyIsLikeType
                                    {
                                        escapeChar = "\\",
                                        singleChar = "_",
                                        wildCard = "%",
                                        PropertyName = new PropertyNameType {Text = new[] {"AnyText"}},
                                        Literal = new LiteralType {Text = new[] { "*" }}
                                    }
                                },

                                ItemsElementName = new ItemsChoiceType22[]
                                    {
                                        ItemsChoiceType22.PropertyIsLike, ItemsChoiceType22.PropertyIsLike
                                    }
                        },

                      };

            var filterNames = new ItemsChoiceType23[]
                {
                    ItemsChoiceType23.And
                };



            var stopwatch = new Stopwatch();
            var services = new List<RecordType>();
            stopwatch.Start();
            int startPosition = 1;
            int limit = 100;
            var result = _geoNorge.SearchWithFilters(filters, filterNames, startPosition, limit, false);
            var resultItems = result.Items.Cast<RecordType>().ToList();
            services.AddRange(resultItems);
            var nextRecord = int.Parse(result.nextRecord);
            startPosition = nextRecord;
            var numberOfRecordsMatched = int.Parse(result.numberOfRecordsMatched);
            while (nextRecord < numberOfRecordsMatched && nextRecord > 0)
            {
                result = _geoNorge.SearchWithFilters(filters, filterNames, startPosition, limit, false);
                resultItems = result.Items.Cast<RecordType>().ToList();
                services.AddRange(resultItems);

                nextRecord = int.Parse(result.nextRecord);
                startPosition = nextRecord;
                numberOfRecordsMatched = int.Parse(result.numberOfRecordsMatched);
            }

            stopwatch.Stop();
            _logger.LogDebug($"Looking up metadata from GeonorgeApi [timespent={stopwatch.ElapsedMilliseconds}ms]");
            return services;
        }

        private void LogEventsDebug(string log)
        {

            System.Diagnostics.Debug.Write(log);
            _logger.LogDebug(log);
        }

        private void LogEventsError(string log, Exception ex)
        {
            _logger.LogError(log + ": " + ex);
        }

        string EncodeUrl(string url)
        {
            return url.Replace(" ", "%20").Replace(",", "%2C").Replace("[", "%5B").Replace("]", "%5D");
        }

        public Dictionary<string, string> GetOrganizationsLink()
        {
            Dictionary<string, string> organizations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var httpClient = _httpClientFactory.GetHttpClient();
            string url = _settings["RegistryUrl"] + "api/register/organisasjoner";

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            HttpResponseMessage response = httpClient.GetAsync(url).Result;
            stopwatch.Stop();
            _logger.LogDebug($"Looking up organizations [timespent={stopwatch.ElapsedMilliseconds}ms]");
            if (response.IsSuccessStatusCode)
            {
                var parsedItems = Newtonsoft.Json.Linq.JObject.Parse(response.Content.ReadAsStringAsync().Result);

                var parsedOrganizations = parsedItems["containeditems"];

                foreach (var org in parsedOrganizations)
                {
                    if (!organizations.ContainsKey(org["label"].ToString()))
                    {
                        organizations.Add(org["label"].ToString(), org["id"].ToString());
                    }
                }
            }
            else
            {
                _logger.LogError($"Unable to fetch organizations from [url={url}], [responseStatusCode={response.StatusCode}] [responseContent={response.Content.ReadAsStringAsync().Result}]");
            }

            return organizations;
        }

        public Dictionary<string, DistributionType> GetDistributionTypes()
        {
            Dictionary<string, DistributionType> DistributionTypes = new Dictionary<string, DistributionType>();

            var httpClient = _httpClientFactory.GetHttpClient();
            string url = _settings["RegistryUrl"] + "api/metadata-kodelister/distribusjonstyper";
            HttpResponseMessage response = httpClient.GetAsync(url).Result;

            if (response.IsSuccessStatusCode)
            {
                var parsedResponse = Newtonsoft.Json.Linq.JObject.Parse(response.Content.ReadAsStringAsync().Result);

                var types = parsedResponse["containeditems"];

                foreach (var type in types)
                {
                    if (!DistributionTypes.ContainsKey(type["codevalue"].ToString()))
                    {
                        DistributionType distroType = new DistributionType();
                        distroType.Title = type["label"].ToString();
                        distroType.Description = type["description"].ToString();

                        DistributionTypes.Add(type["codevalue"].ToString(), distroType);
                    }
                }
            }
            else
            {
                _logger.LogError($"Unable to fetch distributiontypes from [url={url}], [responseStatusCode={response.StatusCode}] [responseContent={response.Content.ReadAsStringAsync().Result}]");
            }

            return DistributionTypes;
        }

        private string GetDistributionTitle(string protocol)
        {
            string title = protocol;
            if (DistributionTypes.ContainsKey(protocol))
                title = DistributionTypes[protocol].Title;

            return title;
        }

        private string GetDistributionDescription(string protocol)
        {
            string description = protocol;
            if (DistributionTypes.ContainsKey(protocol))
                description = DistributionTypes[protocol].Description;

            return description;
        }

        public class DistributionType
        {
            public string Title;
            public string Description;
        }

    }
}
