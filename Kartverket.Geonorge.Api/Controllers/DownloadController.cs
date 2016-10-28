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

        /// <summary>
        /// Check for problems in service capabilities
        /// </summary>
        [System.Web.Http.Route("metadata/serviceinvalid")]
        [System.Web.Http.HttpGet]
        public List<MetadataEntry> GetServices()
        {
            return new ServiceChecker().Check();
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
