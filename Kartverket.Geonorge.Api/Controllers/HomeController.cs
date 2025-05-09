using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OpenIdConnect;
using Microsoft.Owin.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;

namespace Kartverket.Geonorge.Api.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Diverse APIer";

            return View();
        }
        public void SignIn()
        {
            var redirectUrl = Url.Action(nameof(HomeController.Index), "Home");
            HttpContext.GetOwinContext().Authentication.Challenge(new AuthenticationProperties { RedirectUri = redirectUrl },
                OpenIdConnectAuthenticationDefaults.AuthenticationType);
        }

        public void SignOut()
        {
            // Change loggedIn cookie
            var cookie = Request.Cookies["_loggedIn"];

            if (cookie != null)
            {
                cookie.Value = "false";   // update cookie value
                //cookie.SameSite = SameSiteMode.Lax;
                if (!Request.IsLocal)
                    cookie.Domain = ".geonorge.no";

                Response.Cookies.Set(cookie);
            }
            else
            {
                cookie = new HttpCookie("_loggedIn");
                cookie.Value = "false";
                //cookie.SameSite = SameSiteMode.Lax;

                if (!Request.IsLocal)
                    cookie.Domain = ".geonorge.no";

                Response.Cookies.Add(cookie);
            }

            var redirectUri = WebConfigurationManager.AppSettings["GeoID:PostLogoutRedirectUri"];
            HttpContext.GetOwinContext().Authentication.SignOut(
                new AuthenticationProperties { RedirectUri = redirectUri },
                OpenIdConnectAuthenticationDefaults.AuthenticationType,
                CookieAuthenticationDefaults.AuthenticationType);
        }

        /// <summary>
        /// This is the action responding to /signout-callback-oidc route after logout at the identity provider
        /// </summary>
        /// <returns></returns>
        public ActionResult SignOutCallback()
        {
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }
    }
}
