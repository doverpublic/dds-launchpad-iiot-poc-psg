// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Insight.DataService.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Iot.Insight.DataService.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.ServiceFabric.Data;
    using Microsoft.ServiceFabric.Data.Collections;
    using System.Fabric;
    using Microsoft.AspNetCore.Hosting;

    using global::Iot.Common;

    [Route("api/[controller]")]
    public class EventsController : Controller
    {
        private readonly IApplicationLifetime appLifetime;

        private readonly IReliableStateManager stateManager;

        private readonly StatefulServiceContext context;

        public EventsController(IReliableStateManager stateManager, StatefulServiceContext context, IApplicationLifetime appLifetime)
        {
            this.stateManager = stateManager;
            this.context = context;
            this.appLifetime = appLifetime;
        }


        [HttpPost]
        [Route("{deviceId}")]
        public async Task<IActionResult> Post(string deviceId, [FromBody] IEnumerable<DeviceEvent> events)
        {
            if (String.IsNullOrEmpty(deviceId))
            {
                return this.BadRequest();
            }

            if (events == null)
            {
                return this.BadRequest();
            }

            ServiceEventSource.Current.ServiceMessage(
                this.context,
                "Received {0} events from device {1}",
                events.Count(),
                deviceId);

            DeviceEvent evt = events.FirstOrDefault();

            if (evt == null)
            {
                return this.Ok();
            }

            DeviceEventSeries eventList = new DeviceEventSeries(deviceId, events);

            IReliableDictionary<string, DeviceEventSeries> storeInProgressMessage =  await this.stateManager.GetOrAddAsync<IReliableDictionary<string, DeviceEventSeries>>(TargetSolution.Names.EventLatestDictionaryName);
            IReliableDictionary<DateTimeOffset, DeviceEventSeries> storeCompletedMessages = await this.stateManager.GetOrAddAsync<IReliableDictionary<DateTimeOffset, DeviceEventSeries>>(TargetSolution.Names.EventHistoryDictionaryName);
            IReliableDictionary<string, EdgeDevice> storeDeviceCounters = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, EdgeDevice>>(TargetSolution.Names.EventCountsDictionaryName);


            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                DeviceEventSeries completedMessage = null; 

                await storeInProgressMessage.AddOrUpdateAsync(
                    tx,
                    deviceId,
                    eventList,
                    (key, currentValue) =>
                    {
                        return ManageDeviceEventSeriesContent(currentValue, eventList, out completedMessage);
                    });

                if(completedMessage != null)
                {
                    EdgeDevice device = null;

                    ConditionalValue<EdgeDevice> deviceValue = await storeDeviceCounters.TryGetValueAsync(tx, deviceId) ;

                    if (deviceValue.HasValue)
                        device = deviceValue.Value;
                    else
                        device = new EdgeDevice(deviceId);


                    bool tryAgain = true;

                    while( tryAgain )
                    {
                        tryAgain = await storeCompletedMessages.ContainsKeyAsync(tx, completedMessage.Timestamp);

                        if( tryAgain )
                        {
                            completedMessage.Timestamp.AddMilliseconds(1);
                        }
                        else
                        {
                            await storeCompletedMessages.AddOrUpdateAsync(
                                tx,
                                completedMessage.Timestamp,
                                completedMessage,
                                (key, currentValue) =>
                                {
                                    return completedMessage;
                                }
                           );
                        }
                    }

                    device.AddEventCount();

                    await storeDeviceCounters.AddOrUpdateAsync(
                        tx,
                        deviceId,
                        device,
                        (key, currentValue) =>
                        {
                            return device;
                        });
                }

                // Commit
                await tx.CommitAsync();
            }

            return this.Ok();
        }

        // PRIVATE METHODS
        private DeviceEventSeries ManageDeviceEventSeriesContent( DeviceEventSeries currentSeries, DeviceEventSeries newSeries, out DeviceEventSeries completedMessage )
        {
            bool resetCurrent = false;

            foreach( DeviceEvent item in currentSeries.Events)
            {
                if (item.SensorIndex == newSeries.Events.First().SensorIndex )
                {
                    resetCurrent = true;
                    break;
                }
            }

            if( resetCurrent )
            {
                completedMessage = currentSeries;
                currentSeries = newSeries;
            }
            else
            {
                completedMessage = null;
                currentSeries.AddEvent(newSeries.Events.First());
            }

            return currentSeries;
        }
    }
}
