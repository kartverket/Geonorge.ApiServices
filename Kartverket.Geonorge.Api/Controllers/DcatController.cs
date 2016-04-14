using Kartverket.Geonorge.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;

namespace Kartverket.Geonorge.Api.Controllers
{
    public class DcatController : ApiController
    {
        /// <summary>
        /// Catalogue in dcat format
        /// </summary>
        [System.Web.Http.Route("metadata/dcat")]
        [System.Web.Http.HttpGet]
        public System.Net.Http.HttpResponseMessage GetDcat()
        {
            System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
            doc.Load(System.Web.HttpContext.Current.Request.MapPath("~\\dcat\\geonorge_dcat.rdf"));
            return new System.Net.Http.HttpResponseMessage()
            { Content = new System.Net.Http.StringContent(doc.OuterXml, System.Text.Encoding.UTF8, "application/xml") };
        }

        [System.Web.Http.Route("metadata/updatedcat")]
        [System.Web.Http.HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        public System.Net.Http.HttpResponseMessage UpdateDcat()
        {
            var dcat = new DcatService().GenerateDcat();
            return new System.Net.Http.HttpResponseMessage()
            { Content = new System.Net.Http.StringContent(dcat.OuterXml, System.Text.Encoding.UTF8, "application/xml") };
        }
    }
}
