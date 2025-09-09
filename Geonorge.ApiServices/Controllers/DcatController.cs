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
        public IActionResult GetDcat()
        {
            var doc = new XmlDocument();
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "dcat", "geonorge_dcat.rdf");
            doc.Load(filePath);
            return Content(doc.OuterXml, "application/rdf+xml", Encoding.UTF8);
        }

        [Route("metadata/updatedcat")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult UpdateDcat()
        {
            var dcat = _dcatService.GenerateDcat();
            return Content(dcat.OuterXml, "application/rdf+xml", Encoding.UTF8);
        }
    }
}
