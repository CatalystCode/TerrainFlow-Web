using System;
using System.Configuration;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace TerrainFlow
{
    public partial class Startup
    {
        public Startup(IHostingEnvironment env, Microsoft.Extensions.PlatformAbstractions.IApplicationEnvironment appEnv)
        {
            // Setup configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(appEnv.ApplicationBasePath)                
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }                       

        public IConfigurationRoot Configuration { get; set; }

        // This method gets called by the runtime.
        public void ConfigureServices(IServiceCollection services)
        {
            // Cookie Login
            services.AddAuthentication(options => options.SignInScheme = "Cookie");

            // Add MVC services to the services container.
            services.AddMvc();
            services.AddMvcDnx();

            // Uncomment the following line to add Web API services which makes it easier to port Web API 2 controllers.
            // You will also need to add the Microsoft.AspNet.Mvc.WebApiCompatShim package to the 'dependencies' section of project.json.
            // services.AddWebApiConventions();

            services.AddSingleton<IConfiguration>(Configuration);
        }

        // Configure is called after ConfigureServices is called.
        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();
            loggerFactory.AddDebug();

            // Add the platform handler to the request pipeline.
            app.UseIISPlatformHandler();

            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationScheme = "Cookie",
                AutomaticAuthenticate = true,
                AutomaticChallenge = true,
                LoginPath = new PathString("/signin")
            });

            var microsoftOptions = new MicrosoftAccountOptions
            {
                ClientId = Configuration["MICROSOFT_CLIENT_ID"],
                ClientSecret = Configuration["MICROSOFT_CLIENT_SECRET"],
                SignInScheme = "Cookie"
            };

            microsoftOptions.Scope.Add("wl.emails");

            app.UseMicrosoftAccountAuthentication(microsoftOptions);         

            app.UseGoogleAuthentication(new GoogleOptions
            {
                ClientId = Configuration["GOOGLE_CLIENT_ID"],
                ClientSecret = Configuration["GOOGLE_CLIENT_SECRET"],
                SignInScheme = "Cookie"
            });

            app.UseFacebookAuthentication(new FacebookOptions
            {
                ClientId = Configuration["FACEBOOK_CLIENT_ID"],
                ClientSecret = Configuration["FACEBOOK_CLIENT_SECRET"],
                SignInScheme = "Cookie"
            });


            // Add static files to the request pipeline.
            app.UseStaticFiles();

            // Add MVC to the request pipeline.
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");

                // Uncomment the following line to add a route for porting Web API 2 controllers.
                // routes.MapWebApiRoute("DefaultApi", "api/{controller}/{id?}");
            });
        }

        public static void Main(string[] args)
        {
            //WebApplication.Run<Startup>(args);

            var host = new WebHostBuilder()
            .UseDefaultConfiguration(args)
            .UseIISPlatformHandlerUrl()
            .UseServer("Microsoft.AspNetCore.Server.Kestrel")
            .UseStartup<Startup>()
            .Build();

            host.Run();
        }
    }
}
