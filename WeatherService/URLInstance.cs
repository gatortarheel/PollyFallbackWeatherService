using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherService
{
    public class URLInstance
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string URL { get; set; }
        public string Path { get; set; }
        public int Failure { get; set; }
        public int Success { get; set; }
    }
}
