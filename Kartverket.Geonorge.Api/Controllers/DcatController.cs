using System.Net.Http;
using System.Text;
using System.Web;
using System.Web.Http;
using System.Web.Http.Description;
using System.Xml;
using Kartverket.Geonorge.Api.Services;

namespace Kartverket.Geonorge.Api.Controllers
{
    public class DcatController : ApiController
    {
        private readonly IDcatService _dcatService;

        public DcatController(IDcatService dcatService)
        {
            _dcatService = dcatService;
        }

        /// <summary>
        ///     Catalogue in dcat format
        /// </summary>
        [Route("metadata/dcat")]
        [HttpGet]
        public HttpResponseMessage GetDcat()
        {
            var doc = new XmlDocument();
            doc.Load(HttpContext.Current.Request.MapPath("~\\dcat\\geonorge_dcat.rdf"));
            return new HttpResponseMessage
            {
                Content = new StringContent(doc.OuterXml, Encoding.UTF8, "application/rdf+xml")
            };
        }

        [Route("metadata/updatedcat")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        public HttpResponseMessage UpdateDcat()
        {
            var dcat = _dcatService.GenerateDcat();
            return new HttpResponseMessage
            {
                Content = new StringContent(dcat.OuterXml, Encoding.UTF8, "application/rdf+xml")
            };
        }
    }
}