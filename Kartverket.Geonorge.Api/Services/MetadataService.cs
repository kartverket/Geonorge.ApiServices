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
using Kartverket.Geonorge.Utilities.LogEntry;
using Kartverket.Geonorge.Utilities;
using System.Globalization;
using System.Web.Http;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Kartverket.Geonorge.Api.Services
{
    public interface IMetadataService
    {
        Task DeleteMetadata(string uuid);
        object GetSchema();
        Task<string> InsertMetadata(MetadataModel model);
        Task UpdateMetadata(string uuid, MetadataModel model);
        Task UpdateMetadataFair(string uuid, string result);
    }

    public class MetadataService : IMetadataService
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly HttpClient HttpClient = new HttpClient();

        public Task<string> InsertMetadata(MetadataModel model)
        {
            SimpleMetadata metadata = null;
            try
            {
                System.Collections.Specialized.NameValueCollection settings = System.Web.Configuration.WebConfigurationManager.AppSettings;
                string server = settings["GeoNetworkUrl"];
                string usernameGeonetwork = settings["GeoNetworkUsername"];
                string password = settings["GeoNetworkPassword"];
                string geonorgeUsername = settings["GeonorgeUsername"];


                GeoNorge _geoNorge = new GeoNorge(usernameGeonetwork, password, server);
                _geoNorge.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
                _geoNorge.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);

                metadata = metadata = SimpleMetadata.CreateDataset();
                metadata.MetadataLanguage = "nor";    
                metadata.Title = model.Title;

                metadata.Abstract = model.Description;

                metadata.ContactMetadata = new SimpleContact
                {
                    Name = model.ContactName,
                    Email = model.ContactEmail,
                    Organization = model.ContactOrganization,
                    Role = "pointOfContact"
                };

                if (!string.IsNullOrEmpty(model.PublisherOrganization))
                {
                    metadata.ContactPublisher = new SimpleContact
                    {
                        Name = model.PublisherName,
                        Email = model.PublisherEmail,
                        Organization = model.PublisherOrganization,
                        Role = "publisher"
                    };
                }

                if (!string.IsNullOrEmpty(model.OwnerOrganization))
                {
                    metadata.ContactOwner = new SimpleContact
                    {
                        Name = model.OwnerName,
                        Email = model.OwnerEmail,
                        Organization = model.OwnerOrganization,
                        Role = "owner"
                    };
                }

                DateTime now = DateTime.Now;
                metadata.DateCreated = model.DateOfCreation != null ? model.DateOfCreation : now; ;
                metadata.DatePublished = now;
                metadata.DateUpdated = model.DateUpdated != null ? model.DateUpdated : now;

                metadata.Keywords = model.GetAllKeywords();

                if (!string.IsNullOrWhiteSpace(model.TopicCategory))
                    metadata.TopicCategory = model.TopicCategory;

                metadata.Constraints = new SimpleConstraints
                {
                    SecurityConstraints = !string.IsNullOrWhiteSpace(model.SecurityConstraints) ? model.SecurityConstraints : "",
                    AccessConstraints = !string.IsNullOrWhiteSpace(model.AccessConstraints) ? model.AccessConstraints : "",
                    AccessConstraintsLink = !string.IsNullOrWhiteSpace(model.AccessConstraintsLink) ? model.AccessConstraintsLink : "",
                    UseConstraintsLicenseLink = !string.IsNullOrWhiteSpace(model.OtherConstraintsLink) ? model.OtherConstraintsLink : null,
                    UseConstraintsLicenseLinkText = !string.IsNullOrWhiteSpace(model.OtherConstraintsLinkText) ? model.OtherConstraintsLinkText : null,
                };

                var refsys = model.GetReferenceSystems();
                if (refsys != null)
                    metadata.ReferenceSystems = refsys;

                metadata.SpatialRepresentation = model.SpatialRepresentation;

                DateTime? DateMetadataValidFrom = model.DateMetadataValidFrom;
                DateTime? DateMetadataValidTo = model.DateMetadataValidTo;

                metadata.ValidTimePeriod = new SimpleValidTimePeriod()
                {
                    ValidFrom = DateMetadataValidFrom != null ? String.Format("{0:yyyy-MM-dd}", DateMetadataValidFrom) : "",
                    ValidTo = DateMetadataValidTo != null ? String.Format("{0:yyyy-MM-dd}", DateMetadataValidTo) : ""
                };

                var distribution = model.GetDistributionsFormats();
  
                metadata.DistributionsFormats = distribution;

                if (metadata.DistributionsFormats != null && metadata.DistributionsFormats.Count > 0)
                {
                    metadata.DistributionDetails = new SimpleDistributionDetails
                    {
                        URL = metadata.DistributionsFormats[0].URL,
                        Protocol = metadata.DistributionsFormats[0].Protocol,
                        Name = metadata.DistributionsFormats[0].Name,
                        UnitsOfDistribution = metadata.DistributionsFormats[0].UnitsOfDistribution,
                        EnglishUnitsOfDistribution = metadata.DistributionsFormats[0].EnglishUnitsOfDistribution
                    };
                }

                if (!string.IsNullOrWhiteSpace(model.BoundingBoxEast))
                {
                    metadata.BoundingBox = new SimpleBoundingBox
                    {
                        EastBoundLongitude = model.BoundingBoxEast,
                        WestBoundLongitude = model.BoundingBoxWest,
                        NorthBoundLatitude = model.BoundingBoxNorth,
                        SouthBoundLatitude = model.BoundingBoxSouth
                    };
                }

                SetDefaultValuesOnMetadata(metadata);

                _geoNorge.MetadataInsert(metadata.GetMetadata(), CreateAdditionalHeadersWithUsername(geonorgeUsername));

            }
            catch (Exception ex)
            {
                Log.Error("Error inserting metadata uuid: " + metadata.Uuid + ", error: " + ex);
            }

            return Task.FromResult(metadata.Uuid);

        }
        private void SetDefaultValuesOnMetadata(SimpleMetadata metadata)
        {
            metadata.DateMetadataUpdated = DateTime.Now;
            metadata.MetadataStandard = "ISO19115";
            metadata.MetadataStandardVersion = "2003";
            if (string.IsNullOrEmpty(metadata.MetadataLanguage))
                metadata.MetadataLanguage = "nor";
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

        public Task DeleteMetadata(string uuid)
        {
            System.Collections.Specialized.NameValueCollection settings = System.Web.Configuration.WebConfigurationManager.AppSettings;
            string server = settings["GeoNetworkUrl"];
            string usernameGeonetwork = settings["GeoNetworkUsername"];
            string password = settings["GeoNetworkPassword"];
            string geonorgeUsername = settings["GeonorgeUsername"];


            GeoNorge _geoNorge = new GeoNorge(usernameGeonetwork, password, server);
            _geoNorge.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
            _geoNorge.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);

            var respons = _geoNorge.MetadataDelete(uuid, CreateAdditionalHeadersWithUsername(geonorgeUsername));

            return Task.CompletedTask;
        }

        public Task UpdateMetadata(string uuid, MetadataModel model)
        {
            System.Collections.Specialized.NameValueCollection settings = System.Web.Configuration.WebConfigurationManager.AppSettings;
            string server = settings["GeoNetworkUrl"];
            string usernameGeonetwork = settings["GeoNetworkUsername"];
            string password = settings["GeoNetworkPassword"];
            string geonorgeUsername = settings["GeonorgeUsername"];


            GeoNorge _geoNorge = new GeoNorge(usernameGeonetwork, password, server);
            _geoNorge.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
            _geoNorge.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);

            SimpleMetadata metadata = new SimpleMetadata(_geoNorge.GetRecordByUuid(uuid));

            UpdateMetadataFromModel(model, metadata);
            metadata.RemoveUnnecessaryElements();
            var transaction = _geoNorge.MetadataUpdate(metadata.GetMetadata(), CreateAdditionalHeadersWithUsername(geonorgeUsername));
            if (transaction.TotalUpdated == "0")
                throw new Exception("Kunne ikke lagre endringene - kontakt systemansvarlig");

            return Task.CompletedTask;
        }

        public Task UpdateMetadataFair(string uuid, string result)
        {
            System.Collections.Specialized.NameValueCollection settings = System.Web.Configuration.WebConfigurationManager.AppSettings;
            string server = settings["GeoNetworkUrl"];
            string usernameGeonetwork = settings["GeoNetworkUsername"];
            string password = settings["GeoNetworkPassword"];
            string geonorgeUsername = settings["GeonorgeUsername"];


            GeoNorge _geoNorge = new GeoNorge(usernameGeonetwork, password, server);
            _geoNorge.OnLogEventDebug += new GeoNorgeAPI.LogEventHandlerDebug(LogEventsDebug);
            _geoNorge.OnLogEventError += new GeoNorgeAPI.LogEventHandlerError(LogEventsError);

            SimpleMetadata metadata = new SimpleMetadata(_geoNorge.GetRecordByUuid(uuid));

            var processHistory = metadata.ProcessHistory;
            var processHistoryEnglish = metadata.EnglishProcessHistory;

            bool fairFound = false;

            List<SimpleQualitySpecification> updatedList = new List<SimpleQualitySpecification>();

            if (metadata.QualitySpecifications != null) 
            { 
                for (int f =0; f < metadata.QualitySpecifications.Count; f++)
                {
                    updatedList.Add(metadata.QualitySpecifications[f]);

                    if (metadata.QualitySpecifications[f].Title.Contains("FAIR"))
                    {
                        updatedList[f].QuantitativeResult = result;
                        fairFound = true;
                    }
                }
            }

            if (!fairFound)
            {
                SimpleQualitySpecification fair = new SimpleQualitySpecification();
                fair.Title = "Prosentvis oppfyllelse av FAIR-prinsipper";
                fair.TitleLinkDescription = "Angir fullstendighet i forhold til krav fra FAIR-prinsippene (The FAIR Guiding Principles for scientific data management and stewardship)";
                fair.QuantitativeResult = result;
                updatedList.Add(fair);
            }

            metadata.QualitySpecifications = updatedList;

            if(!string.IsNullOrEmpty(processHistory))
                metadata.ProcessHistory = processHistory;

            if (!string.IsNullOrEmpty(processHistoryEnglish))
                metadata.EnglishProcessHistory = processHistoryEnglish;

            metadata.RemoveUnnecessaryElements();
            var transaction = _geoNorge.MetadataUpdate(metadata.GetMetadata(), CreateAdditionalHeadersWithUsername(geonorgeUsername));
            if (transaction.TotalUpdated == "0")
                throw new Exception("Kunne ikke lagre endringene - kontakt systemansvarlig");

            return Task.CompletedTask;
        }

        private void UpdateMetadataFromModel(MetadataModel model, SimpleMetadata metadata)
        {
            metadata.Title = model.Title;

            metadata.Abstract = model.Description;

            metadata.ContactMetadata = new SimpleContact
            {
                Name = model.ContactName,
                Email = model.ContactEmail,
                Organization = model.ContactOrganization,
                Role = "pointOfContact"
            };


            if (!string.IsNullOrEmpty(model.PublisherOrganization))
            {
                metadata.ContactPublisher = new SimpleContact
                {
                    Name = model.PublisherName,
                    Email = model.PublisherEmail,
                    Organization = model.PublisherOrganization,
                    Role = "publisher"
                };
            }

            if (!string.IsNullOrEmpty(model.OwnerOrganization)) 
            { 
                metadata.ContactOwner = new SimpleContact
                {
                    Name = model.OwnerName,
                    Email = model.OwnerEmail,
                    Organization = model.OwnerOrganization,
                    Role = "owner"
                };
            }

            DateTime now = DateTime.Now;
            metadata.DateCreated = model.DateOfCreation != null ? model.DateOfCreation : now; ;
            metadata.DatePublished = now;
            metadata.DateUpdated = model.DateUpdated != null ? model.DateUpdated : now;

            metadata.Keywords = model.GetAllKeywords();

            if (!string.IsNullOrWhiteSpace(model.TopicCategory))
                metadata.TopicCategory = model.TopicCategory;

            metadata.Constraints = new SimpleConstraints
            {
                SecurityConstraints = !string.IsNullOrWhiteSpace(model.SecurityConstraints) ? model.SecurityConstraints : "",
                AccessConstraints = !string.IsNullOrWhiteSpace(model.AccessConstraints) ? model.AccessConstraints : "",
                AccessConstraintsLink = !string.IsNullOrWhiteSpace(model.AccessConstraintsLink) ? model.AccessConstraintsLink : "",
                UseConstraintsLicenseLink = !string.IsNullOrWhiteSpace(model.OtherConstraintsLink) ? model.OtherConstraintsLink : null,
                UseConstraintsLicenseLinkText = !string.IsNullOrWhiteSpace(model.OtherConstraintsLinkText) ? model.OtherConstraintsLinkText : null,
            };

            var refsys = model.GetReferenceSystems();
            if (refsys != null)
                metadata.ReferenceSystems = refsys;

            metadata.SpatialRepresentation = model.SpatialRepresentation;

            DateTime? DateMetadataValidFrom = model.DateMetadataValidFrom;
            DateTime? DateMetadataValidTo = model.DateMetadataValidTo;

            metadata.ValidTimePeriod = new SimpleValidTimePeriod()
            {
                ValidFrom = DateMetadataValidFrom != null ? String.Format("{0:yyyy-MM-dd}", DateMetadataValidFrom) : "",
                ValidTo = DateMetadataValidTo != null ? String.Format("{0:yyyy-MM-dd}", DateMetadataValidTo) : ""
            };

            var distribution = model.GetDistributionsFormats();
  
            metadata.DistributionsFormats = distribution;

            if (metadata.DistributionsFormats != null && metadata.DistributionsFormats.Count > 0)
            {
                metadata.DistributionDetails = new SimpleDistributionDetails
                {
                    URL = metadata.DistributionsFormats[0].URL,
                    Protocol = metadata.DistributionsFormats[0].Protocol,
                    Name = metadata.DistributionsFormats[0].Name,
                    UnitsOfDistribution = metadata.DistributionsFormats[0].UnitsOfDistribution,
                    EnglishUnitsOfDistribution = metadata.DistributionsFormats[0].EnglishUnitsOfDistribution
                };
            }

            if (!string.IsNullOrWhiteSpace(model.BoundingBoxEast))
            {
                metadata.BoundingBox = new SimpleBoundingBox
                {
                    EastBoundLongitude = model.BoundingBoxEast,
                    WestBoundLongitude = model.BoundingBoxWest,
                    NorthBoundLatitude = model.BoundingBoxNorth,
                    SouthBoundLatitude = model.BoundingBoxSouth
                };
            }


            SetDefaultValuesOnMetadata(metadata);

            
        }

        public object GetSchema()
        {
            var json = "";
            using (StreamReader r = new StreamReader(System.Web.Hosting.HostingEnvironment.MapPath("~/schema.json")))
            {
                json = r.ReadToEnd();
            }

            var output = JObject.Parse(json);

            return output;
        }
    }


}