// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Insight.DataService
{
    using System;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using System.Web.Mvc;

    using System.Data.Entity;
    using global::Iot.Common;

    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("Config/appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"Config/appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            this.Configuration = builder.Build();

            ValueProviderFactories.Factories.Add(new JsonValueProviderFactory());

            Console.WriteLine("On Launchpad.Iot.Insight.WebService Startup - Configuration has Sections");
            Console.WriteLine("=============================================================================");
            foreach (var section in this.Configuration.GetChildren())
            {
                ManageAppSettings.AddUpdateAppSettings(section.Key, section.Value);

                string value = section.Value;

                if (section.Key.ToLower().Contains("password"))
                {
                    value = "****************";
                }

                Console.WriteLine("Key=" + section.Key + " Path=" + section.Path + " Value=" + value + " To String=" + section.ToString());
            }
            Console.WriteLine("=============================================================================");

        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // web security 
            services.AddCors();

            // Add framework services.
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(this.Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseMvc();
        }
    }
}
