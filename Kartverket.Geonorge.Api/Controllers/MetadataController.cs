using Kartverket.Geonorge.Api.Models;
using Kartverket.Geonorge.Api.Services;
using Kartverket.Geonorge.Download;
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
    [Authorize(Roles = AuthConfig.DatasetProviderRole)]
    public class MetadataController : ApiController
    {
        private readonly IMetadataService _metadataService;

        public MetadataController(IMetadataService metadataService)
        {
            _metadataService = metadataService;
        }

        [Route("metadata")]
        [HttpPost]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IHttpActionResult> InsertMetadata(MetadataCreate metadata)
        {
            var uuid = await _metadataService.InsertMetadata(metadata);
            return Content(HttpStatusCode.Created, uuid);
        }

        [Route("metadata/{uuid}")]
        [HttpDelete]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IHttpActionResult> DeeleteMetadata(string uuid)
        {
            await _metadataService.DeleteMetadata(uuid);
            return Content(HttpStatusCode.Gone, uuid);
        }
    }
}
