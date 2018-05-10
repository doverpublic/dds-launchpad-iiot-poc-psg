using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Launchpad.Iot.PSG.Model
{
    public class DeviceEventRow
    {
        public DeviceEventRow( DateTimeOffset timestamp, string deviceId, string measurementType, int sensorIndex, int temperature, int batteryLevel, int dataPointsCount)
        {
            this.Timestamp = timestamp;
            this.DeviceId = deviceId;
            this.MeasurementType = measurementType;
            this.SensorIndex = sensorIndex;
            this.Temperature = temperature;
            this.BatteryLevel = batteryLevel;
            this.DataPointsCount = dataPointsCount;
        }

        public DateTimeOffset Timestamp { get; set; }
        public string DeviceId { get; set; }
        public string MeasurementType { get; set; }
        public int SensorIndex { get; set; }
        public int Temperature { get; set; }
        public int BatteryLevel { get; set; }
        public int DataPointsCount { get; set; }
    }
}
