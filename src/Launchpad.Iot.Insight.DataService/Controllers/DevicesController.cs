// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Insight.DataService.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Threading;
    using System.Threading.Tasks;
    using Iot.Insight.DataService.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.ServiceFabric.Data;
    using Microsoft.ServiceFabric.Data.Collections;
    using Microsoft.AspNetCore.Hosting;

    using global::Iot.Common;
    using TargetSolution;
    using Launchpad.Iot.PSG.Model;

    [Route("api/[controller]")]
    public class DevicesController : Controller
    {
        private readonly IApplicationLifetime appLifetime;
        private readonly IReliableStateManager stateManager;
        private readonly StatefulServiceContext context;

        public DevicesController(IReliableStateManager stateManager, StatefulServiceContext context, IApplicationLifetime appLifetime)
        {
            this.stateManager = stateManager;
            this.appLifetime = appLifetime;
            this.context = context;
        }


        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetAsync()
        {
            List<object> devices = new List<object>();
            IReliableDictionary<string, DeviceEventSeries> storeLastCompletedMessage = storeLastCompletedMessage = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, DeviceEventSeries>>(TargetSolution.Names.EventLatestDictionaryName);

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                try
                {
                    IAsyncEnumerable<KeyValuePair<string, DeviceEventSeries>> enumerable = await storeLastCompletedMessage.CreateEnumerableAsync(tx,EnumerationMode.Ordered);
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
                    await tx.CommitAsync();
                }
                catch (TimeoutException te)
                {
                    // transient error. Could Retry if one desires .
                    ServiceEventSource.Current.ServiceMessage(this.context, $"DataService - GetAsync - TimeoutException : Message=[{te.ToString()}]");
                    tx.Abort();
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.ServiceMessage(this.context, $"DataService - GetAsync - General Exception - Message=[{0}]", ex);
                    tx.Abort();
                }
            }

            return this.Ok(devices);
        }

        [HttpGet]
        [Route("history/interval/{searchInterval}")]
        [Route("history/{deviceId}/interval/{searchInterval}")]
        public async Task<IActionResult> SearchDevicesHistory( string deviceId = null, int searchInterval = 86400000)
        {
            List<object> deviceMessages = new List<object>();
            IReliableDictionary<DateTimeOffset, DeviceEventSeries> storeCompletedMessages = storeCompletedMessages = await this.stateManager.GetOrAddAsync<IReliableDictionary<DateTimeOffset, DeviceEventSeries>>(TargetSolution.Names.EventHistoryDictionaryName);
 
            if( storeCompletedMessages != null )
            {
               DateTimeOffset intervalToSearch = DateTimeOffset.UtcNow.AddMilliseconds(searchInterval * (-1));

               using (ITransaction tx = this.stateManager.CreateTransaction())
                {
                    IAsyncEnumerable<KeyValuePair<DateTimeOffset, DeviceEventSeries>> enumerable = await storeCompletedMessages.CreateEnumerableAsync(tx, key => key > intervalToSearch, EnumerationMode.Ordered);
                    IAsyncEnumerator<KeyValuePair<DateTimeOffset, DeviceEventSeries>> enumerator = enumerable.GetAsyncEnumerator();

                    while (await enumerator.MoveNextAsync(appLifetime.ApplicationStopping))
                    {
                        deviceMessages.Add(
                            new
                            {
                                DeviceId = enumerator.Current.Value.DeviceId,
                                enumerator.Current.Value.Events
                            });
                    }
                    await tx.CommitAsync();
                }
            }

            return this.Ok(deviceMessages);
        }

        [HttpGet]
        [Route("queue/length")]
        public async Task<IActionResult> GetQueueLengthAsync()
        {
            long count = -1;
            IReliableDictionary<DateTimeOffset, DeviceEventSeries> storeCompletedMessages = await this.stateManager.GetOrAddAsync<IReliableDictionary<DateTimeOffset, DeviceEventSeries>>(TargetSolution.Names.EventHistoryDictionaryName);

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                try
                {
                    count = await storeCompletedMessages.GetCountAsync(tx);
                    await tx.CommitAsync();

                    count += EventsController.getCurentStoreCacheCount();
                }
                catch (TimeoutException te)
                {
                    // transient error. Could Retry if one desires .
                    ServiceEventSource.Current.ServiceMessage(this.context, $"DataService - GetQueueLengthAsync - TimeoutException : Message=[{te.ToString()}]");
                    tx.Abort();
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.ServiceMessage(this.context, $"DataService - GetQueueLengthAsync - General Exception - Message=[{0}]", ex);
                    tx.Abort();
                }
            }

            return this.Ok(count);
        }

        // PRIVATE Methods
        public class StateManagerHelper<TKeyType,TValueType> where TKeyType : IEquatable<TKeyType> , IComparable<TKeyType>
        {
            public static async Task<List<KeyValuePair<TKeyType, TValueType>>> GetAllObjectsFromStateManagerFor(StatefulServiceContext context,ITransaction tx, IReliableStateManager stateManager, string dictionaryName, IApplicationLifetime appLifetime)
            {
                List< KeyValuePair <TKeyType, TValueType >> listRet = new List<KeyValuePair<TKeyType, TValueType>>();
                
                IReliableDictionary <TKeyType, TValueType> dictionary = await stateManager.GetOrAddAsync<IReliableDictionary<TKeyType, TValueType>>(dictionaryName);

                using (tx)
                {
                    try
                    {
                        IAsyncEnumerable<KeyValuePair<TKeyType, TValueType>> enumerable = await dictionary.CreateEnumerableAsync(tx);
                        IAsyncEnumerator<KeyValuePair<TKeyType, TValueType>> enumerator = enumerable.GetAsyncEnumerator();

                        while (await enumerator.MoveNextAsync(appLifetime.ApplicationStopping))
                        {
                            if (enumerator.Current.Value.GetType() == typeof(TValueType))
                            {
                                listRet.Add(new KeyValuePair<TKeyType, TValueType>(enumerator.Current.Key, (TValueType)enumerator.Current.Value));
                            }

                        }
                    }
                    catch (TimeoutException te)
                    {
                        // transient error. Could Retry if one desires .
                        ServiceEventSource.Current.ServiceMessage( context, $"DataService - GetAllObjectsFromStateManagerFor - TimeoutException : Message=[{te.ToString()}]");
                    }
                    catch (Exception ex)
                    {
                        ServiceEventSource.Current.ServiceMessage( context, $"DataService - GetAllObjectsFromStateManagerFor - General Exception - Message=[{0}]", ex);
                        tx.Abort();
                    }

                    return listRet;
                }
            }

        }
    }
}
