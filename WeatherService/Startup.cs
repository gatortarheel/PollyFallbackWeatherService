using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Net;
using Polly.Timeout;
using Serilog;

namespace WeatherService
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            Log.Logger = new LoggerConfiguration()
                   .ReadFrom.Configuration(configuration)
                   .CreateLogger();
        }

        public void ConfigureServices(IServiceCollection services)
        {

            // require sticky session // other options ? 
            services.AddDistributedMemoryCache();

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromSeconds(10);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddSwaggerGen();


            // https://github.com/App-vNext/Polly/wiki/Polly-and-HttpClientFactory
            var timeoutPolicySeconds3 = Policy.TimeoutAsync<HttpResponseMessage>(3);


            IAsyncPolicy<HttpResponseMessage> httpWait_1Sec_AndRetry_1x_Policy = Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                                                                              .WaitAndRetryAsync(1, retryAttempt => TimeSpan.FromSeconds(retryAttempt),
                                                                                (result, span, retryCount, ctx) => Console.WriteLine($"Retrying({retryCount})..."));

            IAsyncPolicy<HttpResponseMessage> httpWaitAndRetryPolicy = Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                                                                              .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(retryAttempt),
                                                                                (result, span, retryCount, ctx) => Console.WriteLine($"Retrying({retryCount})..."));

            /*
                The fallback action method returns a HttpResponseMessage that is passed back to the original caller in place of the HttpResponseMessage 
                received from the Temperature Service. In this example the action to take is to send a dummy message to an admin, but it could be anything you want.
             */
            IAsyncPolicy<HttpResponseMessage> fallbackPolicy = Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                                                                     .FallbackAsync(FallbackAction, OnFallbackAsync);

            // combine the policies
            IAsyncPolicy<HttpResponseMessage> wrapOfRetryAndFallback = Policy.WrapAsync(fallbackPolicy, httpWaitAndRetryPolicy);


            services.AddHttpClient("TemperatureService", client =>
            {
                client.BaseAddress = new Uri("http://localhost:6001/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                //client.Timeout = TimeSpan.FromSeconds(5);
            });//.AddPolicyHandler(timeoutPolicySeconds3);


            services.AddHttpClient("TemperatureServiceAlpha", client =>
            {
                client.BaseAddress = new Uri("http://localhost:6001/"); // TODO: actual paths  //
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                //client.Timeout = TimeSpan.FromSeconds(5);
            }).AddPolicyHandler(timeoutPolicySeconds3); // TODO: 3 second timeout  //

            services.AddHttpClient("TemperatureServiceBeta", client =>
            {
                client.BaseAddress = new Uri("http://localhost:6001/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddPolicyHandler(timeoutPolicySeconds3); // TODO: 3 second timeout //

            services.AddHttpClient("TemperatureServiceGamma", client =>
            {
                client.BaseAddress = new Uri("http://localhost:6001/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddPolicyHandler(timeoutPolicySeconds3); // TODO: 3 second timeout //

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            app.UseSession();
            app.UseAuthorization();
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                options.RoutePrefix = string.Empty;
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private Task OnFallbackAsync(DelegateResult<HttpResponseMessage> response, Context context)
        {
            Console.WriteLine("About to call the fallback action. This is a good place to do some logging");
            return Task.CompletedTask;
        }

        private Task<HttpResponseMessage> FallbackAction(DelegateResult<HttpResponseMessage> responseToFailedRequest, Context context, CancellationToken cancellationToken)
        {
            Console.WriteLine("Fallback action is executing");
            HttpResponseMessage httpResponseMessage = new HttpResponseMessage(responseToFailedRequest.Result.StatusCode)
            {
                Content = new StringContent($"The fallback executed, the original error was {responseToFailedRequest.Result.ReasonPhrase}")
            };
            return Task.FromResult(httpResponseMessage);
            
        }
    }
}
