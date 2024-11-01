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

namespace Kartverket.Geonorge.Api.Services
{
    public interface IMetadataService
    {
        Task DeleteMetadata(string uuid);
        Task<string> InsertMetadata(MetadataCreate metadataCreate);
    }

    public class MetadataService : IMetadataService
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly HttpClient HttpClient = new HttpClient();

        public Task<string> InsertMetadata(MetadataCreate model)
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

                metadata.Abstract = "...";
                metadata.ContactMetadata = new SimpleContact
                {
                    Name = model.MetadataContactName,
                    Email = model.MetadataContactEmail,
                    Organization = model.MetadataContactOrganization,
                    Role = "pointOfContact"
                };

                metadata.ContactPublisher = new SimpleContact
                {
                    Name = model.MetadataContactName,
                    Email = model.MetadataContactEmail,
                    Organization = model.MetadataContactOrganization,
                    Role = "publisher"
                };
                metadata.ContactOwner = new SimpleContact
                {
                    Name = model.MetadataContactName,
                    Email = model.MetadataContactEmail,
                    Organization = model.MetadataContactOrganization,
                    Role = "owner"
                };

                DateTime now = DateTime.Now;
                metadata.DateCreated = now;
                metadata.DatePublished = now;
                metadata.DateUpdated = now;

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
    }

}