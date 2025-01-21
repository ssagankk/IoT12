using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace IoT_Project
{
    public class AppSettings
    {
        public string ServerConnectionString { get; set; }
        public List<string> AzureDevicesConnectionStrings { get; set; }

        public static AppSettings GetSettings()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            return configuration.Get<AppSettings>();
        }
    }
}