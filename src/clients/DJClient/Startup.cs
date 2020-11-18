using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Risk.Shared;
using System.Net.Http.Json;
using System.Net;
using System.Net.Sockets;

namespace DJClient
{
    public class Startup
    {

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddHttpClient();
            services.AddSingleton<IPlayer>(new ClientPlayer { Name="DJ"});
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHttpClientFactory httpClientFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }


            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });


            var server = Configuration["ServerName"];
            var httpClient = httpClientFactory.CreateClient();
            //var clientBaseAddress = app.ServerFeatures.Get<IServerAddressesFeature>().Addresses.First();
            var clientBaseAddress = Configuration["ClientCallbackAddress"];


            var playerName = Configuration["PlayerName"];

            var joinResponse = JoinServer(httpClient, server, clientBaseAddress, playerName);
            //ToDo: Make this somehow accessible as a singelton
            //var player = new ClientPlayer { Name = playerName, Token = joinResponse.Token };

            

        }

        private string getLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());


            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        private async Task JoinServer(HttpClient httpClient, string serverName, string clientBaseAddress, string playerName)
        {
            var joinRequest = new JoinRequest { CallbackBaseAddress = clientBaseAddress,  Name = playerName};

            var response = await httpClient.PostAsJsonAsync($"{serverName}/join", joinRequest);

             //await response.Content.ReadFromJsonAsync<JoinResponse>();
           
        }
    }
}
