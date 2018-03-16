﻿// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Insight.DataService.Models
{
    using System;
    using System.Runtime.Serialization;

    [DataContract]
    public class DeviceEvent
    {
        public DeviceEvent(DateTimeOffset timestamp, int temperature, int batteryLevel, int dataPointsCount, int[] frequency, int[] magnitude )
        {
            this.Timestamp = timestamp;
            this.Temperature = temperature;
            this.BatteryLevel = batteryLevel;
            this.DataPointsCount = dataPointsCount;
            this.Frequency = frequency;
            this.Magnitude = magnitude;
        }

        [DataMember]
        public DateTimeOffset Timestamp { get; private set; }
        [DataMember]
        public int Temperature { get; private set; }
        [DataMember]
        public int BatteryLevel { get; private set; }
        [DataMember]
        public int DataPointsCount { get; private set; }
        [DataMember]
        public int[] Frequency { get; private set; }
        [DataMember]
        public int[] Magnitude { get; private set; }
    }
}
