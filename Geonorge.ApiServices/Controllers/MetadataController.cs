using Kartverket.Geonorge.Api.Models;
using Kartverket.Geonorge.Api.Services;
using Kartverket.Geonorge.Download;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Kartverket.Geonorge.Api.Controllers
{
    //[ApiExplorerSettings(IgnoreApi = true)]
    public class MetadataController : ControllerBase
    {
        private readonly IMetadataService _metadataService;

        public MetadataController(IMetadataService metadataService)
        {
            _metadataService = metadataService;
        }


        [Route("metadata-update-fair/{uuid}/{result}")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> UpdateFair(string uuid, string result)
        {
            await _metadataService.UpdateMetadataFair(uuid, result);
            return StatusCode((int)HttpStatusCode.OK, uuid);
        }

        [Authorize(Roles = AuthConfig.DatasetProviderRole)]
        [Route("metadata-publication")]
        [HttpPost]
        //[ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> InsertMetadata(MetadataModel metadata)
        {
            var uuid = await _metadataService.InsertMetadata(metadata);
            return StatusCode((int)HttpStatusCode.Created, uuid);
        }

        [Authorize(Roles = AuthConfig.DatasetProviderRole)]
        [Route("metadata-publication/{uuid}")]
        [HttpPut]
        //[ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> UpdateMetadata(string uuid, MetadataModel model)
        {
            await _metadataService.UpdateMetadata(uuid, model);
            return StatusCode((int)HttpStatusCode.OK, uuid);
        }

        [Authorize(Roles = AuthConfig.DatasetProviderRole)]
        [Route("metadata-publication/{uuid}")]
        [HttpDelete]
        //[ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> DeleteMetadata(string uuid)
        {
            await _metadataService.DeleteMetadata(uuid);
            return StatusCode((int)HttpStatusCode.Gone, uuid);
        }
    }
}
