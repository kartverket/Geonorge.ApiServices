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
    public class FeedController : ApiController
    {
        private readonly IFeedService _feedService;

        public FeedController(IFeedService feedService)
        {
            _feedService = feedService;
        }

        [Route("metadata/update-atomfeed")]
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IHttpActionResult> UpdateMetadataWithAtomFeeds()
        {
            await _feedService.UpdateMetadata();
            return StatusCode(HttpStatusCode.NoContent);
        }
    }
}
