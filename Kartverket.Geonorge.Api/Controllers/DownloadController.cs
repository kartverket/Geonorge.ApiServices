using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kartverket.Geonorge.Api.Services;
using System.Web.Http.Description;
using System.Threading;
using System.Web;
using System.Runtime.Caching;

namespace Kartverket.Geonorge.Api.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
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

        /// <summary>
        /// Get problems in service capabilities
        /// </summary>
        [System.Web.Http.Route("metadata/serviceinvalid")]
        [System.Web.Http.HttpGet]
        public List<MetadataEntry>  GetServices()
        {
            MemoryCache memoryCache = MemoryCache.Default;
            var cache = memoryCache.Get("ServiceErrors") as List<MetadataEntry>;
            if (cache != null)
                return cache;
            else
                CheckServices();

            return null;

        }

        /// <summary>
        /// Run checking for problems in service capabilities
        /// </summary>
        [System.Web.Http.Route("metadata/runservicecheck")]
        [System.Web.Http.HttpGet]
        public IHttpActionResult CheckServices()
        {
            Thread t = new Thread(new ThreadStart(() =>
            {
                new ServiceChecker().Check();
            }));
            t.Start();

            return Ok();
        }

        /// <summary>
        /// Check for AccessConstraint mismatch between kartkatalog and download
        /// </summary>
        [System.Web.Http.Route("metadata/constraintproblems")]
        [System.Web.Http.HttpGet]
        public List<MetadataEntry> GetConstraints()
        {
            return new ConstraintChecker().Check();
        }

        /// <summary>
        /// Check for mismatch between kartkatalog search index and geonetwork
        /// </summary>
        [System.Web.Http.Route("metadata/searchindexproblems")]
        [System.Web.Http.HttpGet]
        public List<MetadataEntry> SearchIndexProblems()
        {

            MemoryCache memoryCache = MemoryCache.Default;
            var cache = memoryCache.Get("MetadataErrors") as List<MetadataEntry>;
            if (cache != null)
                return cache;
            else
                new MetadataChecker().Check();

            return null;
        }

    }
}
