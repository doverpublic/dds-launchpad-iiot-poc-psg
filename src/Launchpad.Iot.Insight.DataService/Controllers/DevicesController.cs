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
            IReliableDictionary<string, DeviceEventSeries> storeLastCompletedMessage = null;

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                storeLastCompletedMessage = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, DeviceEventSeries>>(tx, TargetSolution.Names.EventLatestDictionaryName);
                await tx.CommitAsync();
            }

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                try
                {
                    IAsyncEnumerable<KeyValuePair<string, DeviceEventSeries>> enumerable = await storeLastCompletedMessage.CreateEnumerableAsync(tx);
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
        [Route("queue/length")]
        public async Task<IActionResult> GetQueueLengthAsync()
        {
            long count = -1;
            IReliableDictionary<string, EdgeDevice> storeDeviceCounters = null;

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                try
                {
                    storeDeviceCounters = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, EdgeDevice>>(tx, TargetSolution.Names.EventCountsDictionaryName);
                    await tx.CommitAsync();
                }
                catch (TimeoutException te)
                {
                    // transient error. Could Retry if one desires .
                    ServiceEventSource.Current.ServiceMessage(this.context, $"DataService - GetQueueLengthAsync - Creating Trasaction - TimeoutException : Message=[{te.ToString()}]");
                    tx.Abort();
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.ServiceMessage(this.context, $"DataService - GetQueueLengthAsync - Creating Trasaction - General Exception - Message=[{0}]", ex);
                    tx.Abort();
                }
            }

            if(storeDeviceCounters != null )
            {
                using (ITransaction tx = this.stateManager.CreateTransaction())
                {
                    try
                    {
                        IAsyncEnumerable<KeyValuePair<string, EdgeDevice>> enumerable = await storeDeviceCounters.CreateEnumerableAsync(tx);
                        IAsyncEnumerator<KeyValuePair<string, EdgeDevice>> enumerator = enumerable.GetAsyncEnumerator();

                        while (await enumerator.MoveNextAsync(appLifetime.ApplicationStopping))
                        {
                            count += enumerator.Current.Value.MessagesCount;
                        }
                        await tx.CommitAsync();
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
