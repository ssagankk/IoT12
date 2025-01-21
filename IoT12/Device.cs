using AgentApp;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.UaFx;
using Opc.UaFx.Client;
using System.Net.Mime;
using System.Text;

namespace VirtualDevices
{
    [Flags]
    public enum DeviceErrors
    {
        None = 0,
        EmergencyStop = 1,
        PowerFailure = 2,
        SensorFailure = 4,
        Unknown = 8SS
    }

    public class Device
    {
        private readonly OpcNodeInfo serverDevice;
        private readonly DeviceClient deviceClient;
        private readonly OpcClient opcClient;
        private readonly string nodeId;

        private int lastReportedErrorCode;
        private int lastReportedProductionRate;

        public OpcNodeInfo ServerDevice => serverDevice;
        public DeviceClient DeviceClient => deviceClient;

        public Device(DeviceClient deviceClient, OpcNodeInfo serverDevice, OpcClient opcClient)
        {
            this.deviceClient = deviceClient ?? throw new ArgumentNullException(nameof(deviceClient));
            this.serverDevice = serverDevice ?? throw new ArgumentNullException(nameof(serverDevice));
            this.opcClient = opcClient ?? throw new ArgumentNullException(nameof(opcClient));

            nodeId = CreateDeviceNodeId();
        }

        public async Task InitializeHandlersAsync()
        {
            Console.WriteLine($"{DateTime.Now}: Initializing device handlers...");
            await InitializeTwinOnStartAsync();

            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(DesiredProductionRateChangedAsync, null);
            await deviceClient.SetMethodHandlerAsync("EmergencyStop", EmergencyStop, null);
            await deviceClient.SetMethodHandlerAsync("ResetErrorStatus", ResetErrorStatus, null);
            await deviceClient.SetMethodDefaultHandlerAsync(DefaultMethod, null);

            lastReportedProductionRate = await GetReportedPropertyAsync("ProductionRate");
            lastReportedErrorCode = await GetReportedPropertyAsync("DeviceError");

            Console.WriteLine($"{DateTime.Now}: Device handlers initialized successfully.");
        }

        private string CreateDeviceNodeId()
        {
            string deviceName = serverDevice.Attribute(OpcAttribute.DisplayName).Value.ToString();
            return $"ns=2;s={deviceName}";
        }

        private string ReadDeviceNode(string suffix)
        {
            try
            {
                OpcReadNode node = new OpcReadNode($"{nodeId}{suffix}");
                OpcValue info = opcClient.ReadNode(node);
                return info.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to read OPC UA node {suffix}: {ex.Message}");
                return "0";
            }
        }

        #region Sending Telemetry
        public async Task ReadTelemetryAndSendToHubAsync()
        {
            try
            {
                var data = new
                {
                    deviceName = serverDevice.Attribute(OpcAttribute.DisplayName).Value.ToString(),
                    productionStatus = int.Parse(ReadDeviceNode("/ProductionStatus")),
                    workorderId = ReadDeviceNode("/WorkorderId"),
                    goodCount = int.Parse(ReadDeviceNode("/GoodCount")),
                    badCount = int.Parse(ReadDeviceNode("/BadCount")),
                    temperature = double.Parse(ReadDeviceNode("/Temperature"))
                };

                var dataString = JsonConvert.SerializeObject(data);
                await SendMessageToHubAsync(dataString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to read telemetry data: {ex.Message}");
            }
        }

        private async Task SendMessageToHubAsync(string content)
        {
            try
            {
                Console.WriteLine($"{DateTime.Now}: Sending telemetry: {content}");
                Message message = new Message(Encoding.UTF8.GetBytes(content))
                {
                    ContentType = MediaTypeNames.Application.Json,
                    ContentEncoding = "utf-8"
                };
                await deviceClient.SendEventAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to send message to IoT Hub: {ex.Message}");
            }
        }
        #endregion

        #region Device Twin
        public async Task<int> GetReportedPropertyAsync(string name)
        {
            try
            {
                var twin = await deviceClient.GetTwinAsync();
                var reportedProperties = twin.Properties.Reported;
                return reportedProperties.Contains(name) ? (int)reportedProperties[name] : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to get reported property: {ex.Message}");
                return 0;
            }
        }

        public async Task ReportPropertyToTwinAsync(string propertyName, int value)
        {
            try
            {
                var reportedProperties = new TwinCollection
                {
                    [propertyName] = value
                };

                Console.WriteLine($"{DateTime.Now}: Reporting {propertyName} = {value}");
                await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to report property to twin: {ex.Message}");
            }
        }

        private async Task InitializeTwinOnStartAsync()
        {
            int desiredInitialRate = await ReadDesiredRateIfExistsAsync();
            opcClient.WriteNode($"{nodeId}/ProductionRate", desiredInitialRate);
            Console.WriteLine($"{DateTime.Now}: Initial production rate set to {desiredInitialRate}");
            await ReportPropertyToTwinAsync("ProductionRate", desiredInitialRate);
            await ReportPropertyToTwinAsync("DeviceError", 0);
        }

        private async Task<int> ReadDesiredRateIfExistsAsync()
        {
            try
            {
                var desired = await deviceClient.GetTwinAsync();
                var desiredProperties = desired.Properties.Desired;
                return desiredProperties.Contains("ProductionRate") ? (int)desiredProperties["ProductionRate"] : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to read desired production rate: {ex.Message}");
                return 0;
            }
        }

        private async Task DesiredProductionRateChangedAsync(TwinCollection desiredProperties, object userContext)
        {
            if (desiredProperties.Contains("ProductionRate"))
            {
                int rate = (int)desiredProperties["ProductionRate"];
                await ReportPropertyToTwinAsync("ProductionRate", rate);
                opcClient.WriteNode($"{nodeId}/ProductionRate", rate);
                Console.WriteLine($"{DateTime.Now}: Production rate updated to {rate}");
            }
        }
        #endregion

        #region Direct Methods
        private async Task<MethodResponse> EmergencyStop(MethodRequest request, object userContext)
        {
            opcClient.CallMethod(nodeId, $"{nodeId}/EmergencyStop");
            Console.WriteLine($"{DateTime.Now}: EmergencyStop method executed.");
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> ResetErrorStatus(MethodRequest request, object userContext)
        {
            opcClient.CallMethod(nodeId, $"{nodeId}/ResetErrorStatus");
            Console.WriteLine($"{DateTime.Now}: ResetErrorStatus method executed.");
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> DefaultMethod(MethodRequest request, object userContext)
        {
            Console.WriteLine($"{DateTime.Now}: Unknown method called.");
            return new MethodResponse(0);
        }
        #endregion

        #region Sending Production Rate
        public async Task ReadProductionRateAndSendChangeToHubAsync()
        {
            int rate = int.Parse(ReadDeviceNode("/ProductionRate"));

            if (rate != lastReportedProductionRate)
            {
                await ReportPropertyToTwinAsync("ProductionRate", rate);
                lastReportedProductionRate = rate;
            }
        }
        #endregion
    }
}
