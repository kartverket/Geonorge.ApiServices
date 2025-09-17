using Geonorge.ApiServices.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;

namespace Geonorge.ApiServices.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [ApiController]
    public class FeedController : ControllerBase
    {
        private readonly IFeedService _feedService;

        public FeedController(IFeedService feedService)
        {
            _feedService = feedService;
        }

        /// <summary>
        ///     Update metadata distributions with atom feeds
        /// </summary>
        [Route("metadata/update-atomfeed")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> UpdateMetadataWithAtomFeeds()
        {
            await _feedService.UpdateMetadata();
            return StatusCode((int)HttpStatusCode.NoContent);
        }
    }
}
