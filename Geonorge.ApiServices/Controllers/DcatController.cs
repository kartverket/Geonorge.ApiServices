using Geonorge.ApiServices.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Xml;

namespace Geonorge.ApiServices.Controllers
{
    public class DcatController : ControllerBase
    {
        private readonly IDcatService _dcatService;
        private readonly IConfiguration _settings;

        public DcatController(IDcatService dcatService, IConfiguration settings)
        {
            _dcatService = dcatService;
            _settings = settings;
        }

        /// <summary>
        ///     Metadata catalogue in dcat format
        /// </summary>
        [Route("metadata/dcat")]
        [HttpGet]
        public IActionResult GetDcat()
        {
            var doc = new XmlDocument();
            var filePath = _settings["DcatFolder"] + "\\geonorge_dcat.rdf";
            doc.Load(filePath);
            return Content(doc.OuterXml, "application/rdf+xml", Encoding.UTF8);
        }

        /// <summary>
        ///     Update dcat file from metadata csw server
        /// </summary>
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
