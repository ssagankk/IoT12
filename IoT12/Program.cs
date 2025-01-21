using Opc.UaFx;
using Opc.UaFx.Client;
using Microsoft.Azure.Devices.Client;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using IoT_Project;

internal static class ProgramEntryPoint
{
    private static async Task Main(string[] args)
    {
        try
        {
            var settings = AppSettings.GetSettings();
            using (var client = new OpcClient(settings.ServerConnectionString))
            {
                Console.WriteLine("Łączenie z OPC UA...");
                client.Connect();
                Console.WriteLine("Połączono z OPC UA.");

                var connections = settings.AzureDevicesConnectionStrings;
                var devices = ConnectDevicesWithIoTDevices(client, connections);

                foreach (var device in devices)
                {
                    var telemetryData = new
                    {
                        deviceId = device.Attribute(OpcAttribute.DisplayName).Value,
                        productionStatus = client.ReadNode(device.NodeId).As<int>(),
                        timestamp = DateTime.UtcNow
                    };

                    await SendDataToIoTHub(connections[devices.IndexOf(device)], telemetryData);
                }
            }
        }
        catch (OpcException ex)
        {
            Console.WriteLine("Serwer OPC UA jest offline.");
            Console.WriteLine(ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Nieznany błąd: {ex.Message}");
        }
    }

    private static List<OpcNodeInfo> ConnectDevicesWithIoTDevices(OpcClient client, List<string> connections)
    {
        List<OpcNodeInfo> devices = BrowseDevices(client);
        if (devices.Count == 0)
            throw new Exception("Nie znaleziono urządzeń.");
        else if (devices.Count > connections.Count)
            throw new Exception($"Brakuje {devices.Count - connections.Count} połączeń do IoT Hub.");

        Console.WriteLine($"Znaleziono {devices.Count} urządzeń.");
        return devices;
    }

    private static List<OpcNodeInfo> BrowseDevices(OpcClient client)
    {
        var objectFolder = client.BrowseNode(OpcObjectTypes.ObjectsFolder);
        var devices = new List<OpcNodeInfo>();

        foreach (var childNode in objectFolder.Children())
        {
            if (IsDeviceNode(childNode))
                devices.Add(childNode);
        }

        return devices;
    }

    private static bool IsDeviceNode(OpcNodeInfo nodeInfo)
    {
        string pattern = @"^Device [0-9]+$";
        Regex correctName = new Regex(pattern);
        string nodeName = nodeInfo.Attribute(OpcAttribute.DisplayName).Value.ToString();
        return correctName.IsMatch(nodeName);
    }

    private static async Task SendDataToIoTHub(string connectionString, object telemetryData)
    {
        try
        {
            using var deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);
            string jsonMessage = JsonSerializer.Serialize(telemetryData);
            var message = new Message(Encoding.UTF8.GetBytes(jsonMessage));

            await deviceClient.SendEventAsync(message);
            Console.WriteLine($"Wysłano dane: {jsonMessage}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd wysyłania danych: {ex.Message}");
        }
    }
}
