using Geonorge.ApiServices.Services;
using Kartverket.Geonorge.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;
using System;
using System.Net;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Setup Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Filter.ByExcluding(logEvent =>
        logEvent.RenderMessage().Contains("AuthenticationScheme: \"Basic\" was not authenticated"))
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("basic", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "basic",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Basic Authentication header"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "basic"
                }
            },
            new List<string>()
        }
    });

    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Geonorge diverse APIer",
        Description = "Diverse apier for metadata og dcat",
        Contact = new OpenApiContact
        {
            Name = "Geonorge",
            Url = new Uri("https://www.geonorge.no/aktuelt/om-geonorge/")
        },
    });

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));

});

builder.Services.AddHttpClient();
builder.Services.AddScoped<IMetadataService, MetadataService>();
builder.Services.AddScoped<IDcatService, DcatService>();
builder.Services.AddScoped<IFeedService, FeedService>();
builder.Services.AddScoped<IAtomFeedParser, AtomFeedParser>();
ConfigureProxy(builder.Configuration);

builder.Services.AddAuthentication("Basic")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("Basic", null);

builder.Services.AddAuthorization();

var app = builder.Build();

 app.UseSwagger();
 app.UseSwaggerUI(c =>
 {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Geonorge diverse APIer");
    c.RoutePrefix = string.Empty;
    c.InjectStylesheet("custom.css");
 });

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static void ConfigureProxy(IConfiguration settings)
{
    var urlProxy = settings.GetValue<string>("UrlProxy");

    if (!string.IsNullOrWhiteSpace(urlProxy))
    {
        var proxy = new WebProxy(urlProxy)
        {
            Credentials = CredentialCache.DefaultCredentials
        };

        WebRequest.DefaultWebProxy = proxy;
        HttpClient.DefaultProxy = proxy;
    }
}
