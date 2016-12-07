using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http.ExceptionHandling;

namespace Kartverket.Geonorge.Api
{
    public class TraceExceptionLogger : ExceptionLogger
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TraceExceptionLogger));

        public override void Log(ExceptionLoggerContext context)
        {
            string postData = string.Empty;

            try
            {
                var data = ((System.Web.Http.ApiController)
          context.ExceptionContext.ControllerContext.Controller)
          .ActionContext.ActionArguments.Values;

                postData = JsonConvert.SerializeObject(data);
            }
            catch
            {       
            }
            Logger.Error(context.ExceptionContext.Exception);
        }
    }
}