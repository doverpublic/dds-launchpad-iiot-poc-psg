using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.ServiceFabric;

using Newtonsoft.Json;

using global::Iot.Common;
using global::Iot.Common.REST;

using TargetSolution;
using Launchpad.Iot.PSG.Model;

namespace Launchpad.Iot.EventsProcessor.ExtenderService
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class ExtenderService : StatelessService
    {
        private string ServiceUniqueId = FnvHash.GetUniqueId();
        private FabricClient fabricClient = new FabricClient();

        public ExtenderService(StatelessServiceContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(serviceContext =>
                    new KestrelCommunicationListener(serviceContext, "ServiceEndpoint", (url, listener) =>
                    {
                        // The idea is to create a listening port for each instance 
                        // This application will never be called - the only purpose of this listener is

                        url += $"/eventsprocessor/extender/{ServiceUniqueId}";

                        ServiceEventSource.Current.Message( "Extender Service Initialized on " + url + " - Dummy url not to be used" );

                        return new WebHostBuilder()
                                    .UseKestrel()
                                    .ConfigureServices(
                                        services => services
                                            .AddSingleton<StatelessServiceContext>(serviceContext)
                                            .AddSingleton<ITelemetryInitializer>((serviceProvider) => FabricTelemetryInitializerExtension.CreateFabricTelemetryInitializer(serviceContext)))
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<Startup>()
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                    .UseApplicationInsights()
                                    .UseUrls(url)
                                    .Build();
                    }))
            };
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // Get the IoT Hub connection string from the Settings.xml config file
            // from a configuration package named "Config"
            string PublishDataServiceURLs =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["ExtenderServiceConfigInformation"]
                    .Parameters["PublishDataServiceURLs"]
                    .Value.Trim('/');

            ServiceEventSource.Current.ServiceMessage(this.Context, $"ExtenderService - {ServiceUniqueId} - RunAsync - Starting service  - Data Service URLs[{PublishDataServiceURLs}]");

            if(PublishDataServiceURLs != null && PublishDataServiceURLs.Length > 0 )
            {
                string[] routingparts = PublishDataServiceURLs.Split(';');

                using (HttpClient httpClient = new HttpClient(new HttpServiceClientHandler()))
                {
                    int searchInterval = global::Iot.Common.Names.ExtenderStandardRetryWaitIntervalsInMills;
                    string servicePathAndQuery = $"/api/devices/history/interval/{searchInterval}";

                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string reportUniqueId = FnvHash.GetUniqueId();

                        int messageCount = 0;
                        try
                        {
                            ServiceUriBuilder uriBuilder = new ServiceUriBuilder(routingparts[0], global::Iot.Common.Names.InsightDataServiceName);
                            Uri serviceUri = uriBuilder.Build();

                            ServiceEventSource.Current.ServiceMessage(this.Context, $"ExtenderService - {ServiceUniqueId} - RunAsync - About to call URL[{serviceUri}] to collect completed messages - SearchInterval[{searchInterval}]");

                            // service may be partitioned.
                            // this will aggregate the queue lengths from each partition
                            System.Fabric.Query.ServicePartitionList partitions = await this.fabricClient.QueryManager.GetPartitionListAsync(serviceUri);

                            foreach (System.Fabric.Query.Partition partition in partitions)
                            {
                                List<DeviceViewModelList> deviceViewModelList = new List<DeviceViewModelList>();
                                Uri getUrl = new HttpServiceUriBuilder()
                                    .SetServiceName(serviceUri)
                                    .SetPartitionKey(((Int64RangePartitionInformation)partition.PartitionInformation).LowKey)
                                    .SetServicePathAndQuery(servicePathAndQuery)
                                    .Build();

                                HttpResponseMessage response = await httpClient.GetAsync(getUrl, cancellationToken);

                                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    JsonSerializer serializer = new JsonSerializer();
                                    using (StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync()))
                                    {
                                        using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
                                        {
                                            List<DeviceViewModelList> resultList = serializer.Deserialize<List<DeviceViewModelList>>(jsonReader);

                                            deviceViewModelList.AddRange(resultList);
                                        }
                                    }

                                    if (deviceViewModelList.Count > 0)
                                    {
                                        messageCount += deviceViewModelList.Count;

                                        await ReportsDataHandler.PublishReportDataFor(reportUniqueId, routingparts[1], deviceViewModelList, this.Context, httpClient, cancellationToken, ServiceEventSource.Current);

                                    }
                                }
                            }
                            ServiceEventSource.Current.ServiceMessage(this.Context, $"ExtenderService - {ServiceUniqueId} - RunAsync - Finished posting messages to report stream - Total number of messages[{messageCount}]");
                        }
                        catch (Exception ex)
                        {
                            ServiceEventSource.Current.ServiceMessage(this.Context, $"ExtenderService - {ServiceUniqueId} - RunAsync - Severe error when reading or sending messages to report stream - Exception[{ex}] - Inner Exception[{ex.InnerException}] StackTrace[{ex.StackTrace}]");
                        }

                        await Task.Delay(global::Iot.Common.Names.ExtenderStandardRetryWaitIntervalsInMills);
                    }
                }
            }
            else
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, $"ExtenderService - {ServiceUniqueId} - RunAsync - Starting service  - Data Service URLs[{PublishDataServiceURLs}]");
            }
        }
    }
}
