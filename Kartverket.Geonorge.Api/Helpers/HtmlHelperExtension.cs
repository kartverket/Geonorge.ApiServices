using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;

namespace Kartverket.Geonorge.Api.Helpers
{
    public static class HtmlHelperExtension
    {

        public static string ApplicationVersionNumber(this HtmlHelper helper)
        {
            string versionNumber = WebConfigurationManager.AppSettings["BuildVersionNumber"];
            return versionNumber;
        }

        public static string GeonorgeUrl(this HtmlHelper helper)
        {
            return WebConfigurationManager.AppSettings["GeonorgeUrl"];
        }
        public static string KartkatalogenUrl(this HtmlHelper helper)
        {
            return WebConfigurationManager.AppSettings["KartkatalogenUrl"];
        }
        public static string EnvironmentName(this HtmlHelper helper)
        {
            return WebConfigurationManager.AppSettings["EnvironmentName"];
        }
        public static string WebmasterEmail(this HtmlHelper helper)
        {
            return WebConfigurationManager.AppSettings["WebmasterEmail"];
        }

    }
}