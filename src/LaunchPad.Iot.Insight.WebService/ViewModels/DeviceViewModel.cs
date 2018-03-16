// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Insight.WebService.ViewModels
{
    using System;

    public class DeviceViewModel
    {
        public DeviceViewModel(string id, DateTimeOffset timestamp, string measurementType, int temperature, int batteryLevel, int dataPointsCount, int[] frequency, int[] magnitude )
        {
            this.Id = id;
            this.Timestamp = timestamp;
            this.MeasurementType = measurementType;
            this.Temperature = temperature;
            this.BatteryLevel = batteryLevel;
            this.DataPointsCount = dataPointsCount;
            this.Frequency = frequency;
            this.Magnitude = magnitude;
        }

        public string Id { get; private set; }

        public DateTimeOffset Timestamp { get; private set; }
        public string MeasurementType { get; private set; }
        public int      Temperature     { get; private set; }
        public int      BatteryLevel    { get; private set; }
        public int      DataPointsCount { get; private set; }
        public int[]    Frequency       { get; private set; }
        public int[]    Magnitude       { get; private set; }
    }
}
