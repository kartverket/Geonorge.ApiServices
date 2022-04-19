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

        public List<Dataset> ParseDatasets(string xml)
        {
            var datasets = new List<Dataset>();

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
                    var dataset = new Dataset();
                    var id = childrenNode.SelectSingleNode("n:header/n:identifier", nsmgr);
                    var title = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Entry_Title", nsmgr);
                    var titleText = title.InnerXml;
                    System.Diagnostics.Debug.WriteLine(id.InnerXml + ": " + titleText);
                    dataset.Uuid = id.InnerXml;
                    dataset.DistributionsFormats = new List<SimpleDistribution>();
                    dataset.ReferenceSystems = new List<SimpleReferenceSystem>();
                    datasets.Add(dataset);
                }
            return datasets;
        }

    }
}
