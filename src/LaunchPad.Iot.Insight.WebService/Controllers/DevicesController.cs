// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Insight.WebService.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Fabric.Query;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Hosting;
    using Newtonsoft.Json;

    using Iot.Insight.WebService.ViewModels;

    using global::Iot.Common;

    [Route("api/[controller]")]
    public class DevicesController : Controller
    {
        private const string TargetSiteDataServiceName = "DataService";
        private readonly FabricClient fabricClient;
        private readonly IApplicationLifetime appLifetime;
        private readonly HttpClient httpClient;

        private readonly StatelessServiceContext context;

        public DevicesController(FabricClient fabricClient, HttpClient httpClient, IApplicationLifetime appLifetime, StatelessServiceContext context)
        {
            this.fabricClient = fabricClient;
            this.httpClient = httpClient;
            this.appLifetime = appLifetime;
            this.context = context;
        }


        [HttpGet]
        [Route("")]
        public IActionResult Devices()
        {
            // Manage session and Context
            HttpServiceUriBuilder contextUri = new HttpServiceUriBuilder().SetServiceName(this.context.ServiceName);

            if( HTTPHelper.IsSessionExpired( HttpContext, this ))
            {
                return Redirect(contextUri.GetServiceNameSiteHomePath());
            }
            else
            {
                this.ViewData["TargetSite"] = contextUri.GetServiceNameSite();
                this.ViewData["PageTitle"] = "Devices";
                this.ViewData["HeaderTitle"] = "Devices Dashboard";
                return this.View();
            }
        }

        [HttpGet]
        [Route("queue/length")]
        public async Task<IActionResult> GetQueueLengthAsync()
        {
            // Manage session and Context
//            HttpServiceUriBuilder contextUri = new HttpServiceUriBuilder().SetServiceName(this.context.ServiceName);

//            if (HTTPHelper.IsSessionExpired(HttpContext, this))
//            {
//                return Redirect(contextUri.GetServiceNameSiteHomePath());
//            }
//            else
//            {
                ServiceUriBuilder uriBuilder = new ServiceUriBuilder(TargetSiteDataServiceName);
                Uri serviceUri = uriBuilder.Build();

                // service may be partitioned.
                // this will aggregate the queue lengths from each partition
                ServicePartitionList partitions = await this.fabricClient.QueryManager.GetPartitionListAsync(serviceUri);

                long count = 0;
                foreach (Partition partition in partitions)
                {
                    Uri getUrl = new HttpServiceUriBuilder()
                        .SetServiceName(serviceUri)
                        .SetPartitionKey(((Int64RangePartitionInformation)partition.PartitionInformation).LowKey)
                        .SetServicePathAndQuery($"/api/devices/queue/length")
                        .Build();

                    HttpResponseMessage response = await this.httpClient.GetAsync(getUrl, this.appLifetime.ApplicationStopping);

                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        return this.StatusCode((int)response.StatusCode);
                    }

                    string result = await response.Content.ReadAsStringAsync();

                    count += Int64.Parse(result);
                }

                return this.Ok(count);
  //          }
        }

        [HttpGet]
        [Route("device/{deviceId}/timestamp/{timestampVal}")]
        [Route("deviceList")]
        public async Task<IActionResult> GetDevicesAsync( string deviceId = null, string timestampVal = null)
        {
            // Manage session and Context
//            HttpServiceUriBuilder contextUri = new HttpServiceUriBuilder().SetServiceName(this.context.ServiceName);

//            if (HTTPHelper.IsSessionExpired(HttpContext, this))
//            {
//                return Redirect(contextUri.GetServiceNameSiteHomePath());
//            }
//            else
//            {
                ServiceUriBuilder uriBuilder = new ServiceUriBuilder(TargetSiteDataServiceName);
                Uri serviceUri = uriBuilder.Build();

                // service may be partitioned.
                // this will aggregate device IDs from all partitions
                ServicePartitionList partitions = await this.fabricClient.QueryManager.GetPartitionListAsync(serviceUri);

                List<DeviceViewModel> deviceViewModels = new List<DeviceViewModel>();
                foreach (Partition partition in partitions)
                {
                    Uri getUrl = new HttpServiceUriBuilder()
                        .SetServiceName(serviceUri)
                        .SetPartitionKey(((Int64RangePartitionInformation)partition.PartitionInformation).LowKey)
                        .SetServicePathAndQuery($"/api/devices")
                        .Build();

                    HttpResponseMessage response = await this.httpClient.GetAsync(getUrl, this.appLifetime.ApplicationStopping);

                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        return this.StatusCode((int)response.StatusCode);
                    }

                    JsonSerializer serializer = new JsonSerializer();
                    using (StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync()))
                    {
                        using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
                        {
                            List<DeviceViewModel> result = serializer.Deserialize<List<DeviceViewModel>>(jsonReader);

                            if (result != null)
                            {
                                if (deviceId == null)
                                    deviceViewModels.AddRange(result);
                                else
                                {
                                    DateTimeOffset timestamp = new DateTimeOffset();
                                    timestamp = DateTime.Parse(timestampVal);

                                    foreach (DeviceViewModel device in result)
                                    {
                                        if (device.Id.Equals(deviceId, StringComparison.InvariantCultureIgnoreCase) &&
                                            device.Timestamp == timestamp)
                                            deviceViewModels.Add(device);
                                    }
                                }
                            }
                        }
                    }
                }

                return this.Ok(deviceViewModels);
//            }
        }


        // PRIVATE METHODS

    }
}
