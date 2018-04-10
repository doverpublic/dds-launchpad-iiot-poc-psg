// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Insight.DataService.Models
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    using System.Linq;

    [DataContract]
    internal class DeviceEventSeries
    {
        private List<DeviceEvent> EventList;

        public DeviceEventSeries(string deviceId, IEnumerable<DeviceEvent> events)
        {
            this.DeviceId = deviceId;
            this.EventList = new List<DeviceEvent>();

            this.EventList.AddRange(events);

            this.Events = this.EventList;

            DeviceEvent firstEvent = events.FirstOrDefault();

            this.Timestamp = firstEvent.Timestamp;
        }


        [DataMember]
        public string DeviceId { get; private set; }

        [DataMember]
        public DateTimeOffset Timestamp { get; private set; }

        [DataMember]
        public IEnumerable<DeviceEvent> Events { get; private set; }

        
        public void AddEvent(DeviceEvent evt)
        {
            this.EventList.Add(evt);
        }

        public void AddEvents(IEnumerable<DeviceEvent> events)
        {
            this.EventList.AddRange(events);
        }

    }
}
