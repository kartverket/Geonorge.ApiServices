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
            if (HttpContext.Current.Application["ServiceErrors"] != null)
                return (List<MetadataEntry>)HttpContext.Current.Application["ServiceErrors"];

            return null;

        }

        /// <summary>
        /// Run checking for problems in service capabilities
        /// </summary>
        [System.Web.Http.Route("metadata/checkforinvalid")]
        [System.Web.Http.HttpGet]
        public IHttpActionResult CheckServices()
        {
            HttpContext ctx = HttpContext.Current;
            Thread t = new Thread(new ThreadStart(() =>
            {
                HttpContext.Current = ctx;
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
    }
}
