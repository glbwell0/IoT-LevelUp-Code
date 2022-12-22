// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// This application was adapted from the Azure IoT Hub device SDK for .NET
// For more samples see: https://github.com/Azure/azure-iot-sdk-csharp/tree/main/iothub/device/samples

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace SimulatedDevice
{
    /// <summary>
    /// This sample illustrates the very basics of a device app sending telemetry. For a more comprehensive device app sample, please see
    /// <see href="https://github.com/Azure-Samples/azure-iot-samples-csharp/tree/main/iot-hub/Samples/device/DeviceReconnectionSample"/>.
    /// </summary>
    internal class Program
    {
        static int _intervalFrequency = 3;

        private static async Task Main(string[] args)
        {
            string deviceConnectionString = "<Paste your device connection string here>";

            Console.WriteLine("IoT Hub Quickstarts #1 - Simulated device.");

            // Connect to the IoT hub using the MQTT protocol by default
            using var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt);

            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, deviceClient).ConfigureAwait(false);

            // direct method to reset the sensor values back to their initial state
            await deviceClient.SetMethodHandlerAsync("reset", ResetMethod, null);

            // Set up to close the connection gracefully on exiting
            Console.WriteLine("Press control-C to exit.");
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
                Console.WriteLine("Exiting...");
            };

            // Run the telemetry loop
            await SendDeviceToCloudMessagesAsync(deviceClient, cts.Token);

            // SendDeviceToCloudMessagesAsync is designed to run until cancellation has been explicitly requested by Console.CancelKeyPress.
            await deviceClient.CloseAsync();

            Console.WriteLine("Device simulator finished.");
        }

        // Async method to send simulated telemetry
        private static async Task SendDeviceToCloudMessagesAsync(DeviceClient deviceClient, CancellationToken ct)
        {
            // Initial telemetry values
            double minTemperature = 20;
            double minHumidity = 60;
            var rand = new Random();

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    double currentTemperature = minTemperature + rand.NextDouble() * 15;
                    double currentHumidity = minHumidity + rand.NextDouble() * 20;

                    // Create JSON message
                    string messageBody = System.Text.Json.JsonSerializer.Serialize(
                        new
                        {
                            temperature = currentTemperature,
                            humidity = currentHumidity,
                        });
                    using var message = new Message(Encoding.ASCII.GetBytes(messageBody))
                    {
                        ContentType = "application/json",
                        ContentEncoding = "utf-8",
                    };

                    // Add a custom application property to the message.
                    // An IoT hub can filter on these properties without access to the message body.
                    message.Properties.Add("temperatureAlert", (currentTemperature > 30) ? "true" : "false");

                    // Send the telemetry message
                    await deviceClient.SendEventAsync(message, ct);
                    Console.WriteLine($"{DateTime.Now} > Sending message: {messageBody}");

                    await Task.Delay(_intervalFrequency * 1000, ct);
                }
            }
            catch (TaskCanceledException) { } 
        }

        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {

            Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

            if (desiredProperties.Contains("IntervalFrequency") )
            {
                _intervalFrequency = desiredProperties["IntervalFrequency"];
                Console.WriteLine($"Frequency updated to {_intervalFrequency} seconds");
            }

            var client = userContext as DeviceClient;

            var reportedProperties = new TwinCollection
            {
                ["IntervalFrequency"] = _intervalFrequency,
                ["DateTimeLastDesiredPropertyChangeReceived"] = DateTime.Now
            };

            await client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);

            Console.WriteLine("Reported properties updated in device twin");
        }

        private static Task<MethodResponse> ResetMethod(MethodRequest Request, object UserContext)
        {
            Console.WriteLine("Device reset requested");

            var response = new MethodResponse((int)HttpStatusCode.OK);
            return Task.FromResult(response);
        }
    }
}
