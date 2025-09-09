using Geonorge.ApiServices.Models;
using GeoNorgeAPI;
using Kartverket.Geonorge.Api.Services;
using Kartverket.Geonorge.Utilities.Organization;
using System.Diagnostics;
using System.Web;
using System.Xml;
using www.opengis.net;
using HttpClientFactory = Kartverket.Geonorge.Utilities.Organization.HttpClientFactory;
using IHttpClientFactory = Kartverket.Geonorge.Utilities.Organization.IHttpClientFactory;

namespace Geonorge.ApiServices.Services
{
    public interface IDcatService
    {
        XmlDocument GenerateDcat();
        List<RecordType> GetDatasets();
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
        XmlDocument conceptsDoc;
        XmlNamespaceManager nsmgr;

        List<RecordType> metadataSets;

        GeoNorge geoNorge;

        DateTime? catalogLastModified;

        private readonly OrganizationService _organizationService;

        Dictionary<string, string> OrganizationsLink;
        Dictionary<string, string> ConceptObjects = new Dictionary<string, string>();
        Dictionary<string, string> MediaTypes;
        Dictionary<string, string> FormatUrls;
        Dictionary<string, DistributionType> DistributionTypes;

        public XmlDocument GenerateDcat()
        {
            _logger.LogInformation("Generating DCAT");

            try
            {
                FormatUrls = GetFormatUrls();
                MediaTypes = GetMediaTypes();
                OrganizationsLink = GetOrganizationsLink();
                DistributionTypes = GetDistributionTypes();
                metadataSets = GetDatasets();

                XmlElement root = Setup();

                GetConcepts();

                XmlElement catalog = CreateCatalog(root);

                CreateDatasets(root, catalog);

                AddConcepts(root);

                Finalize(root, catalog);

                doc.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dcat", "geonorge_dcat.rdf"));

                _logger.LogInformation("Finished generating DCAT");
            }
            catch (Exception e)
            {
                _logger.LogError("Error generating DCAT", e);
            }

            return doc;
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

            };
        }

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

                    //dct:publisher => Referanse til en akt�r (organisasjon) som er ansvarlig for � gj�re datatjenesten tilgjengelig => ContactPublisher.Email => foaf:Agent
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

                    //Distribution
                    if (data.DistributionsFormats != null)
                    {
                        List<string> distributionFormats = new List<string>();

                        foreach (var distro in data.DistributionsFormats)
                        {

                            if (string.IsNullOrEmpty(distro.FormatName))
                                distro.FormatName = "ZIP";

                            if (!string.IsNullOrEmpty(distro.FormatName) && !distributionFormats.Contains(distro.FormatName))
                            {
                                //Map distribution to dataset
                                XmlElement distributionDataset = doc.CreateElement("dcat", "distribution", xmlnsDcat);
                                distributionDataset.SetAttribute("resource", xmlnsRdf, kartkatalogenUrl + "Metadata/uuid/" + data.Uuid + "/" + HttpUtility.UrlEncode(distro.FormatName));
                                dataset.AppendChild(distributionDataset);

                                XmlElement distribution = doc.CreateElement("dcat", "Distribution", xmlnsDcat);
                                distribution.SetAttribute("about", xmlnsRdf, kartkatalogenUrl + "Metadata/uuid/" + data.Uuid + "/" + HttpUtility.UrlEncode(distro.FormatName));
                                root.AppendChild(distribution);

                                XmlElement distributionTitle = doc.CreateElement("dct", "title", xmlnsDct);
                                distributionTitle.SetAttribute("xml:lang", "no");
                                if (data.DistributionDetails != null && !string.IsNullOrEmpty(data.DistributionDetails.Protocol))
                                    distributionTitle.InnerText = GetDistributionTitle(data.DistributionDetails.Protocol);

                                XmlElement distributionDescription = doc.CreateElement("dct", "description", xmlnsDct);
                                if (data.DistributionDetails != null && !string.IsNullOrEmpty(data.DistributionDetails.Protocol))
                                    distributionDescription.InnerText = GetDistributionDescription(data.DistributionDetails.Protocol);
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

                                distributionFormats.Add(distro.FormatName);
                            }
                            // Dataset distributions
                            AddDistributions(uuid, dataset, data, services);
                        }

                    }
                    //}

                }
                catch (Exception e)
                {
                    _logger.LogError("Error processing dataset", e);
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

        private void AddDistributions(string uuid, XmlElement dataset, SimpleMetadata data, Dictionary<string, XmlNode> services)
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

                        if (services.ContainsKey(serviceDistributionUrl))
                            continue;

                        string protocolName = protocol;
                        if (protocolName.Contains("-"))
                            protocolName = protocolName.Split('-')[0];

                        protocol = "OGC:" + protocolName;

                        if (protocol == "OGC:WMS" || protocol == "OGC:WFS" || protocol == "OGC:WCS")
                        {
                            var distribution = CreateXmlElementForDistribution(dataset, data, uuidService, protocol,
                                protocolName, serviceDistributionUrl);
                            if (!services.ContainsKey(serviceDistributionUrl))
                                services.Add(serviceDistributionUrl, distribution);
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

                            string urlDownload = distro.URL.Value;

                            var distribution = CreateXmlElementForDistributionAtomFeed(dataset, data, serviceDistributionUrl, urlDownload);
                            if (!services.ContainsKey(serviceDistributionUrl))
                            {

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
                _logger.LogError($"Unable to fetch distributions from: [url={metadataUrl}], [message={e.Message}]", e);
            }
        }

        private XmlNode CreateXmlElementForDistributionAtomFeed(XmlElement dataset, SimpleMetadata data, dynamic serviceDistributionUrl, string urlDownload)
        {
            XmlElement distributionDataset = doc.CreateElement("dcat", "distribution", xmlnsDcat);
            distributionDataset.SetAttribute("resource", xmlnsRdf,
                kartkatalogenUrl + "Metadata/uuid/" + data.Uuid);
            dataset.AppendChild(distributionDataset);

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

        private XmlElement CreateXmlElementForDistribution(XmlElement dataset, SimpleMetadata data, dynamic uuidService,
            dynamic protocol, string protocolName, dynamic serviceDistributionUrl)
        {
            XmlElement distributionDataset = doc.CreateElement("dcat", "distribution", xmlnsDcat);
            distributionDataset.SetAttribute("resource", xmlnsRdf,
                kartkatalogenUrl + "Metadata/uuid/" + uuidService);
            dataset.AppendChild(distributionDataset);

            XmlElement distribution = doc.CreateElement("dcat", "Distribution", xmlnsDcat);
            distribution.SetAttribute("about", xmlnsRdf, kartkatalogenUrl + "Metadata/uuid/" + uuidService);

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

        private void Finalize(XmlElement root, XmlElement catalog)
        {
            XmlElement catalogModified = doc.CreateElement("dct", "modified", xmlnsDct);
            catalogModified.SetAttribute("datatype", xmlnsRdf, "http://www.w3.org/2001/XMLSchema#date");
            if (catalogLastModified.HasValue)
                catalogModified.InnerText = catalogLastModified.Value.ToString("yyyy-MM-dd");
            catalog.AppendChild(catalogModified);
        }

        public List<RecordType> GetDatasets()
        {
            GeoNorge _geoNorge = new GeoNorge("", "", _settings["GeoNetworkUrl"]);
            _geoNorge.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
            _geoNorge.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);
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
            //                            PropertyName = new PropertyNameType {Text = new[] {"type"}},
            //                            Literal = new LiteralType {Text = new[] { "dataset" }}
            //                        },
            //                        new PropertyIsLikeType
            //                        {
            //                            escapeChar = "\\",
            //                            singleChar = "_",
            //                            wildCard = "%",
            //                            PropertyName = new PropertyNameType {Text = new[] {"type"}},
            //                            Literal = new LiteralType {Text = new[] { "series" }}
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
            //        ItemsChoiceType23.Or
            //    };

            //test use only 1 dataset todo remove
            string searchString = "9e419f66-f5d4-43e0-b01b-59d6c36b607c";
            var filters = new object[]
            {
                        new PropertyIsLikeType
                            {
                                escapeChar = "\\",
                                singleChar = "_",
                                wildCard = "*",
                                PropertyName = new PropertyNameType {Text = new[] {"AnyText"}},
                                Literal = new LiteralType {Text = new[] {searchString}}
                            }
            };

            var filterNames = new ItemsChoiceType23[]
            {
                        ItemsChoiceType23.PropertyIsLike,
            };


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
            return datasets; //todo test datasets.Take(1).ToList()
        }

        private void LogEventsDebug(string log)
        {

            System.Diagnostics.Debug.Write(log);
            _logger.LogDebug(log);
        }

        private void LogEventsError(string log, Exception ex)
        {
            _logger.LogError(log, ex);
        }

        string EncodeUrl(string url)
        {
            return url.Replace(" ", "%20").Replace(",", "%2C").Replace("[", "%5B").Replace("]", "%5D");
        }

        public Dictionary<string, string> GetOrganizationsLink()
        {
            Dictionary<string, string> organizations = new Dictionary<string, string>();

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
