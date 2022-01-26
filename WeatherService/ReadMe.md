# Round Robin with Multiple Endpoints
## Goals: 
- Set up a way to gracefully and quickly fail on endpoints that need to be load balanced, but have not been set up yet.
- Keep using an active endpoint for subsequent calls.
- If an endpoint is down or doesn't respond within a time frame, move on to the next one.
- If all endpoints fail, tell the calling API to use the data it has.
----

## Weather API 
Weather API is the source of the API calls.  

For this test, there are three instances.  The only parameter is a locationId, which is used to make the end points break or slow down.
Each returns its name – Alpha | Beta | Gamma.
The name is returned prov
![alt text](weatherapi1.png)


- Alpha is programmed to be slow – it has a thread sleep command that makes it sleep for locationId * 1000 ms.  Also, if the locationId is **5** – it breaks.

 ```CSharp
 [HttpGet]
        [Route("GetTemperatureAlpha/{locationId}")]
        public ActionResult GetTemperatureAlpha(int locationId)
        {
            Thread.Sleep(locationId * 1000);
            // fail based on locationId //
            if (locationId == 5)
            {
                _logger.LogInformation("Failure: GetTemperatureAlpha ");
                return StatusCode((int)HttpStatusCode.InternalServerError, "GetTemperatureAlpha");
            }
            else
            {
                return StatusCode((int)HttpStatusCode.OK, "Alpha");
            }
        }
```

- Beta breaks if the locationId is **10**.

- Gamma breaks if the locationId is **15**.


 ---

 ## Weather Service
This is the consumer that needs to respond and fail gracefully.

 ### Startup
 Add the required features to make this work.

 - Add logging
 ```CSharp
  Log.Logger = new LoggerConfiguration()
                   .ReadFrom.Configuration(configuration)
                   .CreateLogger();
 ```

 - Add cache
 ```CSharp
    services.AddDistributedMemoryCache();
 ```
 [Cache Link]( https://docs.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-6.0#use-setsize-size-and-sizelimit-to-limit-cache-size)

 - Add Polly Timeout Policy with timeout of 3 seconds.

  ```CSharp
            var timeoutPolicySeconds3 = Policy.TimeoutAsync<HttpResponseMessage>(3);
 ```
 [Polly Link](https://github.com/App-vNext/Polly/wiki/Polly-and-HttpClientFactory)

 - Add the three endpoints
 ```CSharp
 services.AddHttpClient("TemperatureServiceAlpha", client =>
            {
                client.BaseAddress = new Uri("http://localhost:6001/"); // TODO: actual paths  //
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddPolicyHandler(timeoutPolicySeconds3); 

            services.AddHttpClient("TemperatureServiceBeta", client =>
            {
                client.BaseAddress = new Uri("http://localhost:6001/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddPolicyHandler(timeoutPolicySeconds3); 

            services.AddHttpClient("TemperatureServiceGamma", client =>
            {
                client.BaseAddress = new Uri("http://localhost:6001/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddPolicyHandler(timeoutPolicySeconds3);
 ```

> The HttpClient also has a timeout property which worked, so using Polly for this is not required. TODO: Benchmark


### Controller 
If .NET has a preference in cache, use that value for the endpoint.  If there is no preference, try each endpoint until finding one that works.

#### Check the cache for a value.
```CSharp
    if(_memoryCache.Get(StickySessionKey) == null)
            {
                _memoryCache.Set(StickySessionKey, 0);
            }
```

- For quick reporting, cache the instances of the HttpClient.
```CSharp 
    if(_urlInstances.Count == 0)
            {
                _urlInstances = GetURLInstancesFromCache();
            }
```
> Dev Note - replace with logging or monitoring of endpoints.

- Start looping 
```CSharp
var attempts = 0;
var activeEndpoint = _memoryCache.Get<int>(StickySessionKey);

while (attempts < _urlInstances.Count)
{
    var client = _urlInstances[activeEndpoint];
    var httpClient = _httpClientFactory.CreateClient(client.Name);

```

- Attempt to get a response and return the response if received.
- If the response is successful, update the cache with the value of the client.
- Also, for quick reporting, update the cache with the number of successes.
```CSharp
HttpResponseMessage httpResponseMessage = await httpClient.GetAsync($"{client.Path}/{locationId}");
        if (httpResponseMessage.IsSuccessStatusCode)
        {
            client.Success++;
            UpdateURLInstanceCache();
            _memoryCache.Set(StickySessionKey, activeEndpoint); 
            _logger.LogInformation($"{client.Name} | Success | {httpResponseMessage.StatusCode} ");
            return StatusCode((int)httpResponseMessage.StatusCode, httpResponseMessage.Content.ReadAsStringAsync());
        }
```

- If the response fails or times out, catch the exception, log it, and move on.
```CSharp
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
```
