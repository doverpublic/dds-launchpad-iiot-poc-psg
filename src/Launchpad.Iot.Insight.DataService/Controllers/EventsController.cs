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

        private TaskSynchronizationScope _lock = new TaskSynchronizationScope();

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
                    "Data Service - Received a Really Bad Request - device id not defined" );
                return this.BadRequest();
            }

            if (events == null)
            {
                ServiceEventSource.Current.ServiceMessage(
                    this.context,
                    $"Data Service - Received Bad Request from device {deviceId}");

                return this.BadRequest();
            }

            ServiceEventSource.Current.ServiceMessage(
                this.context,
                $"Data Service - Received {events.Count()} events from device {deviceId}");

            DeviceEvent evt = events.FirstOrDefault();

            if (evt == null)
            {
                return this.Ok();
            }

            DeviceEventSeries eventList = new DeviceEventSeries(deviceId, events);

            IReliableDictionary<string, DeviceEventSeries> storeInProgressMessage = null;
            IReliableDictionary<DateTimeOffset, DeviceEventSeries> storeCompletedMessages = null;
            IReliableDictionary<string, EdgeDevice> storeDeviceCounters = null;

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                storeInProgressMessage = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, DeviceEventSeries>>(tx, TargetSolution.Names.EventInProgressDictionaryName);
                await tx.CommitAsync();
            }

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                storeCompletedMessages = await this.stateManager.GetOrAddAsync<IReliableDictionary<DateTimeOffset, DeviceEventSeries>>(tx, TargetSolution.Names.EventHistoryDictionaryName);
                await tx.CommitAsync();
            }

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                storeDeviceCounters = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, EdgeDevice>>(tx, TargetSolution.Names.EventCountsDictionaryName);
                await tx.CommitAsync();
            }

            string transactionType = "";
            try
            {
                int retryCounter = 1;
                DeviceEventSeries completedMessage = null;
                EdgeDevice device = null;

                while (retryCounter > 0)
                {
                    transactionType = "";
                    using (ITransaction tx = this.stateManager.CreateTransaction())
                    {
                        try
                        { 
                            transactionType = "In Progress Message";
                            ConditionalValue<EdgeDevice> deviceValue = await storeDeviceCounters.TryGetValueAsync(tx, deviceId, LockMode.Update);

                            if (deviceValue.HasValue)
                                device = deviceValue.Value;
                            else
                                device = new EdgeDevice(deviceId);

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
                                bool tryAgain = true;

                                while (tryAgain)
                                {
                                    tryAgain = await storeCompletedMessages.ContainsKeyAsync(tx, completedMessage.Timestamp);

                                    if (tryAgain)
                                    {
                                        completedMessage.Timestamp.AddMilliseconds(10);
                                        ServiceEventSource.Current.ServiceMessage(
                                            this.context,
                                            $"Data Service - Message with timestamp {completedMessage.Timestamp.ToString()} from device {deviceId} already present in the store");
                                    }
                                    else
                                    {
                                        transactionType = "Completed Message";
                                        await storeCompletedMessages.AddOrUpdateAsync(
                                            tx,
                                            completedMessage.Timestamp,
                                            completedMessage,
                                            (key, currentValue) =>
                                            {
                                                return completedMessage;
                                            }
                                        );
                                        ServiceEventSource.Current.ServiceMessage(
                                            this.context,
                                            $"Data Service - Saved Message with timestamp {completedMessage.Timestamp.ToString()} from device {deviceId}");
                                        break;
                                    }
                                }
                                device.AddEventCount();
                                device.AddMessageCount();

                                transactionType = "Device Counters For Message";
                                await storeDeviceCounters.AddOrUpdateAsync(
                                    tx,
                                    deviceId,
                                    device,
                                    (key, currentValue) =>
                                    {
                                        return device;
                                    });

                                retryCounter = 0;
                                await tx.CommitAsync();
                            }
                            else
                            {
                                device.AddEventCount();

                                transactionType = "Device Counters For Event";
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
                                    $"Data Service Received {events.Count()} events from device {deviceId} - Message not completed yet");
                                retryCounter = 0;   // this means we have saved the partial changes for the message each sensor message
                                await tx.CommitAsync();
                            }
                        }
                        catch (TimeoutException tex)
                        {
                            if(global::Iot.Common.Names.TransactionsRetryCount > retryCounter)
                            {
                                ServiceEventSource.Current.ServiceMessage(
                                    this.context,
                                    $"Data Service Timeout Exception when saving [{transactionType}] data from device {deviceId} - Iteration #{retryCounter} - Message-[{tex}]");

                                await Task.Delay(global::Iot.Common.Names.TransactionRetryWaitIntervalInMills * (int)Math.Pow(2,retryCounter));
                                retryCounter++;
                            }
                            else
                            {
                                ServiceEventSource.Current.ServiceMessage(
                                    this.context,
                                    $"Data Service Timeout Exception when saving [{transactionType}] data from device {deviceId} - Iteration #{retryCounter} - Transaction Aborted - Message-[{tex}]");

                                resultRet = this.BadRequest();
                                retryCounter = 0;
                            }
                        }
                    }
                }

                if( completedMessage != null )
                {
                    await StoreLastCompletedMessage(completedMessage);
                    ServiceEventSource.Current.ServiceMessage(
                        this.context,
                        $"Data Service Received {events.Count()} events from device {deviceId} - Message completed");
                }
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.ServiceMessage(
                    this.context,
                    $"Data Service Exception when saving [{transactionType}] data from device {deviceId} - Message-[{ex}]");
            }

            return resultRet;
        }

        // PRIVATE METHODS
       private async Task<bool> StoreLastCompletedMessage(DeviceEventSeries completedMessage )
       {
            int retryCounter = 1;
            string transactionType = "Completed Last Message";
            IReliableDictionary<string, DeviceEventSeries> storeLastCompletedMessage = null;

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                storeLastCompletedMessage = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, DeviceEventSeries>>(tx, TargetSolution.Names.EventLatestDictionaryName);
                await tx.CommitAsync();
            }

            while (retryCounter > 0)
            {
                using (ITransaction tx = this.stateManager.CreateTransaction())
                {
                    try
                    {
                        await storeLastCompletedMessage.AddOrUpdateAsync(
                            tx,
                            completedMessage.DeviceId,
                            completedMessage,
                            (key, currentValue) =>
                            {
                                return completedMessage;
                            }
                        );
                        retryCounter = 0;
                        await tx.CommitAsync();
                    }
                    catch (TimeoutException tex)
                    {
                        if (global::Iot.Common.Names.TransactionsRetryCount > retryCounter)
                        {
                            ServiceEventSource.Current.ServiceMessage(
                                this.context,
                                $"Data Service Timeout Exception when saving [{transactionType}] data from device {completedMessage.DeviceId} - Iteration #{retryCounter} - Message-[{tex}]");

                            await Task.Delay(global::Iot.Common.Names.TransactionRetryWaitIntervalInMills * (int)Math.Pow(2, retryCounter));
                            retryCounter++;
                        }
                        else
                        {
                            ServiceEventSource.Current.ServiceMessage(
                                this.context,
                                $"Data Service Timeout Exception when saving [{transactionType}] data from device {completedMessage.DeviceId} - Iteration #{retryCounter} - Transaction Aborted - Message-[{tex}]");

                            retryCounter = 0;
                        }
                    }
                }
            }
            return true;
        }

        public class TaskSynchronizationScope
        {
            private Task _currentTask;
            private readonly object _lock = new object();

            public Task RunAsync(Func<Task> task)
            {
                return RunAsync<object>(async () =>
                {
                    await task();
                    return null;
                });
            }

            public Task<T> RunAsync<T>(Func<Task<T>> task)
            {
                lock (_lock)
                {
                    if (_currentTask == null)
                    {
                        var currentTask = task();
                        _currentTask = currentTask;
                        return currentTask;
                    }
                    else
                    {
                        var source = new TaskCompletionSource<T>();
                        _currentTask.ContinueWith(t =>
                        {
                            var nextTask = task();
                            nextTask.ContinueWith(nt =>
                            {
                                if (nt.IsCompleted)
                                    source.SetResult(nt.Result);
                                else if (nt.IsFaulted)
                                    source.SetException(nt.Exception);
                                else
                                    source.SetCanceled();

                                lock (_lock)
                                {
                                    if (_currentTask.Status == TaskStatus.RanToCompletion)
                                        _currentTask = null;
                                }
                            });
                        });
                        _currentTask = source.Task;
                        return source.Task;
                    }
                }
            }
        }

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
