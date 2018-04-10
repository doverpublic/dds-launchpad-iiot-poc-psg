// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Insight.DataService.Controllers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Iot.Insight.DataService.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.ServiceFabric.Data;
    using Microsoft.ServiceFabric.Data.Collections;
    using Microsoft.AspNetCore.Hosting;

    using global::Iot.Common;

    [Route("api/[controller]")]
    public class DevicesController : Controller
    {
        private readonly IApplicationLifetime appLifetime;

        private readonly IReliableStateManager stateManager;

        public DevicesController(IReliableStateManager stateManager, IApplicationLifetime appLifetime)
        {
            this.stateManager = stateManager;
            this.appLifetime = appLifetime;
        }


        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetAsync()
        {
            IReliableDictionary<string, DeviceEventSeries> storeInProgressMessage = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, DeviceEventSeries>>(DataService.EventDictionaryName);

            List<object> devices = new List<object>();
            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                IAsyncEnumerable<KeyValuePair<string, DeviceEventSeries>> enumerable = await storeInProgressMessage.CreateEnumerableAsync(tx);
                IAsyncEnumerator<KeyValuePair<string, DeviceEventSeries>> enumerator = enumerable.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(appLifetime.ApplicationStopping))
                {
                    devices.Add(
                        new
                        {
                            DeviceId = enumerator.Current.Key,
                            enumerator.Current.Value.Events
                        });
                }
            }

            return this.Ok(devices);
        }

        [HttpGet]
        [Route("queue/length")]
        public async Task<IActionResult> GetQueueLengthAsync()
        {
            long count = 0;
            IReliableDictionary<string, EdgeDevice> storeDeviceCounters = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, EdgeDevice>>(DataService.EventDictionaryName);

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                IAsyncEnumerable<KeyValuePair<string, EdgeDevice>> enumerable = await storeDeviceCounters.CreateEnumerableAsync(tx);
                IAsyncEnumerator<KeyValuePair<string, EdgeDevice>> enumerator = enumerable.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(appLifetime.ApplicationStopping))
                {
                    string deviceId = enumerator.Current.Key;
                    EdgeDevice device = enumerator.Current.Value;

                    count += device.EventsCount;
                }

                return this.Ok(count);
            }
        }
    }
}
