// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Admin.WebService.Models
{
    public class EventsProcessorApplicationParams
    {
        public EventsProcessorApplicationParams(string dataServiceURLs, string iotHubConnectionString, string iotHubProcessOnlyFutureEvents, int partitionCount, string version)
        {
            this.DataServiceURLs = dataServiceURLs;
            this.IotHubConnectionString = iotHubConnectionString;
            this.IotHubProcessOnlyFutureEvents = iotHubProcessOnlyFutureEvents;
            this.Version = version;
        }

        public string DataServiceURLs { get; set; }

        public string IotHubConnectionString { get; set; }

        public string IotHubProcessOnlyFutureEvents { get; set;  }

        public string Version { get; set; }
    }
}
