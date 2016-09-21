using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kartverket.Geonorge.Api.Services;

namespace Kartverket.Geonorge.Api.Controllers
{
    public class DownloadController : ApiController
    {
        /// <summary>
        /// Check for problems in download distribution
        /// </summary>
        [System.Web.Http.Route("metadata/downloadinvalid")]
        [System.Web.Http.HttpGet]
        public List<MetadataEntry> GetDownloads()
        {
            return new DownloadService().CheckDownload();
        }
    }
}
