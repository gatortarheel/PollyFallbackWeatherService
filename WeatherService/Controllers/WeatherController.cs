using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace WeatherService.Controllers
{
    [Route("[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WeatherController> _logger;
        public string StickySessionKey = "UniqueSessionKey";
        public string StickySessionClients = "StickySessionClients";
        public List<URLInstance> _urlInstances = new List<URLInstance>();
        private readonly IMemoryCache _memoryCache;

        public WeatherController(ILogger<WeatherController> logger, IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _memoryCache = memoryCache;
            _memoryCache.Set("test", DateTime.Now, TimeSpan.FromDays(1));

            _urlInstances.Add(new URLInstance
            {
                Id = 1,
                Failure = 0,
                Name = "TemperatureServiceAlpha",
                Success = 0,
                URL = "http://localhost:6001/",
                Path = @"/WeatherForecast/GetTemperatureAlpha"
            });

            _urlInstances.Add(new URLInstance
            {
                Id = 2,
                Failure = 0,
                Name = "TemperatureServiceBeta",
                Success = 0,
                URL = "http://localhost:6001/", ///WeatherForecast/GetTemperatureBeta
                Path = @"/WeatherForecast/GetTemperatureBeta"
            });

            _urlInstances.Add(new URLInstance
            {
                Id = 3,
                Failure = 0,
                Name = "TemperatureServiceGamma",
                Success = 0,
                URL = "http://localhost:6001/",
                Path = @"/WeatherForecast/GetTemperatureGamma"
            });
        }

        /*[HttpGet("{locationId}")]
        public async Task<IActionResult> Get(int locationId)
        {
            //var httpClient = _httpClientFactory.CreateClient("TemperatureServiceAlpha");
            //HttpResponseMessage httpResponseMessage = await httpClient.GetAsync($"temperature/{locationId}");

            //var httpClientBeta = _httpClientFactory.CreateClient("TemperatureServiceBeta");
            //HttpResponseMessage httpResponseMessageBeta = await httpClient.GetAsync($"temperature/{locationId}");

            //if (httpResponseMessage.IsSuccessStatusCode)
            //{
            //    var temperature = await httpResponseMessage.Content.ReadAsStringAsync();
            //    return Ok(temperature);
            // }

            var result = await GetTheTemperatureAsync(locationId);
            return result;

            //return StatusCode((int)httpResponseMessage.StatusCode, httpResponseMessage.Content.ReadAsStringAsync());
        }*/

        [HttpGet("{locationId}")]
        public async Task<IActionResult> GetTheTemperatureAsync(int locationId)
        {
            _logger.LogInformation("------------------starting GetTheTemperatureAsync --- switching off and on randomly to see if .NET picks it up. ");

            if (HttpContext.Session.GetInt32(StickySessionKey) == null)
            {
                _logger.LogInformation("------------------StickySessionKey is null ");
                HttpContext.Session.SetInt32(StickySessionKey, 0);  // arrays are 0 based //
            }

            //logging           

 
            try
            {
                var attempts = 0;
                var x = HttpContext.Session.GetInt32(StickySessionKey) ?? 0;

                while (attempts < _urlInstances.Count)
                {
                    _logger.LogInformation($"{StickySessionKey}: {HttpContext.Session.GetInt32(StickySessionKey)}");

                    var client = _urlInstances[x];
                    _logger.LogInformation($"attempt {attempts} {client.Name}");
                    var httpClient = _httpClientFactory.CreateClient(client.Name);

                    HttpResponseMessage httpResponseMessage = await httpClient.GetAsync($"{client.Path}/{locationId}");
                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        client.Success++;
                        HttpContext.Session.SetInt32(StickySessionKey, x);
                        _logger.LogInformation($"StickySessionKey set to: {x} {client.Name}");
                        _logger.LogInformation($"success: {client.Name}");
                        return StatusCode((int)httpResponseMessage.StatusCode, httpResponseMessage.Content.ReadAsStringAsync());
                    }
                    else
                    {
                        client.Failure++;
                        _logger.LogInformation($"*** Failure Count : {client.Name} : {client.Failure}");
                        x++;
                        if (x > 2)
                        {
                            x = 0;
                        }
                    }
                    attempts++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical($"Exception :{ ex.Message} | {ex.StackTrace}");
            }
            /*
            
            else
            {
                // get the next client Beta for now // 
                var httpClientBeta = _httpClientFactory.CreateClient("TemperatureServiceBeta"); //WeatherForecast / GetTemperatureBeta
                httpResponseMessage = await httpClientBeta.GetAsync($"/WeatherForecast/GetTemperatureBeta/{locationId}");
                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    HttpContext.Session.SetString(StickySessionKey, "TemperatureServiceBeta"); 
                }
            }*/
            //
            return StatusCode((int)HttpStatusCode.InternalServerError, "Something went wrong when getting the temperature.");
            // return StatusCode((int)httpResponseMessage.StatusCode, httpResponseMessage.Content.ReadAsStringAsync());
        }


        /*static URLInstance GetNextURL()
        {
            
            yield return "TemperatureServiceAlpha";
            yield return "TemperatureServiceBeta";
            yield return "TemperatureServiceGamma";
        }*/

    }
}
