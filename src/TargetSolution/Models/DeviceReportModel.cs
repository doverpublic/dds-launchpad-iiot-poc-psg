// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------


using System;

namespace Launchpad.Iot.PSG.Model
{
    public class DeviceReportModel
    {

        public DeviceReportModel(string reportUniqueId,
                                    DateTimeOffset timestamp,
                                    string deviceId,
                                    int batteryLevel,
                                    int batteryVoltage,
                                    int batteryMax,
                                    int batteryMin,
                                    int batteryTarget,
                                    int batteryPercentage,
                                    int batteryPercentageMax,
                                    int batteryPercentageMin,
                                    int batteryPercentageTarget,
                                    int temperature,
                                    int temperatureMax,
                                    int temperatureMin,
                                    int temperatureTarget,
                                    int dataPointsCount,
                                    string measurementType,
                                    int sensorIndex,
                                    int frequency,
                                    int magnitude)
        {
            this.ReportUniqueId = reportUniqueId;
            this.Timestamp = timestamp;
            this.DeviceId = deviceId;
            this.BatteryLevel = batteryLevel;
            this.BatteryVoltage = batteryVoltage;
            this.BatteryMax = batteryMax;
            this.BatteryMin = batteryMin;
            this.BatteryTarget = batteryTarget;
            this.BatteryPercentage = batteryPercentage;
            this.BatteryPercentageMax = batteryPercentageMax;
            this.BatteryPercentageMin = batteryPercentageMin;
            this.BatteryPercentageTarget = batteryPercentageTarget;
            this.Temperature = temperature;
            this.TemperatureMax = temperatureMax;
            this.TemperatureMin = temperatureMin;
            this.TemperatureTarget = temperatureTarget;
            this.DataPointsCount = dataPointsCount;
            this.MeasurementType = measurementType;
            this.SensorIndex = sensorIndex;
            this.Frequency = frequency;
            this.Magnitude = magnitude;
        }

        public string ReportUniqueId { get; private set; }
        public DateTimeOffset Timestamp { get; private set; }
        public string DeviceId { get; private set; }
        public int BatteryLevel { get; private set; }
        public int BatteryVoltage { get; private set; }
        public int BatteryMax { get; private set; }
        public int BatteryMin { get; private set; }
        public int BatteryTarget { get; private set; }
        public int BatteryPercentage { get; private set; }
        public int BatteryPercentageMax { get; private set; }
        public int BatteryPercentageMin { get; private set; }
        public int BatteryPercentageTarget { get; private set; }
        public int Temperature { get; private set; }
        public int TemperatureMax { get; private set; }
        public int TemperatureMin { get; private set; }
        public int TemperatureTarget { get; private set; }
        public int DataPointsCount { get; private set; }
        public string MeasurementType { get; private set; }
        public int SensorIndex { get; private set; }
        public int Frequency { get; private set; }
        public int Magnitude { get; private set; }
    }
}
