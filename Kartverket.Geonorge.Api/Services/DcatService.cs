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

namespace Kartverket.Geonorge.Api.Services
{
    public interface IDcatService
    {
        XmlDocument GenerateDcat();
        SearchResultsType GetDatasets();
        Dictionary<string, string> GetOrganizationsLink();
        Dictionary<string, DcatService.DistributionType> GetDistributionTypes();
    }

    public class DcatService : IDcatService
    {
        private readonly IHttpClientFactory _httpClientFactory;
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

        string geoNetworkendPoint = "srv/nor/csw-dataset?";

        string kartkatalogenUrl = WebConfigurationManager.AppSettings["KartkatalogenUrl"];

        XmlDocument doc;
        XmlDocument conceptsDoc;
        XmlNamespaceManager nsmgr;

        SearchResultsType metadataSets;

        GeoNorge geoNorge = new GeoNorge("", "", WebConfigurationManager.AppSettings["GeoNetworkUrl"]);

        DateTime? catalogLastModified;

        private readonly OrganizationService _organizationService = new OrganizationService(WebConfigurationManager.AppSettings["RegistryUrl"], new HttpClientFactory());

        Dictionary<string, string> OrganizationsLink;
        Dictionary<string, string> ConceptObjects = new Dictionary<string, string>();
        Dictionary<string, string> MediaTypes;
        Dictionary<string, DistributionType> DistributionTypes;

        public DcatService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public XmlDocument GenerateDcat()
        {
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

            doc.Save(System.Web.HttpContext.Current.Request.MapPath("~\\dcat\\geonorge_dcat.rdf"));

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
               { "FGDB", "https://publications.europa.eu/resource/authority/file-type/GDB" },
               { "PostGIS", "https://www.iana.org/assignments/media-types/application/sql" },
               { "LAS", "https://www.iana.org/assignments/media-types/application/vnd.las" },
               { "LAZ", "https://www.iana.org/assignments/media-types/application/vnd.laszip" },
               { "JPEG", "https://publications.europa.eu/resource/authority/file-type/JPEG" },
               { "KML", "https://www.iana.org/assignments/media-types/application/vnd.google-earth.kml+xml" },
               { "KMZ", "https://www.iana.org/assignments/media-types/application/vnd.google-earth.kmz+xml" },
               { "PPTX", "https://publications.europa.eu/resource/authority/file-type/PPTX" }

            };
        }

        private void GetConcepts()
        {
            conceptsDoc = new XmlDocument();
            conceptsDoc.Load(HttpContext.Current.Server.MapPath("~/App_Data/Concepts.xml"));
        }

        private string GetConcept(string prefLabel)
        {
            var concept = conceptsDoc.SelectSingleNode("//skos:Concept[skos:prefLabel='"+ prefLabel + "']", nsmgr);
            if(concept != null)
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

            for (int d = 0; d < metadataSets.Items.Length; d++)
            {
                string uuid = ((www.opengis.net.DCMIRecordType)(metadataSets.Items[d])).Items[0].Text[0];
                MD_Metadata_Type md = geoNorge.GetRecordByUuid(uuid);
                var data = new SimpleMetadata(md);

                if (data.DistributionFormats != null && data.DistributionFormats.Count > 0 
                    && !string.IsNullOrEmpty(data.DistributionFormats[0].Name) &&
                    data.DistributionDetails != null && !string.IsNullOrEmpty(data.DistributionDetails.Protocol) )
                {
                    Log.Info($"Processing dataset: [title={data.Title}], [uuid={uuid}]");

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

                    foreach (var keyword in data.Keywords)
                    {

                        XmlElement datasetKeyword = doc.CreateElement("dcat", "keyword", xmlnsDcat);
                        datasetKeyword.SetAttribute("xml:lang", "no");
                        datasetKeyword.InnerText = keyword.Keyword;
                        dataset.AppendChild(datasetKeyword);

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
                            datasetLocation.InnerText = aboutPlace;
                            dataset.AppendChild(datasetLocation);
                        }
                    }

                    //Resource metadata in GeoDCAT - AP using a geographic bounding box
                    if (data.BoundingBox != null)
                    {
                        XmlElement datasetSpatial = doc.CreateElement("dct", "spatial", xmlnsDct);
                        datasetSpatial.SetAttribute("rdf:parseType", "Resource");

                        XmlElement spatialLocn = doc.CreateElement("locn", "geometry", xmlnsLocn);
                        spatialLocn.SetAttribute("rdf:datatype", "http://www.opengis.net/ont/geosparql#gmlLiteral");

                        var cdata = doc.CreateCDataSection("<gml:Envelope srsName=\"http://www.opengis.net/def/crs/OGC/1.3/CRS84\"><gml:lowerCorner>" + data.BoundingBox.WestBoundLongitude + " " + data.BoundingBox.SouthBoundLatitude + "</gml:lowerCorner><gml:upperCorner>" + data.BoundingBox.EastBoundLongitude + " " + data.BoundingBox.NorthBoundLatitude + "</gml:upperCorner></gml:Envelope>");
                        spatialLocn.AppendChild(cdata);

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

                        if(!string.IsNullOrEmpty(aboutConcept))
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
                            if(!themes.Contains(euLink + Mappings.ThemeInspireToEU[themeInspire.Keyword]))
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
                        if(theme.Contains("objektkatalog.geonorge.no"))
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
                    if (data.DateUpdated.HasValue)
                    {
                        datasetUpdated.InnerText = data.DateUpdated.Value.ToString("yyyy-MM-dd");
                        if (!catalogLastModified.HasValue || data.DateUpdated > catalogLastModified)
                            catalogLastModified = data.DateUpdated;
                    }
                    dataset.AppendChild(datasetUpdated);


                    XmlElement datasetPublisher = doc.CreateElement("dct", "publisher", xmlnsDct);
                    if (data.ContactOwner != null && !string.IsNullOrEmpty(data.ContactOwner.Organization) && OrganizationsLink.ContainsKey(data.ContactOwner.Organization) && OrganizationsLink[data.ContactOwner.Organization] != null)
                        datasetPublisher.SetAttribute("resource", xmlnsRdf, OrganizationsLink[data.ContactOwner.Organization]);

                    dataset.AppendChild(datasetPublisher);

                    Organization organization = null;

                    if (data.ContactOwner != null)
                    {
                        Log.Info("Looking up organization: " + data.ContactOwner.Organization);
                        Task<Organization> getOrganizationTask = _organizationService.GetOrganizationByName(data.ContactOwner.Organization);
                        organization = getOrganizationTask.Result;
                    }

                    XmlElement datasetContactPoint = doc.CreateElement("dcat", "contactPoint", xmlnsDcat);
                    if (data.ContactOwner != null && !string.IsNullOrEmpty(data.ContactOwner.Organization) && OrganizationsLink.ContainsKey(data.ContactOwner.Organization) && OrganizationsLink[data.ContactOwner.Organization] != null)
                        datasetContactPoint.SetAttribute("resource", xmlnsRdf, OrganizationsLink[data.ContactOwner.Organization].Replace("organisasjoner/kartverket/", "organisasjoner/"));
                    dataset.AppendChild(datasetContactPoint);

                    XmlElement datasetKind = doc.CreateElement("vcard", "Organization", xmlnsVcard);
                    if (data.ContactOwner != null && !string.IsNullOrEmpty(data.ContactOwner.Organization) && OrganizationsLink.ContainsKey(data.ContactOwner.Organization) && OrganizationsLink[data.ContactOwner.Organization] != null)
                        datasetKind.SetAttribute("about", xmlnsRdf, OrganizationsLink[data.ContactOwner.Organization].Replace("organisasjoner/kartverket/", "organisasjoner/"));

                    XmlElement datasetOrganizationName = doc.CreateElement("vcard", "organization-unit", xmlnsVcard);
                    datasetOrganizationName.SetAttribute("xml:lang", "");
                    if (organization != null)
                        datasetOrganizationName.InnerText = organization.Name;
                    datasetKind.AppendChild(datasetOrganizationName);

                    if (data.ContactOwner != null && !string.IsNullOrEmpty(data.ContactOwner.Email))
                    {
                        XmlElement datasetHasEmail = doc.CreateElement("vcard", "hasEmail", xmlnsVcard);
                        datasetHasEmail.SetAttribute("resource", xmlnsRdf, "mailto:" + data.ContactOwner.Email);
                        datasetKind.AppendChild(datasetHasEmail);
                    }
                    if (data.ContactOwner != null && !string.IsNullOrEmpty(data.ContactOwner.Organization) && OrganizationsLink.ContainsKey(data.ContactOwner.Organization) && OrganizationsLink[data.ContactOwner.Organization] != null)
                        if (!vcardKinds.ContainsKey(OrganizationsLink[data.ContactOwner.Organization].Replace("organisasjoner/kartverket/", "organisasjoner/")))
                            vcardKinds.Add(OrganizationsLink[data.ContactOwner.Organization].Replace("organisasjoner/kartverket/", "organisasjoner/"), datasetKind);


                    XmlElement datasetAccrualPeriodicity = doc.CreateElement("dct", "accrualPeriodicity", xmlnsDct);
                    if (!string.IsNullOrEmpty(data.MaintenanceFrequency))
                        datasetAccrualPeriodicity.InnerText = data.MaintenanceFrequency;
                    dataset.AppendChild(datasetAccrualPeriodicity);

                    XmlElement datasetGranularity = doc.CreateElement("dcat", "granularity", xmlnsDcat);
                    if (!string.IsNullOrEmpty(data.ResolutionScale))
                        datasetGranularity.InnerText = data.ResolutionScale;
                    dataset.AppendChild(datasetGranularity);

                    XmlElement datasetLicense = doc.CreateElement("dct", "license", xmlnsDct);
                    if (data.Constraints != null && !string.IsNullOrEmpty(data.Constraints.OtherConstraintsLink))
                        datasetLicense.SetAttribute("resource", xmlnsRdf, data.Constraints.OtherConstraintsLink);
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
                    if (data.DistributionFormats != null)
                    {
                        foreach (var distro in data.DistributionFormats)
                        {
                            if (!string.IsNullOrEmpty(distro.Name))
                            {
                                //Map distribution to dataset
                                XmlElement distributionDataset = doc.CreateElement("dcat", "distribution", xmlnsDcat);
                                distributionDataset.SetAttribute("resource", xmlnsRdf, kartkatalogenUrl + "Metadata/uuid/" + data.Uuid + "/" + HttpUtility.UrlEncode(distro.Name));
                                dataset.AppendChild(distributionDataset);

                                XmlElement distribution = doc.CreateElement("dcat", "Distribution", xmlnsDcat);
                                distribution.SetAttribute("about", xmlnsRdf, kartkatalogenUrl + "Metadata/uuid/" + data.Uuid + "/" + HttpUtility.UrlEncode(distro.Name));
                                root.AppendChild(distribution);

                                XmlElement distributionTitle = doc.CreateElement("dct", "title", xmlnsDct);
                                distributionTitle.SetAttribute("xml:lang", "no");
                                if (data.DistributionDetails != null && !string.IsNullOrEmpty(data.DistributionDetails.Protocol))
                                    distributionTitle.InnerText = GetDistributionTitle(data.DistributionDetails.Protocol);
                                distribution.AppendChild(distributionTitle);

                                XmlElement distributionDescription = doc.CreateElement("dct", "description", xmlnsDct);
                                if (data.DistributionDetails != null && !string.IsNullOrEmpty(data.DistributionDetails.Protocol))
                                    distributionDescription.InnerText = GetDistributionDescription( data.DistributionDetails.Protocol);
                                distribution.AppendChild(distributionDescription);

                                XmlElement distributionFormat = doc.CreateElement("dct", "format", xmlnsDct);
                                if (MediaTypes.ContainsKey(distro.Name))
                                {
                                    distributionFormat.SetAttribute("resource", xmlnsRdf, MediaTypes[distro.Name]);
                                }
                                else { 
                                distributionFormat.InnerText = distro.Name;
                                }
                                distribution.AppendChild(distributionFormat);

                                XmlElement distributionAccessURL = doc.CreateElement("dcat", "accessURL", xmlnsDcat);
                                distributionAccessURL.SetAttribute("resource", xmlnsRdf, kartkatalogenUrl + "metadata/uuid/" + uuid);
                                distribution.AppendChild(distributionAccessURL);

                                XmlElement distributionLicense = doc.CreateElement("dct", "license", xmlnsDct);
                                if (data.Constraints != null && !string.IsNullOrEmpty(data.Constraints.OtherConstraintsLink))
                                    distributionLicense.SetAttribute("resource", xmlnsRdf, data.Constraints.OtherConstraintsLink);
                                distribution.AppendChild(distributionLicense);

                                XmlElement distributionStatus = doc.CreateElement("adms", "status", xmlnsAdms);
                                if (!string.IsNullOrEmpty(data.Status))
                                    distributionStatus.SetAttribute("resource", xmlnsRdf, "http://purl.org/adms/status/" + data.Status);
                                distribution.AppendChild(distributionStatus);
                            }

                        }

                    }

                    // Dataset distributions
                    AddDistributions(uuid, dataset, data, services);


                    //Agent/publisher

                    XmlElement agent = doc.CreateElement("foaf", "Agent", xmlnsFoaf);
                    if (data.ContactOwner != null && !string.IsNullOrEmpty(data.ContactOwner.Organization) && OrganizationsLink.ContainsKey(data.ContactOwner.Organization) && OrganizationsLink[data.ContactOwner.Organization] != null)
                        agent.SetAttribute("about", xmlnsRdf, OrganizationsLink[data.ContactOwner.Organization]);

                    XmlElement agentType = doc.CreateElement("dct", "type", xmlnsDct);
                    agentType.SetAttribute("resource", xmlnsRdf, "http://purl.org/adms/publishertype/NationalAuthority");
                    agent.AppendChild(agentType);


                    if (organization != null && !string.IsNullOrEmpty(organization.Number))
                    {
                        XmlElement agentIdentifier = doc.CreateElement("dct", "identifier", xmlnsDct);
                        agentIdentifier.InnerText = organization.Number;
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

                    if (data.ContactOwner != null && !string.IsNullOrEmpty(data.ContactOwner.Organization) && OrganizationsLink.ContainsKey(data.ContactOwner.Organization) && OrganizationsLink[data.ContactOwner.Organization] != null)
                    {
                        if (!foafAgents.ContainsKey(OrganizationsLink[data.ContactOwner.Organization]))
                            foafAgents.Add(OrganizationsLink[data.ContactOwner.Organization], agent);
                    }

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

        private void AddDistributions(string uuid, XmlElement dataset, SimpleMetadata data, Dictionary<string, XmlNode> services)
        {
// Get distribution from index in kartkatalog 
            string metadataUrl = WebConfigurationManager.AppSettings["KartkatalogenUrl"] + "api/getdata/" + uuid;

            try
            {
                Log.Info("Looking up distributions");
                
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var httpClient = _httpClientFactory.GetHttpClient();
                var json = httpClient.GetStringAsync(metadataUrl).Result;
                stopwatch.Stop();
                Log.Debug($"Distribution lookup for [uuid={uuid}] [timespent={stopwatch.ElapsedMilliseconds}ms]");

                dynamic metadata = Newtonsoft.Json.Linq.JObject.Parse(json);

                if (metadata != null && metadata.Related != null)
                {
                    foreach (var related in metadata.Related)
                    {
                        var uuidService = related.Uuid.Value;

                        if (related.DistributionDetails != null)
                        {
                            var protocol = related.DistributionDetails.Protocol.Value;
                            var serviceDistributionUrl = related.DistributionDetails.URL.Value;

                            if (services.ContainsKey(serviceDistributionUrl))
                                continue;

                            string protocolName = protocol;
                            if (protocolName.Contains(":"))
                                protocolName = protocolName.Split(':')[1];

                            if (protocol == "OGC:WMS" || protocol == "OGC:WFS" || protocol == "OGC:WCS")
                            {
                                var distribution = CreateXmlElementForDistribution(dataset, data, uuidService, protocol,
                                    protocolName, serviceDistributionUrl);

                                services.Add(serviceDistributionUrl, distribution);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"Unable to fetch distributions from: [url={metadataUrl}], [message={e.Message}]", e);
            }
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
            distributionFormat.InnerText = protocolName;
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
                Log.Info($"Looking up concept from [url={url}]");
                try
                {
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Accept", "application/rdf+xml");
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    HttpResponseMessage response = httpClient.SendAsync(request).Result;
                    stopwatch.Stop();

                    Log.Debug($"Concept lookup for [url={url}] [timespent={stopwatch.ElapsedMilliseconds}ms]");

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
                        Log.Error(
                            $"Unable to fetch concept from [url={url}], [responseStatusCode={response.StatusCode}], [responseContent={response.Content.ReadAsStringAsync().Result}");
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Unable to fetch concept from [url={url}], [message={e.Message}]" ,e);
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

            XmlElement catalogHomePage = doc.CreateElement("foaf", "homepage", xmlnsFoaf);
            catalogHomePage.InnerText = "http://www.geonorge.no/geonetwork";
            catalog.AppendChild(catalogHomePage);

            XmlElement catalogOpenSearchDescription = doc.CreateElement("void", "openSearchDescription", xmlnsVoid);
            catalogOpenSearchDescription.InnerText = "http://www.geonorge.no/geonetwork/srv/nor/portal.opensearch";
            catalog.AppendChild(catalogOpenSearchDescription);

            XmlElement catalogUriLookupEndpoint = doc.CreateElement("void", "uriLookupEndpoint", xmlnsVoid);
            catalogUriLookupEndpoint.InnerText = "http://www.geonorge.no/geonetwork/srv/nor/rdf.search?any=";
            catalog.AppendChild(catalogUriLookupEndpoint);

            XmlElement catalogPublisher = doc.CreateElement("dct", "publisher", xmlnsDct);

            if(WebConfigurationManager.AppSettings["EnvironmentName"] == "dev" )
                catalogPublisher.SetAttribute("resource", xmlnsRdf, "http://register.dev.geonorge.no/organisasjoner/kartverket/10087020-f17c-45e1-8542-02acbcf3d8a3");
            else
                catalogPublisher.SetAttribute("resource", xmlnsRdf, "https://register.geonorge.no/organisasjoner/geonorge/f5fb2fdf-76b6-4e15-9fd1-603849e41e09");

            catalog.AppendChild(catalogPublisher);

            XmlElement catalogLicense = doc.CreateElement("dct", "license", xmlnsDct);
            catalogLicense.SetAttribute("resource", xmlnsRdf, "http://creativecommons.org/licenses/by/4.0/");
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

            XmlDeclaration dec = doc.CreateXmlDeclaration("1.0", null, null);
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


        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public SearchResultsType GetDatasets()
        {
            GeoNorge _geoNorge = new GeoNorge("", "", WebConfigurationManager.AppSettings["GeoNetworkUrl"] + geoNetworkendPoint);
            _geoNorge.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
            _geoNorge.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);
            var filters = new object[]
            {
                    new PropertyIsLikeType
                        {
                            escapeChar = "\\",
                            singleChar = "_",
                            wildCard = "%",
                            PropertyName = new PropertyNameType {Text = new[] {"keyword"}},
                            Literal = new LiteralType {Text = new[] {"fellesDatakatalog"}}
                        }
            };

            var filterNames = new ItemsChoiceType23[]
            {
                        ItemsChoiceType23.PropertyIsLike,
            };

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var result = _geoNorge.SearchWithFilters(filters, filterNames, 1, 1000, false);
            stopwatch.Stop();
            Log.Debug($"Looking up metadata from GeonorgeApi [timespent={stopwatch.ElapsedMilliseconds}ms]");
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

        string EncodeUrl(string url)
        {
            return url.Replace(" ", "%20").Replace(",", "%2C").Replace("[", "%5B").Replace("]", "%5D");
        }

        public Dictionary<string, string>  GetOrganizationsLink()
        {
            Dictionary<string, string> organizations = new Dictionary<string, string>();

            var httpClient = _httpClientFactory.GetHttpClient();
            string url = WebConfigurationManager.AppSettings["RegistryUrl"] + "api/register/organisasjoner";

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            HttpResponseMessage response = httpClient.GetAsync(url).Result;
            stopwatch.Stop();
            Log.Debug($"Looking up organizations [timespent={stopwatch.ElapsedMilliseconds}ms]");
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
                Log.Error($"Unable to fetch organizations from [url={url}], [responseStatusCode={response.StatusCode}] [responseContent={response.Content.ReadAsStringAsync().Result}]");
            }

            return organizations;
        }

        public Dictionary<string, DistributionType> GetDistributionTypes()
        {
            Dictionary<string, DistributionType> DistributionTypes = new Dictionary<string, DistributionType>();

            var httpClient = _httpClientFactory.GetHttpClient();
            string url = WebConfigurationManager.AppSettings["RegistryUrl"] + "api/metadata-kodelister/distribusjonstyper";
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
                Log.Error($"Unable to fetch distributiontypes from [url={url}], [responseStatusCode={response.StatusCode}] [responseContent={response.Content.ReadAsStringAsync().Result}]");
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