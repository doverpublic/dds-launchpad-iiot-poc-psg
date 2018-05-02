﻿// ------------------------------------------------------------
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

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Hosting;
    using Newtonsoft.Json;

    using Launchpad.Iot.PSG.Model;

    using global::Iot.Common;

    [Route("api/[controller]")]
    public class DevicesController : Controller
    {
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

            if (HTTPHelper.IsSessionExpired(HttpContext, this))
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
            HttpServiceUriBuilder contextUri = new HttpServiceUriBuilder().SetServiceName(this.context.ServiceName);
            string reportsSecretKey = HTTPHelper.GetQueryParameterValueFor(HttpContext, Names.REPORTS_SECRET_KEY_NAME);
            long count = 0;

            if((reportsSecretKey.Length == 0) && HTTPHelper.IsSessionExpired(HttpContext, this))
            {
                return Ok(contextUri.GetServiceNameSiteHomePath());
            }
            else
            {
                ServiceUriBuilder uriBuilder = new ServiceUriBuilder(Names.InsightDataServiceName);
                Uri serviceUri = uriBuilder.Build();

                // service may be partitioned.
                // this will aggregate the queue lengths from each partition
                ServicePartitionList partitions = await this.fabricClient.QueryManager.GetPartitionListAsync(serviceUri);

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
            }

            return this.Ok(count);
        }
    

        [HttpGet]
        [Route("device/{deviceId}")]
        [Route("deviceList")]
        public async Task<IActionResult> GetDevicesAsync( string deviceId = null )
        {
            // Manage session and Context
            HttpServiceUriBuilder contextUri = new HttpServiceUriBuilder().SetServiceName(this.context.ServiceName);
            string reportsSecretKey = HTTPHelper.GetQueryParameterValueFor(HttpContext, Names.REPORTS_SECRET_KEY_NAME);
            List<DeviceViewModelList> deviceViewModelList = new List<DeviceViewModelList>();

            if ((reportsSecretKey.Length == 0) && HTTPHelper.IsSessionExpired(HttpContext, this))
            {
                return Ok(contextUri.GetServiceNameSiteHomePath());
            }
            else if (reportsSecretKey.Length > 0 )
            {
                // simply return some empty answer - no indication of error for security reasons
                if ( !reportsSecretKey.Equals(Names.REPORTS_SECRET_KEY_VALUE) )
                    return this.Ok(deviceViewModelList);
            }

            deviceViewModelList = await GetDevicesDataAsync( deviceId, this.httpClient, this.fabricClient, this.appLifetime);
            return this.Ok(deviceViewModelList);
        }

        public static async Task<List<DeviceViewModelList>> GetDevicesDataAsync( string deviceId, HttpClient httpClient, FabricClient fabricClient, IApplicationLifetime appLifetime)
        {
            List<DeviceViewModelList> deviceViewModelList = new List<DeviceViewModelList>();
            ServiceUriBuilder uriBuilder = new ServiceUriBuilder(Names.InsightDataServiceName);
            Uri serviceUri = uriBuilder.Build();

            // service may be partitioned.
            // this will aggregate device IDs from all partitions
            ServicePartitionList partitions = await fabricClient.QueryManager.GetPartitionListAsync(serviceUri);

            foreach (Partition partition in partitions)
            {
                Uri getUrl = new HttpServiceUriBuilder()
                    .SetServiceName(serviceUri)
                    .SetPartitionKey(((Int64RangePartitionInformation)partition.PartitionInformation).LowKey)
                    .SetServicePathAndQuery($"/api/devices")
                    .Build();

                HttpResponseMessage response = await httpClient.GetAsync(getUrl,appLifetime.ApplicationStopping);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return deviceViewModelList;
                }

                JsonSerializer serializer = new JsonSerializer();
                using (StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync()))
                {
                    using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
                    {
                        List<DeviceViewModelList> result = serializer.Deserialize<List<DeviceViewModelList>>(jsonReader);

                        if (result != null)
                        {
                            if (deviceId == null)
                                deviceViewModelList.AddRange(result);
                            else
                            {
                                foreach (DeviceViewModelList device in result)
                                {
                                    if (device.DeviceId.Equals(deviceId, StringComparison.InvariantCultureIgnoreCase))
                                        deviceViewModelList.Add(device);
                                }
                            }
                        }
                    }
                }
            }

            return deviceViewModelList;
        }
    }
}
