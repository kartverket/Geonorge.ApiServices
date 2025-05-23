﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Kartverket.Geonorge.Api
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute("SignIn", "SignIn", new { controller = "Home", action = "SignIn" });
            routes.MapRoute("SignOut", "SignOut", new { controller = "Home", action = "SignOut" });
            // authentication - openid connect 
            routes.MapRoute("OIDC-callback-signout", "signout-callback-oidc", new { controller = "Home", action = "SignOutCallback" });

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
