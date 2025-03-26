using System.Collections.Generic;
using System.Reflection;
using System.Web.Configuration;
using System.Web.Http;
using System.Web.Mvc;
using Autofac;
using Autofac.Core.Activators.Reflection;
using Autofac.Integration.Mvc;
using Autofac.Integration.WebApi;
using Geonorge.AuthLib.NetFull;
using GeoNorgeAPI;
using Kartverket.Geonorge.Api.Services;
using Kartverket.Geonorge.Utilities.LogEntry;
using Kartverket.Geonorge.Utilities.Organization;

namespace Kartverket.Geonorge.Api
{
    public class DependencyConfig
    {
        public static IContainer Configure(ContainerBuilder builder)
        {
            //// must register dependencies in Kartverket.Geonorge.Utilities manually - consider making an autofac module 
            //builder.RegisterType<HttpClientFactory>().As<IHttpClientFactory>();

            //// auto registration of classes implementing an interface, e.g. MyClass as IMyClass
            //builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly()).AsImplementedInterfaces();

            //builder.RegisterApiControllers(Assembly.GetExecutingAssembly());

            //var container = builder.Build();

            //// dependency resolver for MVC
            //DependencyResolver.SetResolver(new AutofacDependencyResolver(container));

            //// dependency resolver for Web API
            //GlobalConfiguration.Configuration.DependencyResolver = new AutofacWebApiDependencyResolver(container);


            //test

            builder.RegisterControllers(typeof(MvcApplication).Assembly).PropertiesAutowired();
            builder.RegisterApiControllers(Assembly.GetExecutingAssembly()).PropertiesAutowired();

            builder.RegisterModule(new AutofacWebTypesModule());
            ConfigureAppDependencies(builder);
            var container = builder.Build();

            // dependency resolver for MVC
            DependencyResolver.SetResolver(new AutofacDependencyResolver(container));

            // dependency resolver for Web API
            GlobalConfiguration.Configuration.DependencyResolver = new AutofacWebApiDependencyResolver(container);

            return container;


        }

        // the order of component registration is significant. must wire up dependencies in other packages before types in this project.
        private static void ConfigureAppDependencies(ContainerBuilder builder)
        {
            builder.RegisterModule<GeonorgeAuthenticationModule>();
        }
    }
}