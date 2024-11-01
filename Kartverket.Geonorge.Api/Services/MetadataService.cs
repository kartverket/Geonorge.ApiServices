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
        Task<string> InsertMetadata(MetadataCreate metadataCreate);
        Task UpdateMetadata(string uuid, MetadataModel model);
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

                metadata = metadata = SimpleMetadata.CreateDataset(model.Uuid);
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

        private void UpdateMetadataFromModel(MetadataModel model, SimpleMetadata metadata)
        {
            metadata.MetadataLanguage = model.MetadataLanguage;
            metadata.Title = model.Title;
            metadata.Abstract = model.Description;
            var dateType = "publication";

            metadata.ParentIdentifier = model.ParentIdentifier;

            metadata.Purpose = !string.IsNullOrWhiteSpace(model.Purpose) ? model.Purpose : " ";

            if (!string.IsNullOrWhiteSpace(model.TopicCategory))
                metadata.TopicCategory = model.TopicCategory;

            if (!model.IsService())
                metadata.SupplementalDescription = model.SupplementalDescription;

            metadata.SpecificUsage = !string.IsNullOrWhiteSpace(model.SpecificUsage) ? model.SpecificUsage : " ";

            //var contactMetadata = model.ContactMetadata.ToSimpleContact();
            //if (!string.IsNullOrWhiteSpace(model.EnglishContactMetadataOrganization))
            //{
            //    contactMetadata.OrganizationEnglish = model.EnglishContactMetadataOrganization;
            //}
            //else if (model.MetadataLanguage == "eng")
            //{
            //    contactMetadata.OrganizationEnglish = contactMetadata.Organization;
            //}
            //metadata.ContactMetadata = contactMetadata;

            //var contactPublisher = model.ContactPublisher.ToSimpleContact();
            //if (!string.IsNullOrWhiteSpace(model.EnglishContactPublisherOrganization))
            //{
            //    contactPublisher.OrganizationEnglish = model.EnglishContactPublisherOrganization;
            //}
            //else if (model.MetadataLanguage == "eng")
            //{
            //    contactPublisher.OrganizationEnglish = contactPublisher.Organization;
            //}
            //metadata.ContactPublisher = contactPublisher;

            //if (model.IsInspireSpatialServiceConformance())
            //{
            //    metadata.ContactCustodian = new SimpleContact
            //    {
            //        Name = contactPublisher.Name,
            //        Email = contactPublisher.Email,
            //        Organization = contactPublisher.Organization,
            //        Role = "custodian"
            //    };
            //}

            //var contactOwner = model.ContactOwner.ToSimpleContact();
            //if (!string.IsNullOrWhiteSpace(model.EnglishContactOwnerOrganization))
            //{
            //    contactOwner.OrganizationEnglish = model.EnglishContactOwnerOrganization;
            //}
            //else if (model.MetadataLanguage == "eng")
            //{
            //    contactOwner.OrganizationEnglish = contactOwner.Organization;
            //}

            //if (!string.IsNullOrWhiteSpace(model.ContactOwnerPositionName))
            //{
            //    contactOwner.PositionName = model.ContactOwnerPositionName;
            //}

            //metadata.ContactOwner = contactOwner;

            // documents
            metadata.ProductSpecificationUrl = model.ProductSpecificationUrl;

            metadata.ApplicationSchema = model.ApplicationSchema;

            if (metadata.IsDataset())
            {
                //metadata.ProductSpecificationOther = new SimpleOnlineResource
                //{
                //    Name = model.ProductSpecificationOther.Name,
                //    URL = model.ProductSpecificationOther.URL
                //};
            }


            metadata.ProductSheetUrl = model.ProductSheetUrl;
            metadata.ProductPageUrl = model.ProductPageUrl;
            metadata.LegendDescriptionUrl = model.LegendDescriptionUrl;
            if (model.IsDataset() || model.IsDatasetSeries())
            {
                metadata.CoverageUrl = model.CoverageUrl;
                metadata.CoverageGridUrl = model.CoverageGridUrl;
                metadata.CoverageCellUrl = model.CoverageCellUrl;
            }

            metadata.HelpUrl = model.HelpUrl;

            metadata.Thumbnails = Thumbnail.ToSimpleThumbnailList(model.Thumbnails);

            // distribution
            metadata.SpatialRepresentation = model.SpatialRepresentation;

            if (model.IsDataset() || model.IsDatasetSeries())
                metadata.Language = model.Language;

            //var refsys = model.GetReferenceSystems();
            //if (refsys != null)
            //    metadata.ReferenceSystems = refsys;

            var distribution = model.GetDistributionsFormats();
            //distribution = SetEnglishTranslationForUnitsOfDistributions(distribution);
            //var distributionProtocolService = "";


            if (model.IsDataset() || model.IsDatasetSeries())
            {
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
            }
            else
            {
                List<SimpleDistributionFormat> formats = new List<SimpleDistributionFormat>();
                foreach (var format in distribution)
                {
                    formats.Add(new SimpleDistributionFormat { Name = format.FormatName, Version = format.FormatVersion });
                }
                metadata.DistributionFormats = formats;

                if (distribution != null && distribution.Count > 0)
                {
                    metadata.DistributionDetails = new SimpleDistributionDetails
                    {
                        URL = distribution[0].URL,
                        Protocol = distribution[0].Protocol,
                        Name = distribution[0].Name,
                        UnitsOfDistribution = distribution[0].UnitsOfDistribution,
                        EnglishUnitsOfDistribution = metadata.DistributionsFormats[0].EnglishUnitsOfDistribution
                    };
                    //distributionProtocolService = distribution[0].Protocol;
                }
            }

            if (model.IsService())
            {
                metadata.HierarchyLevelName = "service";
                metadata.ContainOperations = model.Operations;
            }

            // quality

            var conformExplanation = "Dataene er i henhold til produktspesifikasjonen";
            var conformExplanationEnglish = "The data is according to the product specification";

            var conformExplanationNotSet = "Dataene er ikke vurdert iht produktspesifikasjonen";
            var conformExplanationEnglishNotSet = "The data is not evaluated according to the product specification";

            if (model.QualitySpecificationResultInspire == true)
            {
                model.QualitySpecificationExplanationInspire = conformExplanation;
                model.EnglishQualitySpecificationExplanationInspire = conformExplanationEnglish;
            }
            else if (model.QualitySpecificationResultInspire == null)
            {
                model.QualitySpecificationExplanationInspire = conformExplanationNotSet;
                model.EnglishQualitySpecificationExplanationInspire = conformExplanationEnglishNotSet;
            }

            if (model.QualitySpecificationResultSosi == true)
            {
                model.QualitySpecificationExplanationSosi = conformExplanation;
                model.EnglishQualitySpecificationExplanationSosi = conformExplanationEnglish;
            }
            else if (model.QualitySpecificationResultSosi == null)
            {
                model.QualitySpecificationExplanationSosi = conformExplanationNotSet;
                model.EnglishQualitySpecificationExplanationSosi = conformExplanationEnglishNotSet;
            }

            if (model.QualitySpecificationResult == true)
            {
                model.QualitySpecificationExplanation = conformExplanation;
                model.EnglishQualitySpecificationExplanation = conformExplanationEnglish;
            }
            else if (model.QualitySpecificationResult == null)
            {
                model.QualitySpecificationExplanation = conformExplanationNotSet;
                model.EnglishQualitySpecificationExplanation = conformExplanationEnglishNotSet;
            }



            //List<SimpleQualitySpecification> qualityList = new List<SimpleQualitySpecification>();
            //if (!string.IsNullOrWhiteSpace(model.QualitySpecificationTitleInspire))
            //{
            //    qualityList.Add(new SimpleQualitySpecification
            //    {
            //        Title = model.QualitySpecificationTitleInspire,
            //        Date = string.Format("{0:yyyy-MM-dd}", model.QualitySpecificationDateInspire),
            //        DateType = model.QualitySpecificationDateTypeInspire,
            //        Explanation = model.QualitySpecificationExplanationInspire,
            //        EnglishExplanation = model.EnglishQualitySpecificationExplanationInspire,
            //        Result = model.QualitySpecificationResultInspire,
            //        Responsible = "inspire"
            //    });
            //}
            //if (!string.IsNullOrEmpty(model.ProductSpecificationUrl) && !string.IsNullOrWhiteSpace(model.QualitySpecificationTitleSosi) && !model.QualitySpecificationTitleSosi.Contains(UI.NoneSelected))
            //{
            //    qualityList.Add(new SimpleQualitySpecification
            //    {
            //        Title = model.QualitySpecificationTitleSosi,
            //        Date = model.QualitySpecificationDateSosi.HasValue ? string.Format("{0:yyyy-MM-dd}", model.QualitySpecificationDateSosi) : null,
            //        DateType = dateType,
            //        Explanation = model.QualitySpecificationExplanationSosi,
            //        EnglishExplanation = model.EnglishQualitySpecificationExplanationSosi,
            //        Result = model.QualitySpecificationResultSosi,
            //        Responsible = "sosi"
            //    });
            //}

            //if (!string.IsNullOrWhiteSpace(model.ApplicationSchema) && !model.IsService())
            //{
            //    if (HasFormat("sosi", model.DistributionsFormats))
            //    {
            //        if (model.QualitySpecificationResultSosiConformApplicationSchema == true)
            //        {
            //            qualityList.Add(new SimpleQualitySpecification
            //            {
            //                Title = "Sosi applikasjonsskjema",
            //                Date = string.Format("{0:yyyy-MM-dd}", model.QualitySpecificationDateSosi),
            //                DateType = dateType,
            //                Explanation = "SOSI-filer er i henhold til applikasjonsskjema",
            //                EnglishExplanation = "SOSI files are according to application form",
            //                Result = true,
            //                Responsible = "uml-sosi"
            //            });
            //        }
            //        else if (model.QualitySpecificationResultSosiConformApplicationSchema == null)
            //        {
            //            qualityList.Add(new SimpleQualitySpecification
            //            {
            //                Title = "Sosi applikasjonsskjema",
            //                Date = string.Format("{0:yyyy-MM-dd}", model.QualitySpecificationDateSosi),
            //                DateType = dateType,
            //                Explanation = "SOSI-filer er ikke vurdert i henhold til applikasjonsskjema",
            //                EnglishExplanation = "SOSI files are not evaluated according to application form",
            //                Result = null,
            //                Responsible = "uml-sosi"
            //            });
            //        }
            //        else
            //        {
            //            qualityList.Add(new SimpleQualitySpecification
            //            {
            //                Title = "Sosi applikasjonsskjema",
            //                Date = string.Format("{0:yyyy-MM-dd}", model.QualitySpecificationDateSosi),
            //                DateType = dateType,
            //                Explanation = "SOSI-filer avviker fra applikasjonsskjema",
            //                EnglishExplanation = "SOSI files are not according to application form",
            //                Result = false,
            //                Responsible = "uml-sosi"
            //            });
            //        }
            //    }
            //    if (HasFormat("gml", model.DistributionsFormats))
            //    {
            //        if (model.QualitySpecificationResultSosiConformGmlApplicationSchema == true)
            //        {
            //            qualityList.Add(new SimpleQualitySpecification
            //            {
            //                Title = "Sosi applikasjonsskjema",
            //                Date = string.Format("{0:yyyy-MM-dd}", model.QualitySpecificationDateSosi),
            //                DateType = dateType,
            //                Explanation = "GML-filer er i henhold til applikasjonsskjema",
            //                EnglishExplanation = "GML files are according to application form",
            //                Result = true,
            //                Responsible = "uml-gml"
            //            });
            //        }
            //        else if (model.QualitySpecificationResultSosiConformGmlApplicationSchema == null)
            //        {
            //            qualityList.Add(new SimpleQualitySpecification
            //            {
            //                Title = "Sosi applikasjonsskjema",
            //                Date = string.Format("{0:yyyy-MM-dd}", model.QualitySpecificationDateSosi),
            //                DateType = dateType,
            //                Explanation = "GML-filer er ikke vurdert i henhold til applikasjonsskjema",
            //                EnglishExplanation = "GML files are not evaluated according to application form",
            //                Result = null,
            //                Responsible = "uml-gml"
            //            });
            //        }
            //        else
            //        {
            //            qualityList.Add(new SimpleQualitySpecification
            //            {
            //                Title = "Sosi applikasjonsskjema",
            //                Date = string.Format("{0:yyyy-MM-dd}", model.QualitySpecificationDateSosi),
            //                DateType = dateType,
            //                Explanation = "GML-filer avviker fra applikasjonsskjema",
            //                EnglishExplanation = "GML files are not according to application form",
            //                Result = false,
            //                Responsible = "uml-gml"
            //            });
            //        }
            //    }
            //}
            //if (!string.IsNullOrWhiteSpace(model.QualitySpecificationTitle))
            //{
            //    qualityList.Add(new SimpleQualitySpecification
            //    {
            //        Title = model.QualitySpecificationTitle,
            //        Date = string.Format("{0:yyyy-MM-dd}", model.QualitySpecificationDate),
            //        DateType = model.QualitySpecificationDateType,
            //        Explanation = model.QualitySpecificationExplanation,
            //        EnglishExplanation = model.EnglishQualitySpecificationExplanation,
            //        Result = model.QualitySpecificationResult,
            //        Responsible = "other"
            //    });
            //}
            //if (model.IsService() && model.KeywordsNationalInitiative.Contains("Inspire") && !string.IsNullOrEmpty(distributionProtocolService))
            //{
            //    if (SimpleMetadata.IsAccessPoint(distributionProtocolService))
            //    {
            //        if (model.QualitySpecificationResultInspireSpatialServiceInteroperability == true)
            //        {
            //            qualityList.Add(new SimpleQualitySpecification
            //            {
            //                SpecificationLink = "http://inspire.ec.europa.eu/id/citation/ir/reg-1089-2010",
            //                Title = "COMMISSION REGULATION (EU) No 1089/2010 of 23 November 2010 implementing Directive 2007/2/EC of the European Parliament and of the Council as regards interoperability of spatial data sets and services",
            //                TitleLink = "http://data.europa.eu/eli/reg/2010/1089",
            //                Date = "2010-12-08",
            //                DateType = dateType,
            //                Explanation = "Denne romlige datatjenesten er i overensstemmelse med INSPIRE Implementing Rules for interoperabilitet av romlige datasett og tjenester",
            //                EnglishExplanation = "This Spatial Data Service set is conformant with the INSPIRE Implementing Rules for the interoperability of spatial data sets and services",
            //                Result = true,
            //                Responsible = "inspire-interop"
            //            });
            //        }
            //        else if (model.QualitySpecificationResultInspireSpatialServiceInteroperability == false)
            //        {
            //            qualityList.Add(new SimpleQualitySpecification
            //            {
            //                SpecificationLink = "http://inspire.ec.europa.eu/id/citation/ir/reg-1089-2010",
            //                Title = "COMMISSION REGULATION (EU) No 1089/2010 of 23 November 2010 implementing Directive 2007/2/EC of the European Parliament and of the Council as regards interoperability of spatial data sets and services",
            //                TitleLink = "http://data.europa.eu/eli/reg/2010/1089",
            //                Date = "2010-12-08",
            //                DateType = dateType,
            //                Explanation = "Denne romlige datatjenesten er ikke i overensstemmelse med INSPIRE Implementing Rules for interoperabilitet av romlige datasett og tjenester",
            //                EnglishExplanation = "This Spatial Data Service set is not conformant with the INSPIRE Implementing Rules for the interoperability of spatial data sets and services",
            //                Result = false,
            //                Responsible = "inspire-interop"
            //            });

            //        }
            //        else
            //        {
            //            qualityList.Add(new SimpleQualitySpecification
            //            {
            //                SpecificationLink = "http://inspire.ec.europa.eu/id/citation/ir/reg-1089-2010",
            //                Title = "COMMISSION REGULATION (EU) No 1089/2010 of 23 November 2010 implementing Directive 2007/2/EC of the European Parliament and of the Council as regards interoperability of spatial data sets and services",
            //                TitleLink = "http://data.europa.eu/eli/reg/2010/1089",
            //                Date = "2010-12-08",
            //                DateType = dateType,
            //                Explanation = "Denne tjenesten er ikke evaluert mot INSPIRE Implementing Rules for interoperabilitet av romlige datasett og tjenester",
            //                EnglishExplanation = "This Spatial Data Service set is not evaluated conformant with the INSPIRE Implementing Rules for the interoperability of spatial data sets and services",
            //                Result = null,
            //                Responsible = "inspire-interop"
            //            });
            //        }

            //        if (!string.IsNullOrEmpty(model.QualitySpecificationTitleInspireSpatialServiceTechnicalConformance))
            //        {
            //            var technicalSpesification = Technical.GetSpecification(model.QualitySpecificationTitleInspireSpatialServiceTechnicalConformance);

            //            if (model.QualitySpecificationResultInspireSpatialServiceTechnicalConformance == true)
            //            {
            //                qualityList.Add(new SimpleQualitySpecification
            //                {
            //                    Title = technicalSpesification.Name,
            //                    TitleLink = technicalSpesification.Url,
            //                    Date = technicalSpesification.PublicationDate,
            //                    DateType = dateType,
            //                    Explanation = "Denne geodatatjenesten er i overensstemmelse med " + technicalSpesification.Name + " spesifikasjonen",
            //                    EnglishExplanation = "This Spatial Data Service set is conformant with the " + technicalSpesification.Name + " specification",
            //                    Result = true,
            //                    Responsible = "conformity-to-technical-specification"
            //                });
            //            }
            //            else if (model.QualitySpecificationResultInspireSpatialServiceTechnicalConformance == false)
            //            {
            //                qualityList.Add(new SimpleQualitySpecification
            //                {
            //                    Title = technicalSpesification.Name,
            //                    TitleLink = technicalSpesification.Url,
            //                    Date = technicalSpesification.PublicationDate,
            //                    DateType = dateType,
            //                    Explanation = "Denne geodatatjenesten er ikke i overensstemmelse med " + technicalSpesification.Name + " spesifikasjonen",
            //                    EnglishExplanation = "This Spatial Data Service set is not conformant with the " + technicalSpesification.Name + " specification",
            //                    Result = false,
            //                    Responsible = "conformity-to-technical-specification"
            //                });

            //            }
            //            else
            //            {
            //                qualityList.Add(new SimpleQualitySpecification
            //                {
            //                    Title = technicalSpesification.Name,
            //                    TitleLink = technicalSpesification.Url,
            //                    Date = technicalSpesification.PublicationDate,
            //                    DateType = dateType,
            //                    Explanation = "Denne geodatatjenesten er ikke evaluert i overensstemmelse med " + technicalSpesification.Name + " spesifikasjonen",
            //                    EnglishExplanation = "This Spatial Data Service set is not evaluated conformant with the " + technicalSpesification.Name + " specification",
            //                    Result = null,
            //                    Responsible = "conformity-to-technical-specification"
            //                });
            //            }
            //        }

            //        if (!string.IsNullOrEmpty(model.QualitySpecificationTitleInspireSpatialServiceConformance))
            //        {
            //            string sds = model.QualitySpecificationTitleInspireSpatialServiceConformance.ToLower();
            //            string Sds = CapitalizeFirstLetter(sds);

            //            if (model.QualitySpecificationResultInspireSpatialServiceConformance == true)
            //            {
            //                qualityList.Add(new SimpleQualitySpecification
            //                {
            //                    Title = sds,
            //                    TitleLink = "http://inspire.ec.europa.eu/id/ats/metadata/2.0/sds-" + sds,
            //                    TitleLinkDescription = "INSPIRE " + Sds + " Spatial Data Services metadata",
            //                    Date = "2016-05-01",
            //                    DateType = dateType,
            //                    Explanation = "Denne romlige datatjenesten er i samsvar med INSPIRE-kravene for " + Sds + " Spatial Data Services",
            //                    EnglishExplanation = "This Spatial Data Service set is conformant with the INSPIRE requirements for " + Sds + " Spatial Data Services",
            //                    Result = true,
            //                    Responsible = "inspire-conformance"
            //                });

            //            }
            //            else if (model.QualitySpecificationResultInspireSpatialServiceConformance == false)
            //            {
            //                qualityList.Add(new SimpleQualitySpecification
            //                {
            //                    Title = sds,
            //                    TitleLink = "http://inspire.ec.europa.eu/id/ats/metadata/2.0/sds-" + sds,
            //                    TitleLinkDescription = "INSPIRE " + Sds + " Spatial Data Services metadata",
            //                    Date = "2016-05-01",
            //                    DateType = dateType,
            //                    Explanation = "Denne romlige datatjenesten er ikke i samsvar med INSPIRE-kravene for " + Sds + " Spatial Data Services",
            //                    EnglishExplanation = "This Spatial Data Service set is not conformant with the INSPIRE requirements for " + Sds + " Spatial Data Services",
            //                    Result = false,
            //                    Responsible = "inspire-conformance"
            //                });

            //            }
            //            else
            //            {
            //                qualityList.Add(new SimpleQualitySpecification
            //                {
            //                    Title = sds,
            //                    TitleLink = "http://inspire.ec.europa.eu/id/ats/metadata/2.0/sds-" + sds,
            //                    TitleLinkDescription = "INSPIRE " + Sds + " Spatial Data Services metadata",
            //                    Date = "2016-05-01",
            //                    DateType = dateType,
            //                    Explanation = "Denne tjenesten er ikke evaluert mot INSPIRE-kravene for " + Sds + " Spatial Data Services",
            //                    EnglishExplanation = "This Spatial Data Service set is not evaluated conformant with the INSPIRE requirements for " + Sds + " Spatial Data Services",
            //                    Result = null,
            //                    Responsible = "inspire-conformance"
            //                });
            //            }

            //            if (!string.IsNullOrEmpty(model.QualityQuantitativeResultAvailability))
            //            {
            //                qualityList.Add(new SimpleQualitySpecification
            //                {
            //                    QuantitativeResult = model.QualityQuantitativeResultAvailability,
            //                    Responsible = "sds-availability"
            //                });
            //            }

            //            if (model.QualityQuantitativeResultCapacity != null)
            //            {
            //                qualityList.Add(new SimpleQualitySpecification
            //                {
            //                    QuantitativeResult = model.QualityQuantitativeResultCapacity.ToString(),
            //                    Responsible = "sds-capacity"
            //                });
            //            }

            //            if (!string.IsNullOrEmpty(model.QualityQuantitativeResultPerformance))
            //            {
            //                qualityList.Add(new SimpleQualitySpecification
            //                {
            //                    QuantitativeResult = model.QualityQuantitativeResultPerformance,
            //                    Responsible = "sds-performance"
            //                });
            //            }

            //        }
            //    }
            //    if (SimpleMetadata.IsNetworkService(distributionProtocolService))
            //    {
            //        if (model.QualitySpecificationResultInspireSpatialNetworkServices == true)
            //        {
            //            qualityList.Add(new SimpleQualitySpecification
            //            {
            //                SpecificationLink = "http://inspire.ec.europa.eu/id/citation/ir/reg-976-2009",
            //                Title = "COMMISSION REGULATION (EC) No 976/2009 of 19 October 2009 implementing Directive 2007/2/EC of the European Parliament and of the Council as regards the Network Services",
            //                TitleLink = "http://data.europa.eu/eli/reg/2009/976",
            //                Date = "2010-12-08",
            //                DateType = dateType,
            //                Explanation = "Dette datasettet er i samsvar med INSPIRE Implementeringsregler for nettverkstjenester",
            //                EnglishExplanation = "This data set is conformant with the INSPIRE Implementing Rules for Network Services",
            //                Result = true,
            //                Responsible = "inspire-networkservice"
            //            });
            //        }
            //        else if (model.QualitySpecificationResultInspireSpatialNetworkServices == false)
            //        {
            //            qualityList.Add(new SimpleQualitySpecification
            //            {
            //                SpecificationLink = "http://inspire.ec.europa.eu/id/citation/ir/reg-976-2009",
            //                Title = "COMMISSION REGULATION (EC) No 976/2009 of 19 October 2009 implementing Directive 2007/2/EC of the European Parliament and of the Council as regards the Network Services",
            //                TitleLink = "http://data.europa.eu/eli/reg/2009/976",
            //                Date = "2010-12-08",
            //                DateType = dateType,
            //                Explanation = "Dette datasettet er ikke i samsvar med INSPIRE Implementeringsregler for nettverkstjenester",
            //                EnglishExplanation = "This data set is not conformant with the INSPIRE Implementing Rules for Network Services",
            //                Result = false,
            //                Responsible = "inspire-networkservice"
            //            });

            //        }
            //        else
            //        {
            //            qualityList.Add(new SimpleQualitySpecification
            //            {
            //                SpecificationLink = "http://inspire.ec.europa.eu/id/citation/ir/reg-976-2009",
            //                Title = "COMMISSION REGULATION (EC) No 976/2009 of 19 October 2009 implementing Directive 2007/2/EC of the European Parliament and of the Council as regards the Network Services",
            //                TitleLink = "http://data.europa.eu/eli/reg/2009/976",
            //                Date = "2010-12-08",
            //                DateType = dateType,
            //                Explanation = "Dette datasettet er ikke evaluert mot INSPIRE Implementeringsregler for nettverkstjenester",
            //                EnglishExplanation = "This data set is not evaluated conformant with the INSPIRE Implementing Rules for Network Services",
            //                Result = null,
            //                Responsible = "inspire-networkservice"
            //            });
            //        }
            //    }
            //}

            //metadata.QualitySpecifications = qualityList;

            metadata.ProcessHistory = !string.IsNullOrWhiteSpace(model.ProcessHistory) ? model.ProcessHistory : " ";

            if (!string.IsNullOrWhiteSpace(model.MaintenanceFrequency))
                metadata.MaintenanceFrequency = model.MaintenanceFrequency;

            if (model.IsDataset() || model.IsDatasetSeries())
                metadata.ResolutionScale = !string.IsNullOrEmpty(model.ResolutionScale) ? model.ResolutionScale : " ";

            if ((model.IsDataset() || model.IsDatasetSeries()) && !string.IsNullOrEmpty(model.ResolutionDistance))
            {
                var distance = String.Format(CultureInfo.InvariantCulture, model.ResolutionDistance);
                metadata.ResolutionDistance = Double.Parse(distance, CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrWhiteSpace(model.Status))
                metadata.Status = model.Status;

            metadata.DateCreated = model.DateCreated;
            metadata.DatePublished = model.DatePublished;
            metadata.DateUpdated = model.DateUpdated;

            DateTime? DateMetadataValidFrom = model.DateMetadataValidFrom;
            DateTime? DateMetadataValidTo = model.DateMetadataValidTo;

            metadata.ValidTimePeriod = new SimpleValidTimePeriod()
            {
                ValidFrom = DateMetadataValidFrom != null ? String.Format("{0:yyyy-MM-dd}", DateMetadataValidFrom) : "",
                ValidTo = DateMetadataValidTo != null ? String.Format("{0:yyyy-MM-dd}", DateMetadataValidTo) : ""
            };


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

            var accessConstraintsSelected = model.AccessConstraints;
            string otherConstraintsAccess = model.OtherConstraintsAccess;

            var accessConstraintsLink = "http://inspire.ec.europa.eu/metadata-codelist/LimitationsOnPublicAccess/noLimitations";

            //Dictionary<string, string> inspireAccessRestrictions = GetInspireAccessRestrictions();

            //if (!string.IsNullOrEmpty(accessConstraintsSelected))
            //{
            //    if (accessConstraintsSelected.ToLower() == "no restrictions" || accessConstraintsSelected.ToLower() == "norway digital restricted")
            //    {
            //        otherConstraintsAccess = accessConstraintsSelected;

            //        if (accessConstraintsSelected.ToLower() == "no restrictions")
            //            accessConstraintsSelected = inspireAccessRestrictions[accessConstraintsLink];

            //        if (accessConstraintsSelected.ToLower() == "norway digital restricted")
            //        {
            //            accessConstraintsLink = "http://inspire.ec.europa.eu/metadata-codelist/LimitationsOnPublicAccess/INSPIRE_Directive_Article13_1d";
            //            accessConstraintsSelected = inspireAccessRestrictions[accessConstraintsLink];
            //        }

            //    }
            //    else if (accessConstraintsSelected == "restricted")
            //    {
            //        otherConstraintsAccess = null;
            //        accessConstraintsLink = "http://inspire.ec.europa.eu/metadata-codelist/LimitationsOnPublicAccess/INSPIRE_Directive_Article13_1b";
            //        accessConstraintsSelected = inspireAccessRestrictions[accessConstraintsLink];
            //    }
            //}

            if (string.IsNullOrEmpty(model.UseConstraints))
            {
                model.OtherConstraintsLink = "http://inspire.ec.europa.eu/metadata-codelist/ConditionsApplyingToAccessAndUse/noConditionsApply";
                model.OtherConstraintsLinkText = "No conditions apply to access and use";
            }

            metadata.Constraints = new SimpleConstraints
            {
                AccessConstraints = !string.IsNullOrWhiteSpace(accessConstraintsSelected) ? accessConstraintsSelected : "",
                AccessConstraintsLink = accessConstraintsLink,
                OtherConstraints = !string.IsNullOrWhiteSpace(model.OtherConstraints) ? model.OtherConstraints : "",
                EnglishOtherConstraints = !string.IsNullOrWhiteSpace(model.EnglishOtherConstraints) ? model.EnglishOtherConstraints : "",
                //OtherConstraintsLink = !string.IsNullOrWhiteSpace(model.OtherConstraintsLink) ? model.OtherConstraintsLink : null,
                UseConstraintsLicenseLink = !string.IsNullOrWhiteSpace(model.OtherConstraintsLink) ? model.OtherConstraintsLink : null,
                //OtherConstraintsLinkText = !string.IsNullOrWhiteSpace(model.OtherConstraintsLinkText) ? model.OtherConstraintsLinkText : null,
                UseConstraintsLicenseLinkText = !string.IsNullOrWhiteSpace(model.OtherConstraintsLinkText) ? model.OtherConstraintsLinkText : null,
                SecurityConstraints = !string.IsNullOrWhiteSpace(model.SecurityConstraints) ? model.SecurityConstraints : "",
                SecurityConstraintsNote = !string.IsNullOrWhiteSpace(model.SecurityConstraintsNote) ? model.SecurityConstraintsNote : "",
                //UseConstraints = !string.IsNullOrWhiteSpace(model.UseConstraints) ? "license" : "",
                UseLimitations = !string.IsNullOrWhiteSpace(model.UseLimitations) ? model.UseLimitations : "",
                EnglishUseLimitations = !string.IsNullOrWhiteSpace(model.EnglishUseLimitations) ? model.EnglishUseLimitations : "",
                //OtherConstraintsAccess = !string.IsNullOrWhiteSpace(otherConstraintsAccess) ? otherConstraintsAccess : "",
            };

            if (model.IsService() && model.DistributionsFormats != null && model.DistributionsFormats.Count > 0)
            {
                //model.KeywordsServiceType = AddKeywordForService(model);
                //metadata.ServiceType = GetServiceType(model.DistributionsFormats[0].Protocol);
            }
            metadata.Keywords = model.GetAllKeywords();

            bool hasEnglishFields = false;
            // don't create PT_FreeText fields if it isn't necessary
            if (!string.IsNullOrWhiteSpace(model.EnglishTitle))
            {
                metadata.EnglishTitle = model.EnglishTitle;
                hasEnglishFields = true;
            }
            if (!string.IsNullOrWhiteSpace(model.EnglishAbstract))
            {
                metadata.EnglishAbstract = model.EnglishAbstract;
                hasEnglishFields = true;
            }

            if (!string.IsNullOrWhiteSpace(model.EnglishPurpose))
            {
                metadata.EnglishPurpose = model.EnglishPurpose;
                hasEnglishFields = true;
            }

            if (!string.IsNullOrWhiteSpace(model.EnglishSupplementalDescription))
            {
                metadata.EnglishSupplementalDescription = model.EnglishSupplementalDescription;
                hasEnglishFields = true;
            }

            if (!string.IsNullOrWhiteSpace(model.EnglishSpecificUsage))
            {
                metadata.EnglishSpecificUsage = model.EnglishSpecificUsage;
                hasEnglishFields = true;
            }

            if (!string.IsNullOrWhiteSpace(model.EnglishProcessHistory))
            {
                metadata.EnglishProcessHistory = model.EnglishProcessHistory;
                hasEnglishFields = true;
            }

            if (hasEnglishFields)
                metadata.SetLocale(SimpleMetadata.LOCALE_ENG);

            if (model.OperatesOn != null)
                metadata.OperatesOn = model.OperatesOn;

            if (model.CrossReference != null)
                metadata.CrossReference = model.CrossReference;

            if (!string.IsNullOrWhiteSpace(model.ResourceReferenceCode) || !string.IsNullOrWhiteSpace(model.ResourceReferenceCodespace))
            {
                metadata.ResourceReference = new SimpleResourceReference
                {
                    Code = model.ResourceReferenceCode != null ? model.ResourceReferenceCode : null,
                    Codespace = model.ResourceReferenceCodespace != null ? model.ResourceReferenceCodespace : null
                };
            }

            if (model.IsService())
                metadata.AccessProperties = new SimpleAccessProperties { OrderingInstructions = model.OrderingInstructions };

            SetDefaultValuesOnMetadata(metadata);

            if (!string.IsNullOrEmpty(model.MetadataStandard))
                metadata.MetadataStandard = model.MetadataStandard;
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