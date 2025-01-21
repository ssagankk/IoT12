using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;

namespace IoT12
{
    public class AppSettings
    {
        public string ServerConnectionString { get; set; } = string.Empty;
        public List<string> AzureDevicesConnectionStrings { get; set; } = new List<string>();

        public static AppSettings GetSettings()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var settings = new AppSettings();
            configuration.Bind(settings);

            if (string.IsNullOrEmpty(settings.ServerConnectionString))
            {
                throw new InvalidOperationException("ServerConnectionString is required.");
            }

            if (settings.AzureDevicesConnectionStrings == null || settings.AzureDevicesConnectionStrings.Count == 0)
            {
                throw new InvalidOperationException("AzureDevicesConnectionStrings cannot be empty.");
            }

            return settings;
        }
    }
}
