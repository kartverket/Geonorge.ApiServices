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
        ///     Metadata catalogue over datasets in dcat format
        /// </summary>
        [Route("metadata/dcat")]
        [HttpGet]
        public IActionResult GetDcat()
        {
            var doc = new XmlDocument();
            var filePath = _settings["DcatFolder"] + "\\geonorge_dcat.rdf";
            doc.Load(filePath);
            var fileName = "geonorge_dcat.rdf";
            var fileBytes = Encoding.UTF8.GetBytes(doc.OuterXml);
            return File(fileBytes, "application/rdf+xml", fileName);
        }

        /// <summary>
        ///     Metadata catalogue services in dcat format
        /// </summary>
        [Route("metadata/dcat-services")]
        [HttpGet]
        public IActionResult GetDcatServices()
        {
            var doc = new XmlDocument();
            var filePath = _settings["DcatFolder"] + "\\geonorge_dcat_service.rdf";
            doc.Load(filePath);
            var fileName = "geonorge_dcat_services.rdf";
            var fileBytes = Encoding.UTF8.GetBytes(doc.OuterXml);
            return File(fileBytes, "application/rdf+xml", fileName);
        }

        /// <summary>
        ///     Update dcat file from metadata csw server
        /// </summary>
        [Route("metadata/updatedcat")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult UpdateDcat()
        {
            _dcatService.GenerateDcat();
            return Ok();
        }
    }
}
