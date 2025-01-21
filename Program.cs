using Opc.UaFx;
using Opc.UaFx.Client;
using Microsoft.Azure.Devices.Client;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using IoT12;

namespace IoT12
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting IoT12 project...");

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                await RunAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Program terminated by user.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
        }

        private static async Task RunAsync(CancellationToken token)
        {
            var settings = AppSettings.GetSettings();
            using var client = new OpcClient(settings.ServerConnectionString);
            client.Connect();
            Console.WriteLine("Connected to OPC UA server.");

            var connections = settings.AzureDevicesConnectionStrings;
            var devices = ConnectDevicesWithIoTDevices(client, connections);

            foreach (var (device, index) in devices.Select((value, i) => (value, i)))
            {
                if (token.IsCancellationRequested)
                    break;

                var telemetryData = new
                {
                    deviceId = device.Attribute(OpcAttribute.DisplayName).Value,
                    productionStatus = client.ReadNode(device.NodeId).As<int>(),
                    timestamp = DateTime.UtcNow
                };

                await SendDataToIoTHub(connections[index], telemetryData);
            }
        }

        private static List<OpcNodeInfo> ConnectDevicesWithIoTDevices(OpcClient client, List<string> connections)
        {
            var devices = BrowseDevices(client);
            if (devices.Count == 0)
                throw new Exception("No devices found.");
            if (devices.Count > connections.Count)
                throw new Exception($"Missing {devices.Count - connections.Count} IoT Hub connections.");

            Console.WriteLine($"Found {devices.Count} devices.");
            return devices;
        }

        private static List<OpcNodeInfo> BrowseDevices(OpcClient client)
        {
            var objectFolder = client.BrowseNode(OpcObjectTypes.ObjectsFolder);
            return objectFolder.Children().Where(IsDeviceNode).ToList();
        }

        private static bool IsDeviceNode(OpcNodeInfo nodeInfo)
        {
            string pattern = @"^Device \d+$";
            return Regex.IsMatch(nodeInfo.Attribute(OpcAttribute.DisplayName).Value.ToString(), pattern);
        }

        private static async Task SendDataToIoTHub(string connectionString, object telemetryData)
        {
            try
            {
                using var deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);
                string jsonMessage = JsonSerializer.Serialize(telemetryData);
                var message = new Message(Encoding.UTF8.GetBytes(jsonMessage));

                await deviceClient.SendEventAsync(message);
                Console.WriteLine($"Data sent: {jsonMessage}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending data: {ex.Message}");
            }
        }
    }
}
