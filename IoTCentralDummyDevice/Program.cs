using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Azure.Devices.Client; // IoT Hub Client
using Microsoft.Azure.Devices.Shared; //Properties manupulation
using Newtonsoft.Json;
using System.Threading;

namespace SensorDeviceApp
{
    class Program
    {
        private static string iotHubName = "saas-iothub-442d96af-c2cb-4165-bd60-0493882670a8.azure-devices.net";
        private static string deviceId = "57lgyv";
        private static string deviceKey = "3cTeKgF6U6MLEzDOtkAkVfWvMvxj6AsjgklqsSYWRuM=";
        // HostName=saas-iothub-1a7fd014-bb94-4bc5-9ee6-ba4c2bd31dfd.azure-devices.net;DeviceId=1nnidqk;SharedAccessKey=m5DTw6+hVHfo7tbomrucRNu/ulz13MIkvv+jJgv20a0=
        //HostName=saas-iothub-442d96af-c2cb-4165-bd60-0493882670a8.azure-devices.net;DeviceId=57lgyv;SharedAccessKey=3cTeKgF6U6MLEzDOtkAkVfWvMvxj6AsjgklqsSYWRuM=

        static DeviceClient deviceClient = null;
        static double baseTemp = 25.0;
        static double temperature = 0;
        static TwinCollection reportedProperties = new TwinCollection();

        static void Main(string[] args)
        {
            Log("Simulated device started...", ConsoleColor.Green);

            deviceClient = DeviceClient.Create(iotHubName,
                new DeviceAuthenticationWithRegistrySymmetricKey(
                        deviceId, deviceKey));

            // Telemetry Initialization
            InitTelemetry();

            // D2C
            SendD2CAsync();

            // C2D
            ReceiveC2DAsync();

            // Set Desired Properties Callback
            SetDesiredProPropertiesUpdate();

            // Main termination hold untill key is pressed
            Console.ReadLine();
        }
        private static async void SendD2CAsync()
        {
            Random rand = new Random();

            while (true)
            {
                temperature = baseTemp + rand.NextDouble() - rand.NextDouble();
                var temperatureData = new
                {
                    deviceId = deviceId,
                    temperature = temperature,
                    timeStamp = DateTime.Now
                };

                var JsonMessage = JsonConvert.SerializeObject(temperatureData);
                var message = new Message(Encoding.ASCII.GetBytes(
                  JsonMessage));
                Log(JsonMessage);
                await deviceClient.SendEventAsync(message);

                await Task.Delay(1000);
            }
        }
        private static async void ReceiveC2DAsync()
        {
            Log("\nReceiving cloud to device messages from service", ConsoleColor.Green);

            while (true)
            {
                Message receivedMessage = await deviceClient.ReceiveAsync();
                if (receivedMessage == null) continue;

                Log("Received message: " + Encoding.ASCII.GetString(receivedMessage.GetBytes()) + ".", ConsoleColor.Yellow);

                if (receivedMessage.Properties.ContainsKey("baseTemperature"))
                {
                    var val = receivedMessage.Properties["baseTemperature"];
                    baseTemp = Double.Parse(val);
                    Log("Received request to change base temperature to " + val + ".", ConsoleColor.Yellow);
                }
                await deviceClient.CompleteAsync(receivedMessage);
            }
        }
        private static void SetDesiredProPropertiesUpdate()
        {
            try
            {
               Log("Wait for desired telemetry...", ConsoleColor.Cyan);
                deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null).Wait();
                //                Console.ReadKey();
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error in sample: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", ex.Message);
            }
        }
        private static async void InitTelemetry()
        {
            try
            {
                Log("Report initial telemetry config:");
                TwinCollection telemetryConfig = new TwinCollection();

                telemetryConfig["configId"] = "0";
                telemetryConfig["sendFrequency"] = "1s";
                telemetryConfig["baseTemperature"] = baseTemp.ToString();
                reportedProperties["telemetryConfig"] = telemetryConfig;
                Log(JsonConvert.SerializeObject(reportedProperties));

                await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error in sample: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", ex.Message);
            }
        }

        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Log("Desired property changed !!", ConsoleColor.Cyan);
                Log("Desired property change:");
                Log(JsonConvert.SerializeObject(desiredProperties));

                var currentTelemetryConfig = reportedProperties["telemetryConfig"];

                var desiredBaseTemperatureConfig = desiredProperties["baseTemperature"];
                if(desiredBaseTemperatureConfig != null)
                {
                    string val = desiredBaseTemperatureConfig["value"];
                    Log("Base Temperatura changed to : " + val, ConsoleColor.Cyan);

                    Log("\nInitiating config change", ConsoleColor.Cyan);
                    currentTelemetryConfig["status"] = "Pending";
//                    currentTelemetryConfig["pendingConfig"] = desiredTelemetryConfig;

                    await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
               
                    baseTemp = Double.Parse(val);
                    Log("Received request to change base temperature to " + val + ".", ConsoleColor.Yellow);

                    CompleteConfigChange();
                }
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error in sample: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", ex.Message);
            }
        }
        public static async void CompleteConfigChange()
        {
            try
            {
                var currentTelemetryConfig = reportedProperties["telemetryConfig"];

                Log("\nSimulating device reset", ConsoleColor.Cyan);
                await Task.Delay(30000);

                Console.WriteLine("\nCompleting config change");

                await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
                Log("Config change complete.", ConsoleColor.Cyan);
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error in sample: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", ex.Message);
            }
        }
        private static void Log(string msg, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine("{0}", msg);
            Console.ResetColor();
        }
        private static void Log(string msg)
        {
            Log(msg, ConsoleColor.White);
        }
    }
}
