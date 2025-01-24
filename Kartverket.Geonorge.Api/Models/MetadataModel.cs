using GeoNorgeAPI;
using Kartverket.Geonorge.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Kartverket.Geonorge.Api.Models
{
    public class MetadataModel
    {
        public MetadataModel()
        {
            KeywordsTheme = new List<string>();
            KeywordsNationalTheme = new List<string>();
        }

        [Required(ErrorMessage = "Tittel er påkrevd")]
        public string Title { get; set; }
        [Required(ErrorMessage = "Beskrivelse er påkrevd.")]
        public string Description { get; set; }
        [Required(ErrorMessage = "Navn er påkrevd.")]
        public string ContactName { get; set; }

        [Required(ErrorMessage = "Epost er påkrevd.")]
        [EmailAddress(ErrorMessage = "Epost-adressen er ugyldig")]
        public string ContactEmail { get; set; }
        [Required(ErrorMessage = "Organisasjon er påkrevd.")]
        public string ContactOrganization { get; set; }

        public List<String> KeywordsTheme { get; set; }

        public string TopicCategory { get; set; }

        public List<String> KeywordsNationalTheme { get; set; }

        public string SecurityConstraints { get; set; }

        public string AccessConstraints { get; set; }

        public List<SimpleReferenceSystem> ReferenceSystems { get; set; }

        public string SpatialRepresentation { get; set; }

        public DateTime? DateMetadataValidFrom { get; set; }
        public DateTime? DateMetadataValidTo { get; set; }

        public List<SimpleDistribution> DistributionsFormats { get; set; }

        public string OtherConstraintsLink { get; set; }
        public string OtherConstraintsLinkText { get; set; }

        [Required(ErrorMessage = "Geografisk utstrekning nord er påkrevd")]
        [RegularExpression(@"-?([0-9]+)(\.[0-9]+)?")]
        public string BoundingBoxEast { get; set; }

        [Required(ErrorMessage = "Geografisk utstrekning vest er påkrevd")]
        [RegularExpression(@"-?([0-9]+)(\.[0-9]+)?")]
        public string BoundingBoxWest { get; set; }

        [Required(ErrorMessage = "Geografisk utstrekning nord er påkrevd")]
        [RegularExpression(@"-?([0-9]+)(\.[0-9]+)?")]
        public string BoundingBoxNorth { get; set; }

        [Required(ErrorMessage = "Geografisk utstrekning sør er påkrevd")]
        [RegularExpression(@"-?([0-9]+)(\.[0-9]+)?")]
        public string BoundingBoxSouth { get; set; }

        internal List<SimpleKeyword> GetAllKeywords()
        {
            List<SimpleKeyword> allKeywords = new List<SimpleKeyword>();
            allKeywords.AddRange(CreateKeywords(KeywordsTheme, "Theme", SimpleKeyword.TYPE_THEME, null));
            allKeywords.AddRange(CreateKeywords(KeywordsNationalTheme, "NationalTheme", null, SimpleKeyword.THESAURUS_NATIONAL_THEME));

            return allKeywords;
        }


        internal List<SimpleKeyword> CreateKeywords(List<string> inputList, string prefix, string type = null, string thesaurus = null)
        {
            List<SimpleKeyword> output = new List<SimpleKeyword>();

            if (inputList != null)
            {
                inputList = inputList.Distinct().ToList();

                foreach (var keyword in inputList)
                {
                    string keywordString = keyword;
                    string keywordLink = null;
                    if (keyword.Contains("|"))
                    {
                        keywordString = keyword.Split('|')[0];
                        keywordLink = keyword.Split('|')[1];
                    }

                    output.Add(new SimpleKeyword
                    {
                        Keyword = keywordString,
                        KeywordLink = keywordLink,
                        Thesaurus = thesaurus,
                        Type = type,
                        EnglishKeyword = null,
                    });
                }
            }
            return output;
        }

        internal List<SimpleReferenceSystem> GetReferenceSystems()
        {
            if (ReferenceSystems == null)
                return null;

            List<SimpleReferenceSystem> referenceSystems = new List<SimpleReferenceSystem>();

            for (int r = 0; r < ReferenceSystems.Count; r++)
            {
                if (!string.IsNullOrEmpty(ReferenceSystems[r]?.CoordinateSystem))
                {
                    SimpleReferenceSystem referenceSystem = new SimpleReferenceSystem();
                    referenceSystem.CoordinateSystem = GetCoordinatesystemText(ReferenceSystems[r].CoordinateSystem);
                    if (!string.IsNullOrEmpty(ReferenceSystems[r].CoordinateSystemLink))
                        referenceSystem.CoordinateSystemLink = ReferenceSystems[r].CoordinateSystemLink;
                    else
                        referenceSystem.CoordinateSystemLink = ReferenceSystems[r].CoordinateSystem;
                    referenceSystems.Add(referenceSystem);
                }
            }

            return referenceSystems;

        }

        public static string GetCoordinatesystemText(string coordinateSystem)
        {
            if (string.IsNullOrEmpty(coordinateSystem))
                return null;

            string coordinateSystemtext = coordinateSystem;
            string coordinateSystemCode = coordinateSystem.Substring(coordinateSystem.LastIndexOf('/') + 1);
            if (!string.IsNullOrEmpty(coordinateSystemCode))
            {
                if (!coordinateSystemCode.StartsWith("EPSG"))
                    coordinateSystemtext = "EPSG:" + coordinateSystemCode;
                else
                    coordinateSystemtext = coordinateSystemCode;
            }

            return coordinateSystemtext;
        }

        internal List<SimpleDistribution> GetDistributionsFormats()
        {
            List<SimpleDistribution> distributionsFormats = new List<SimpleDistribution>();

            if (DistributionsFormats != null)
            {
                for (int d = 0; d < DistributionsFormats.Count; d++)
                {
                    SimpleDistribution distributionFormat = new SimpleDistribution();
                    distributionFormat.Organization = DistributionsFormats[d].Organization;
                    distributionFormat.FormatName = DistributionsFormats[d].FormatName;
                    distributionFormat.FormatVersion = DistributionsFormats[d].FormatVersion;
                    distributionFormat.Protocol = DistributionsFormats[d].Protocol;
                    distributionFormat.URL = DistributionsFormats[d].URL;
                    distributionFormat.Name = DistributionsFormats[d].Name;
                    distributionFormat.UnitsOfDistribution = DistributionsFormats[d].UnitsOfDistribution;
                    distributionsFormats.Add(distributionFormat);
                }
            }
            return distributionsFormats;

        }
    }
}