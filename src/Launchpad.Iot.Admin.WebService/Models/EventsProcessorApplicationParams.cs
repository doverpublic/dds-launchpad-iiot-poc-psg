// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Admin.WebService.Models
{
    public class EventsProcessorApplicationParams
    {
        public EventsProcessorApplicationParams(string iotHubConnectionString, string iotHubProcessOnlyFutureEvents, int partitionCount, string version)
        {
            this.IotHubConnectionString = iotHubConnectionString;
            this.Version = version;
            this.IotHubProcessOnlyFutureEvents = iotHubProcessOnlyFutureEvents;
        }

        public string IotHubConnectionString { get; set; }

        public string IotHubProcessOnlyFutureEvents { get; set;  }

        public string Version { get; set; }
    }
}
