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
            IActionResult resultRet = this.Ok();

            if (String.IsNullOrEmpty(deviceId))
            {
                ServiceEventSource.Current.ServiceMessage(
                    this.context,
                    "Data Service Received a Really Bad Request - device id not defined" );
                return this.BadRequest();
            }

            if (events == null)
            {
                ServiceEventSource.Current.ServiceMessage(
                    this.context,
                    "Data Service Received Bad Request from device {0}",
                    deviceId);

                return this.BadRequest();
            }

            ServiceEventSource.Current.ServiceMessage(
                this.context,
                "Data Service Received {0} events from device {1}",
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

            try
            {
                int retryCounter = 1;
                DeviceEventSeries completedMessage = null;
                EdgeDevice device = null;

                while (retryCounter > 0)
                {
                    using (ITransaction tx = this.stateManager.CreateTransaction())
                    {
                        try
                        {
                            await storeInProgressMessage.AddOrUpdateAsync(
                                    tx,
                                    deviceId,
                                    eventList,
                                    (key, currentValue) =>
                                    {
                                        return ManageDeviceEventSeriesContent(currentValue, eventList, out completedMessage);
                                    });

                            if (completedMessage != null)
                            {
                                ConditionalValue<EdgeDevice> deviceValue = await storeDeviceCounters.TryGetValueAsync(tx, deviceId);

                                if (deviceValue.HasValue)
                                    device = deviceValue.Value;
                                else
                                    device = new EdgeDevice(deviceId);


                                bool tryAgain = true;

                                while (tryAgain)
                                {
                                    tryAgain = await storeCompletedMessages.ContainsKeyAsync(tx, completedMessage.Timestamp);

                                    if (tryAgain)
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
                                ServiceEventSource.Current.ServiceMessage(
                                    this.context,
                                    "Data Service Received {0} events from device {1} - Message completed",
                                    events.Count(),
                                    deviceId);
                                retryCounter = 0;
                                await tx.CommitAsync();
                            }
                            else
                            {
                                retryCounter = 0;   // this means we have saved the partial changes for the message each sensor message
                                await tx.CommitAsync();
                            }
                        }
                        catch (TimeoutException tex)
                        {
                            if(global::Iot.Common.Names.TransactionsRetryCount < retryCounter)
                            {
                                ServiceEventSource.Current.ServiceMessage(
                                    this.context,
                                    "Data Service Timeout Exception when saving data from device {0} - Iteration #{1} - Message-[{2}]",
                                    deviceId,
                                    retryCounter,
                                    tex);

                                await Task.Delay(100);
                                retryCounter++;
                            }
                            else
                            {
                                ServiceEventSource.Current.ServiceMessage(
                                    this.context,
                                    "Data Service Timeout Exception when saving data from device {0} - Iteration #{1} - Transaction Aborted - Message-[{2}]",
                                    deviceId,
                                    retryCounter,
                                    tex);

                                resultRet = this.BadRequest();
                                retryCounter = 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.ServiceMessage(
                    this.context,
                    "Data Service Exception when saving data from device {0} - Message-[{1}]",
                    deviceId,
                    ex);
                resultRet = this.BadRequest();
            }

            return resultRet;
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
