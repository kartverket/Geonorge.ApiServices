using Kartverket.Geonorge.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;

namespace Kartverket.Geonorge.Api.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class NmdcController : ApiController
    {
        private readonly INmdcService _nmdcService;

        public NmdcController(INmdcService nmdcService)
        {
            _nmdcService = nmdcService;
        }

        [Route("metadata/update-nmdc")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IHttpActionResult> UpdateMetadataWithNmdc()
        {
            await _nmdcService.UpdateMetadata();
            return StatusCode(HttpStatusCode.NoContent);
        }
    }
}
