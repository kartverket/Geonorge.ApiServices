using Geonorge.ApiServices.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Xml;

namespace Geonorge.ApiServices.Controllers
{
    public class DcatController : ControllerBase
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
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "dcat", "geonorge_dcat.rdf");
            doc.Load(filePath);
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
