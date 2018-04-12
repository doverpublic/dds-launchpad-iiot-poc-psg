// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Insight.DataService.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Iot.Insight.DataService.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.ServiceFabric.Data;
    using Microsoft.ServiceFabric.Data.Collections;
    using Microsoft.AspNetCore.Hosting;

    using global::Iot.Common;
    using TargetSolution;

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
            IReliableDictionary<string, DeviceEventSeries> storeInProgressMessage = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, DeviceEventSeries>>(TargetSolution.Names.EventLatestDictionaryName);

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
            IReliableDictionary<DateTimeOffset, DeviceEventSeries> storeCompletedMessages = await this.stateManager.GetOrAddAsync<IReliableDictionary<DateTimeOffset, DeviceEventSeries>>(TargetSolution.Names.EventHistoryDictionaryName);

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                long count = await storeCompletedMessages.GetCountAsync(tx);

                return this.Ok(count);
            }
        }

        // PRIVATE Methods
        public class StateManagerHelper<TKeyType,TValueType> where TKeyType : IEquatable<TKeyType> , IComparable<TKeyType>
        {
            public static async Task<List<KeyValuePair<TKeyType, TValueType>>> GetAllObjectsFromStateManagerFor(ITransaction tx, IReliableStateManager stateManager, string dictionaryName, IApplicationLifetime appLifetime)
            {
                List< KeyValuePair <TKeyType, TValueType >> listRet = new List<KeyValuePair<TKeyType, TValueType>>();
                
                IReliableDictionary <TKeyType, TValueType> dictionary = await stateManager.GetOrAddAsync<IReliableDictionary<TKeyType, TValueType>>(dictionaryName);

                using (tx)
                {
                    IAsyncEnumerable<KeyValuePair<TKeyType, TValueType>> enumerable = await dictionary.CreateEnumerableAsync(tx);
                    IAsyncEnumerator<KeyValuePair<TKeyType, TValueType>> enumerator = enumerable.GetAsyncEnumerator();

                    while (await enumerator.MoveNextAsync(appLifetime.ApplicationStopping))
                    {
                        if(enumerator.Current.Value.GetType() == typeof( TValueType) )
                        {
                            listRet.Add(new KeyValuePair<TKeyType, TValueType>(enumerator.Current.Key, (TValueType)enumerator.Current.Value));
                        }

                    }

                    return listRet;
                }
            }

        }
    }
}
