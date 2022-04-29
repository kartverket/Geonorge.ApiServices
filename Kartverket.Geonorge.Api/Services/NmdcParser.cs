using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Xml;
using System.Linq;
using Kartverket.Geonorge.Api.Models;
using GeoNorgeAPI;
using Newtonsoft.Json.Linq;

namespace Kartverket.Geonorge.Api.Services
{
    public class NmdcParser
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public List<DatasetNMDC> ParseDatasets(string xml)
        {
            GetCodeValueForTopic();

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
                    var dateDatasetUpdated = childrenNode.SelectSingleNode("n:header/n:datestamp", nsmgr);
                    var dateMetadataUpdated = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Last_DIF_Revision_Date", nsmgr);
                    var dateStart = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Temporal_Coverage/ns2:Start_Date", nsmgr);
                    var dateStop = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Temporal_Coverage/ns2:Stop_Date", nsmgr);
                    var title = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Data_Set_Citation/ns2:Dataset_Title", nsmgr);
                    var titleEntry = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Entry_Title", nsmgr);
                    var summary = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Summary/ns2:Abstract", nsmgr);
                    var organization = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Data_Set_Citation/ns2:Dataset_Publisher", nsmgr);
                    var originatingCenter = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Originating_Center", nsmgr);

                    var dataCenters = childrenNode.SelectNodes("n:metadata/ns2:DIF/ns2:Data_Center", nsmgr);

                    var southernmostLatitude = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Spatial_Coverage/ns2:Southernmost_Latitude", nsmgr);
                    var northernBoundLatitude = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Spatial_Coverage/ns2:Northernmost_Latitude", nsmgr);
                    var westernBoundLatitude = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Spatial_Coverage/ns2:Westernmost_Longitude", nsmgr);
                    var easternBoundLatitude = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Spatial_Coverage/ns2:Easternmost_Longitude", nsmgr);

                    var processHistory = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Quality", nsmgr);

                    var status = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Data_Set_Progress", nsmgr);

                    var metadataName = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Metadata_Name", nsmgr);
                    var metadataVersion = childrenNode.SelectSingleNode("n:metadata/ns2:DIF/ns2:Metadata_Version", nsmgr);

                    var topicCategoriesNodes = childrenNode.SelectNodes("n:metadata/ns2:DIF/ns2:ISO_Topic_Category", nsmgr);

                    dataset.Uuid = id.InnerXml;
                    try
                    { 
                        if(!string.IsNullOrEmpty(dateMetadataUpdated?.InnerXml))
                            dataset.DateMetadataUpdated = DateTime.Parse(dateMetadataUpdated.InnerXml, System.Globalization.CultureInfo.InvariantCulture);
                        if (!string.IsNullOrEmpty(dateDatasetUpdated?.InnerXml))
                            dataset.LastUpdated = DateTime.Parse(dateDatasetUpdated.InnerXml, System.Globalization.CultureInfo.InvariantCulture);

                        if (!string.IsNullOrEmpty(dateStart?.InnerXml))
                            dataset.StartDate = dateStart.InnerXml;
                        if (!string.IsNullOrEmpty(dateStop?.InnerXml))
                            dataset.StopDate = dateStop.InnerXml;
                    }
                    catch (Exception e)
                    {
                        Log.Error("Error with date parsing: " + id, e);
                    }

                    if(!string.IsNullOrEmpty(title?.InnerXml))
                        dataset.Title = title.InnerXml;
                    else if(!string.IsNullOrEmpty(titleEntry?.InnerXml))
                        dataset.Title = titleEntry.InnerXml;

                    if (!string.IsNullOrEmpty(summary?.InnerXml))
                        dataset.Abstract = summary?.InnerXml;

                    if (!string.IsNullOrEmpty(originatingCenter?.InnerXml))
                        dataset.Organization = originatingCenter.InnerXml;
                    else if (!string.IsNullOrEmpty(organization?.InnerXml))
                        dataset.Organization = organization.InnerXml;

                    foreach(XmlNode dataCenter in dataCenters) 
                    {
                        var longNameNode = dataCenter.SelectSingleNode("ns2:Data_Center_Name/ns2:Long_Name", nsmgr);
                        if (!string.IsNullOrEmpty(longNameNode?.InnerXml))
                        {
                            var dataCenterName = longNameNode.InnerXml;
                            if(dataCenterName == dataset.Organization) 
                            {
                                var personnelNode = dataCenter.SelectSingleNode("ns2:Personnel", nsmgr);
                                if(personnelNode != null) 
                                { 
                                    var firstName = personnelNode.SelectSingleNode("ns2:First_Name", nsmgr);
                                    var middleName = personnelNode.SelectSingleNode("ns2:Middle_Name", nsmgr);
                                    var lastName = personnelNode.SelectSingleNode("ns2:Last_Name", nsmgr);
                                    var email = personnelNode.SelectSingleNode("ns2:Email", nsmgr);

                                    string name = "";

                                    if (!string.IsNullOrEmpty(firstName?.InnerXml))
                                        name = firstName.InnerXml;

                                    if (!string.IsNullOrEmpty(middleName?.InnerXml))
                                        name = name + " " + middleName.InnerXml;

                                    if (!string.IsNullOrEmpty(lastName?.InnerXml))
                                        name = name + " " + lastName.InnerXml;

                                    string emailPersonnel = "";
                                    if (!string.IsNullOrEmpty(email?.InnerXml))
                                        emailPersonnel = email.InnerXml;

                                    if (!string.IsNullOrEmpty(name))
                                        dataset.OrganizationPersonnelName = name;

                                    if (!string.IsNullOrEmpty(name))
                                        dataset.OrganizationPersonnelEmail = emailPersonnel;
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(southernmostLatitude?.InnerXml))
                        dataset.BBoxSouthBoundLatitude = southernmostLatitude.InnerXml;

                    if (!string.IsNullOrEmpty(northernBoundLatitude?.InnerXml))
                        dataset.BBoxNorthBoundLatitude = northernBoundLatitude.InnerXml;

                    if (!string.IsNullOrEmpty(westernBoundLatitude?.InnerXml))
                        dataset.BBoxWestBoundLongitude = westernBoundLatitude.InnerXml;

                    if (!string.IsNullOrEmpty(easternBoundLatitude?.InnerXml))
                        dataset.BBoxEastBoundLongitude = easternBoundLatitude.InnerXml;

                    if (!string.IsNullOrEmpty(processHistory?.InnerXml))
                        dataset.ProcessHistory = processHistory.InnerXml;

                    if (!string.IsNullOrEmpty(status?.InnerXml)) { 
                        dataset.Status = status.InnerXml;
                        if (dataset.Status.ToLower() == "in work")
                            dataset.Status = "onGoing";
                        else if (dataset.Status.ToLower() == "complete")
                            dataset.Status = "completed";
                        else if (dataset.Status.ToLower() == "planned")
                            dataset.Status = "planned";
                    }

                    if (!string.IsNullOrEmpty(metadataName?.InnerXml))
                        dataset.MetadataName = metadataName.InnerXml;

                    if (!string.IsNullOrEmpty(metadataVersion?.InnerXml))
                        dataset.MetadataVersion = metadataVersion.InnerXml;

                    if (topicCategoriesNodes != null && topicCategoriesNodes.Count > 0) 
                    {
                        dataset.TopicCategories = new List<string>();
                        foreach (XmlNode topic in topicCategoriesNodes)
                        {
                            var topicCategoryKey = topic.InnerXml;
                            topicCategoryKey = topicCategoryKey.Replace(" ", "");
                            topicCategoryKey = topicCategoryKey.ToLower();
                            if (Topics.ContainsKey(topicCategoryKey)) 
                            { 
                                var topicCategory = Topics[topicCategoryKey];
                                if(topicCategory != null)
                                    dataset.TopicCategories.Add(topicCategory);
                            }
                        }
                    }


                    dataset.DistributionsFormats = new List<SimpleDistribution>();
                    dataset.ReferenceSystems = new List<SimpleReferenceSystem>();
                    datasets.Add(dataset);
                }
            return datasets;
        }


        public static Dictionary<string, string> Topics = new Dictionary<string, string>();

        private static void GetCodeValueForTopic()
        {
            if(Topics.Count == 0) 
            { 
                Dictionary<string, string> TopicsList = new Dictionary<string, string>();

                string url = "https://register.geonorge.no/metadata-kodelister/tematisk-hovedkategori.json?lang=en";
                System.Net.WebClient c = new System.Net.WebClient();
                c.Encoding = System.Text.Encoding.UTF8;
                var data = c.DownloadString(url);
                var response = Newtonsoft.Json.Linq.JObject.Parse(data);

                var codeList = response["containeditems"];

                foreach (var code in codeList)
                {
                    JToken labelToken = code["label"];

                    JToken codeToken = code["codevalue"];

                    if (labelToken != null && codeToken != null)
                    {
                        string label = labelToken?.ToString().Replace(" ","").ToLower();
                        string codevalue = codeToken?.ToString();

                        if (!TopicsList.ContainsKey(label))
                        TopicsList.Add(label, codevalue);
                    }

                }

                Topics = TopicsList;
            }
        }
    }
}
