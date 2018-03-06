// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.DeviceEmulator
{
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;
    using global::Iot.Common;

    internal class Program
    {
        private static string connectionString;
        private static string clusterAddress;
        private static RegistryManager registryManager;
        private static FabricClient fabricClient;
        private static IEnumerable<Device> devices;
        private static IEnumerable<string> tenants;

        // credential fields
        private static X509Credentials credential;
        private static string credentialType;
        private static string findType;
        private static string findValue;
        private static string serverCertThumbprint;
        private static string storeLocation;
        private static string storeName;
   

        private static void Main(string[] args)
        {
            Console.WriteLine("Enter IoT Hub Connection String: ");
            connectionString = Console.ReadLine();

            Console.WriteLine("Enter Service Fabric cluster Address Where your IoT Solution is Deployed (or blank for local): ");
            clusterAddress = Console.ReadLine();

            registryManager = RegistryManager.CreateFromConnectionString(connectionString);


            // let's deal with collecting credentials information for the cluster connection
            if( !String.IsNullOrEmpty(clusterAddress) )
            {
                Console.WriteLine("Enter Credential Type [none, x509, Windows] (or blank for unsecured Service Fabric): ");
                credentialType = Console.ReadLine();

                Console.WriteLine("Enter Server Certificate Thumbprint  (or blank for not working with server certificate): ");
                serverCertThumbprint = Console.ReadLine();

                if ( !String.IsNullOrEmpty(credentialType) || !String.Equals( credentialType, "none" ) )
                {
                    Console.WriteLine("Enter Credential Find Type [FindByThumbprint, ... ] (or blank for not working with find type): ");
                    findType = Console.ReadLine();
                }

                if( !String.IsNullOrEmpty(findType) )
                { 
                    Console.WriteLine("Enter Credential Find Value: ");
                    findValue = Console.ReadLine();

                    Console.WriteLine("Enter Credential Find Location: ");
                    storeLocation = Console.ReadLine();

                    Console.WriteLine("Enter Credential Find Location Name: ");
                    storeName = Console.ReadLine();
                }
            }

            if (!String.IsNullOrEmpty(findType))
            {
                credential = new X509Credentials();

                credential.RemoteCertThumbprints.Add( serverCertThumbprint );

                if( String.Equals( storeLocation.ToUpper(), "CURRENTUSER" ))
                    credential.StoreLocation = System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser;
                else
                    credential.StoreLocation = System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine;

                credential.StoreName = storeName;
                credential.FindValue = findValue;

                if (String.Equals(findType.ToUpper(), "FINDBYTHUMBPRINT"))
                    credential.FindType = System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint;
                else
                    Console.WriteLine("X509 Find Type Not Supported [{0}]", findType);
            }

            fabricClient = String.IsNullOrEmpty(clusterAddress)
            ? new FabricClient()
            : credential == null ? new FabricClient(clusterAddress) : new FabricClient(credential, clusterAddress);

            Task.Run(
                async () =>
                {
                    while (true)
                    {
                        try
                        {
                            devices = await registryManager.GetDevicesAsync(Int32.MaxValue);
                            tenants = (await fabricClient.QueryManager.GetApplicationListAsync())
                                .Where(x => x.ApplicationTypeName == Names.InsightApplicationTypeName)
                                .Select(x => x.ApplicationName.ToString().Replace(Names.InsightApplicationNamePrefix + "/", ""));

                            Console.WriteLine();
                            Console.WriteLine("Devices IDs: ");
                            foreach (Device device in devices)
                            {
                                Console.WriteLine(device.Id);
                            }

                            Console.WriteLine();
                            Console.WriteLine("Insight Application URI: ");
                            foreach (string tenant in tenants)
                            {
                                Console.WriteLine(tenant);
                            }

                            Console.WriteLine();
                            Console.WriteLine("Commands:");
                            Console.WriteLine("1: Register a device");
                            Console.WriteLine("2: Register random devices");
                            Console.WriteLine("3: Send data from a device");
                            Console.WriteLine("4: Send data from all devices");
                            Console.WriteLine("5: Exit");

                            string command = Console.ReadLine();

                            switch (command)
                            {
                                case "1":
                                    Console.WriteLine("Make up a Device ID: ");
                                    string deviceId = Console.ReadLine();
                                    await AddDeviceAsync(deviceId);
                                    break;
                                case "2":
                                    Console.WriteLine("How many devices? ");
                                    int num = Int32.Parse(Console.ReadLine());
                                    await AddRandomDevicesAsync(num);
                                    break;
                                case "3":
                                    Console.WriteLine("Insight Application URI: ");
                                    string tenant = Console.ReadLine();
                                    Console.WriteLine("Device ID: ");
                                    string deviceKey = Console.ReadLine();
                                    await SendDeviceToCloudMessagesAsync(deviceKey, tenant);
                                    break;
                                case "4":
                                    Console.WriteLine("Insight Application URI: ");
                                    string tenantName = Console.ReadLine();
                                    Console.WriteLine("Iterations: ");
                                    int iterations = Int32.Parse(Console.ReadLine());
                                    await SendAllDevices(tenantName, iterations);
                                    break;
                                case "5":
                                    return;
                                default:
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Oops, {0}", ex.Message);
                        }
                    }
                })
                .GetAwaiter().GetResult();
        }

        private static async Task SendAllDevices(string tenant, int iterations)
        {
            for (int i = 0; i < iterations; ++i)
            {
                try
                {
                    List<Task> tasks = new List<Task>(devices.Count());
                    foreach (Device device in devices)
                    {
                        tasks.Add(SendDeviceToCloudMessagesAsync(device.Id, tenant));
                    }

                    await Task.WhenAll(tasks);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Send failed. {0}", ex.Message);
                }
            }
        }

        private static async Task SendDeviceToCloudMessagesAsync(string deviceId, string tenant)
        {
            string iotHubUri = connectionString.Split(';')
                .First(x => x.StartsWith("HostName=", StringComparison.InvariantCultureIgnoreCase))
                .Replace("HostName=", "").Trim();

            Device device = devices.FirstOrDefault(x => x.Id == deviceId);
            if (device == null)
            {
                Console.WriteLine("Device '{0}' doesn't exist.", deviceId);
            }

            DeviceClient deviceClient = DeviceClient.Create(
                iotHubUri,
                new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, device.Authentication.SymmetricKey.PrimaryKey));

            List<object> events = new List<object>();
            for (int i = 0; i < 10; ++i)
            {
                var body = new
                {
                    Timestamp = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(i))
                };

                events.Add(body);
            }

            Microsoft.Azure.Devices.Client.Message message;
            JsonSerializer serializer = new JsonSerializer();
            using (MemoryStream stream = new MemoryStream())
            {
                using (StreamWriter streamWriter = new StreamWriter(stream))
                {
                    using (JsonTextWriter jsonWriter = new JsonTextWriter(streamWriter))
                    {
                        serializer.Serialize(jsonWriter, events);
                    }
                }

                message = new Microsoft.Azure.Devices.Client.Message(stream.GetBuffer());
                message.Properties.Add( Names.EventKeyFieldTenantId, tenant );
                message.Properties.Add( Names.EventKeyFieldDeviceId, deviceId );

                await deviceClient.SendEventAsync(message);

                Console.WriteLine($"Sent message: {Encoding.UTF8.GetString(stream.GetBuffer())}");
            }
        }

        private static async Task AddRandomDevicesAsync(int count)
        {
            int start = devices.Count();

            for (int i = start; i < start + count; ++i)
            {
                await AddDeviceAsync("device" + i);
            }
        }

        private static async Task AddDeviceAsync(string deviceId)
        {
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(connectionString);

            try
            {
                await registryManager.AddDeviceAsync(new Device(deviceId));
                Console.WriteLine("Added device {0}", deviceId);
            }
            catch (Microsoft.Azure.Devices.Common.Exceptions.DeviceAlreadyExistsException)
            {
            }
        }
    }
}
