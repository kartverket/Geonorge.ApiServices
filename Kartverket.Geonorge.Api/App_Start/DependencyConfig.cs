using System.Reflection;
using System.Web.Http;
using System.Web.Mvc;
using Autofac;
using Autofac.Integration.Mvc;
using Autofac.Integration.WebApi;
using Kartverket.Geonorge.Utilities.Organization;

namespace Kartverket.Geonorge.Api
{
    public static class DependencyConfig
    {
        public static void Configure(ContainerBuilder builder)
        {
            // must register dependencies in Kartverket.Geonorge.Utilities manually - consider making an autofac module 
            builder.RegisterType<HttpClientFactory>().As<IHttpClientFactory>();

            // auto registration of classes implementing an interface, e.g. MyClass as IMyClass
            builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly()).AsImplementedInterfaces();

            builder.RegisterApiControllers(Assembly.GetExecutingAssembly());

            var container = builder.Build();

            // dependency resolver for MVC
            DependencyResolver.SetResolver(new AutofacDependencyResolver(container));

            // dependency resolver for Web API
            GlobalConfiguration.Configuration.DependencyResolver = new AutofacWebApiDependencyResolver(container);
        }
    }
}