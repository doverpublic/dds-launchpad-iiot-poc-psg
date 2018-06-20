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
         public DeviceEventSeries(string deviceId, IEnumerable<DeviceEvent> events)
        {
            this.DeviceId = deviceId;
            this.Events = new List<DeviceEvent>();

            foreach (DeviceEvent evnt in events)
                this.Events.Add(new DeviceEvent(evnt.Timestamp, evnt.MeasurementType, evnt.SensorIndex, evnt.TempExternal, evnt.TempInternal, evnt.BatteryLevel, evnt.DataPointsCount, evnt.Frequency, evnt.Magnitude));

            DeviceEvent firstEvent = events.FirstOrDefault();

            this.Timestamp = firstEvent.Timestamp;
        }


        [DataMember]
        public string DeviceId { get; private set; }

        [DataMember]
        public DateTimeOffset Timestamp { get; set; }

        [DataMember]
        public List<DeviceEvent> Events { get; private set; }

        
        public void AddEvent(DeviceEvent evt)
        {
            if (this.Events == null)
                this.Events = new List<DeviceEvent>();

            this.Events.Add(evt);
        }

        public void AddEvents(IEnumerable<DeviceEvent> events)
        {
            if (this.Events == null)
                this.Events = new List<DeviceEvent>();

            this.Events.AddRange(events);
        }

    }
}
