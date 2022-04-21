using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Xml;
using System.Linq;
using Kartverket.Geonorge.Api.Models;
using GeoNorgeAPI;

namespace Kartverket.Geonorge.Api.Services
{
    public class NmdcParser
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public List<DatasetNMDC> ParseDatasets(string xml)
        {
            var datasets = new List<DatasetNMDC>();

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);

            string xpath = "//n:ListRecords/n:record";

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("n", "http://www.openarchives.org/OAI/2.0/");
            nsmgr.AddNamespace("ns2", "http://gcmd.gsfc.nasa.gov/Aboutus/xml/dif/");

            var nodes = xmlDoc.SelectNodes(xpath, nsmgr);

            if (nodes != null)
                foreach (XmlNode childrenNode in nodes)
                {
                    var dataset = new DatasetNMDC();
                    var id = childrenNode.SelectSingleNode("n:header/n:identifier", nsmgr);
                    var dateMetadataUpdated = childrenNode.SelectSingleNode("n:header/n:datestamp", nsmgr);
                    var title = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Data_Set_Citation/ns2:Dataset_Title", nsmgr);
                    var titleEntry = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Entry_Title", nsmgr);
                    var summary = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Summary/ns2:Abstract", nsmgr);
                    var organization = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Data_Set_Citation/ns2:Dataset_Publisher", nsmgr);

                    dataset.Uuid = id.InnerXml;
                    try
                    { 
                        if(!string.IsNullOrEmpty(dateMetadataUpdated?.InnerXml))
                            dataset.DateMetadataUpdated = DateTime.Parse(dateMetadataUpdated.InnerXml, System.Globalization.CultureInfo.InvariantCulture); 
                    }
                    catch (Exception e)
                    {
                        Log.Error("Error with LastUpdated: " + id, e);
                    }

                    if(!string.IsNullOrEmpty(title?.InnerXml))
                        dataset.Title = title.InnerXml;
                    else if(!string.IsNullOrEmpty(titleEntry?.InnerXml))
                        dataset.Title = titleEntry.InnerXml;

                    if (!string.IsNullOrEmpty(summary?.InnerXml))
                        dataset.Abstract = summary?.InnerXml;

                    if (!string.IsNullOrEmpty(organization?.InnerXml))
                        dataset.Organization = organization.InnerXml;
                    else
                        System.Diagnostics.Debug.WriteLine("organization missing for " + dataset.Title);

                    dataset.DistributionsFormats = new List<SimpleDistribution>();
                    dataset.ReferenceSystems = new List<SimpleReferenceSystem>();
                    datasets.Add(dataset);
                }
            return datasets;
        }

    }
}
