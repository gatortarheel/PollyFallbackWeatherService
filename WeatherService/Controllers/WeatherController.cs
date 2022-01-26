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
        //  https://docs.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-6.0#use-setsize-size-and-sizelimit-to-limit-cache-size

        public WeatherController(ILogger<WeatherController> logger, IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _memoryCache = memoryCache;
            // idea put the urlInstances in the cache, which we can set to expire to restart the loop //
            if (_memoryCache.Get(StickySessionClients) == null)
            {
                LoadURLInstancesIntoTheCache();
            }
        }

        private void LoadURLInstancesIntoTheCache()
        {
            if(_memoryCache.Get(StickySessionClients) == null)
            {
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
                    URL = "http://localhost:6001/",
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
                _memoryCache.Set(StickySessionClients, _urlInstances);
            }
        }

        private List<URLInstance> GetURLInstancesFromCache()
        {
            return _memoryCache.Get<List<URLInstance>>(StickySessionClients);
        }

        private void UpdateURLInstanceCache()
        {
            _memoryCache.Set(StickySessionClients, _urlInstances);
        }

        [HttpGet()]
        public List<URLInstance> GetTheReport()
        {
            if (_memoryCache.Get(StickySessionClients) == null)
            {
                _memoryCache.Set(StickySessionClients, _urlInstances);
            }
            return _memoryCache.Get<List<URLInstance>>(StickySessionClients);
        }



        [HttpGet("{locationId}")]
        public async Task<IActionResult> GetTheTemperatureAsync(int locationId)
        {
            if(_memoryCache.Get(StickySessionKey) == null)
            {
                _memoryCache.Set(StickySessionKey, 0);
            }

            if(_urlInstances.Count == 0)
            {
                _urlInstances = GetURLInstancesFromCache();
            }
            // cache is application level 

            try
            {
                var attempts = 0;
                var activeEndpoint = _memoryCache.Get<int>(StickySessionKey);

                while (attempts < _urlInstances.Count)
                {
                    var client = _urlInstances[activeEndpoint];
                    var httpClient = _httpClientFactory.CreateClient(client.Name);

                    _logger.LogInformation($" Cache Key {StickySessionKey} | {activeEndpoint}");
                    _logger.LogInformation($"{ client.Name} | Attempt | {attempts} ");

                    try
                    {
                        HttpResponseMessage httpResponseMessage = await httpClient.GetAsync($"{client.Path}/{locationId}");
                        if (httpResponseMessage.IsSuccessStatusCode)
                        {
                            client.Success++;
                            UpdateURLInstanceCache();
                            _memoryCache.Set(StickySessionKey, activeEndpoint); 
                            _logger.LogInformation($"{client.Name} | Success | {httpResponseMessage.StatusCode} ");
                            return StatusCode((int)httpResponseMessage.StatusCode, httpResponseMessage.Content.ReadAsStringAsync());
                        }
                        else
                        {
                            _logger.LogInformation($"{client.Name} | Not successful | {httpResponseMessage.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"{client.Name} | Exception {ex.Message} | {ex.StackTrace}");
                    }

                    // move to the next item in the client array //
                    activeEndpoint++;
                    if (activeEndpoint > 2)
                    {
                        activeEndpoint = 0;
                    }
                    client.Failure++;
                    UpdateURLInstanceCache();
                    attempts++;

                    if(attempts > 3)
                    {
                        return StatusCode((int)HttpStatusCode.InternalServerError, "Cloud Instance not responding, use local data.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception | { ex.Message} | {ex.StackTrace}");
            }
           
            return StatusCode((int)HttpStatusCode.InternalServerError, "Cloud Instance not responding, use local data.");
        }
    }
}
