using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(Kartverket.Geonorge.Api.Startup))]

namespace Kartverket.Geonorge.Api
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
        }
    }
}
