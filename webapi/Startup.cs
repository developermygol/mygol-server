using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using webapi.Models.Db;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using contracts;
using storage.disk;
using Microsoft.Extensions.FileProviders;
using notification.email;
using Dapper;
using Serilog;

namespace webapi
{
    public class Startup
    {
        public Startup(IConfiguration configuration, ILogger<PerfLog> perfLogger)
        {
            Configuration = configuration;
            ConfigurePerfLog(perfLogger);

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                //.Enrich.With<RequestDomainEnricher>()
                .CreateLogger();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc()
                .AddJsonOptions(opt =>
                {
                    opt.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
                });

            services.AddCors();

            var appSection = Configuration.GetSection("App");
            var appConfig = new Config();
            services.Configure<Config>(appSection);
            appSection.Bind(appConfig);

            var dbSection = Configuration.GetSection("db");
            services.Configure<PostgresqlConfig>(dbSection);

            //var dbConfig = new PostgresqlConfig();
            //dbSection.Bind(dbConfig);

            services.Configure<CorsConfig>(Configuration.GetSection("cors"));
            //services.Configure<Organization>(Configuration.GetSection("organization"));

            ConfigureAuth(services);

            // Disk storage
            var diskStorageSection = Configuration.GetSection("storage.disk");
            var diskStorageConfig = new DiskStorageProviderConfig();
            diskStorageSection.Bind(diskStorageConfig);

            services.Configure<DiskStorageProviderConfig>(diskStorageSection);
            services.AddSingleton(typeof(IStorageProvider), new DiskStorageProvider(diskStorageConfig));

            services.AddSingleton(typeof(NotificationManager), new NotificationManager(appConfig));


            OrganizationManager.Initialize(OrganizationManager.LoadOrganizationsFromFile("organizations.json"));
        }

        private void ConfigureAuth(IServiceCollection services)
        {
            var tokenConfig = new TokenAuthConfig();
            var tokenConfigFromSettings = Configuration.GetSection("tokenConfig");
            tokenConfigFromSettings.Bind(tokenConfig);

            var tokenManager = new AuthTokenManager(tokenConfig);
            services.AddSingleton(tokenManager);

            services.AddAuthentication(options => {
                options.DefaultAuthenticateScheme = "JwtBearer";
                options.DefaultChallengeScheme = "JwtBearer";
            })
            .AddJwtBearer("JwtBearer", jwtBearerOptions =>
            {
                jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = tokenManager.Key,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true, //validate the expiration and not before values in the token
                    ClockSkew = TimeSpan.FromMinutes(5) //5 minute tolerance for the expiration date
                };
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, 
            IOptions<CorsConfig> corsOptions, IOptions<DiskStorageProviderConfig> diskConfig)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(diskConfig.Value.UploadPath),
                    RequestPath = "/upload"
                });
            }

            if (corsOptions?.Value != null)
            {
                var origins = corsOptions.Value.Origins;

                app.UseCors(builder => builder
                        .WithOrigins(origins)
                        // .WithOrigins("*")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                );
            }

            app.UseAuthentication();

            app.UseMvc();
        }

        private void ConfigurePerfLog(ILogger<PerfLog> logger)
        {
            PerfLog.Logger = msg =>
            {
                logger.LogInformation(msg);
            };
        }
    }
}
