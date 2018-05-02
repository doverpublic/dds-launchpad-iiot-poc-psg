using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Fabric;
using System.Fabric.Query;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;

using global::Iot.Common;
using global::Iot.Common.REST;


namespace Launchpad.Iot.PSG.Model
{
    public class ReportsDataHandlercs
    {
        private static readonly string DevicesDataStream01URL = "https://api.powerbi.com/beta/3d2d2b6f-061a-48b6-b4b3-9312d687e3a1/datasets/ac227ec0-5bfe-4184-85b1-a9643778f1e4/rows?key=zrg4K1om2l4mj97GF6T3p0ze3SlyynHWYRQMdUUSC0BWetzC7bF3RZgPMG4ukznAhGub5aPsDXuQMq540X8hZA%3D%3D";
        private static readonly string ServiceURL = "https://localhost:20081";

        public static async Task<bool> PublishReportDataFor( string reportUniqueId, string deviceId, string targetSite, ServiceContext serviceContext, HttpClient httpClient, IApplicationLifetime appLifetime, IServiceEventSource serviceEventSource, List<DeviceViewModelList> deviceViewModelList = null )
        {
            bool bRet = false;
            ServiceEventSourceHelper serviceEventSourceHelper = new ServiceEventSourceHelper(serviceEventSource);

            if (deviceViewModelList == null)
            {
                string serviceUrl = $"{ServiceURL}/{targetSite}/device/{deviceId}";

                object result = await RESTHandler.ExecuteHttpGET(typeof(List<DeviceViewModelList>), serviceUrl, httpClient, appLifetime, serviceEventSource);
                if (result != null)
                {
                    deviceViewModelList = (List<DeviceViewModelList>)result;
                }
            }

            if (deviceViewModelList != null)
            {
                 DateTimeOffset timestamp = DateTimeOffset.UtcNow; ;
                bool firstItem = true;
                List<DeviceReportModel> messages = new List<DeviceReportModel>();

                foreach (DeviceViewModelList deviceModel in deviceViewModelList)
                {
                    string devId = deviceModel.DeviceId;
                    IEnumerable<DeviceViewModel> evts = deviceModel.Events;
                    int batteryLevel = 0;
                    int batteryVoltage = 0;
                    int batteryMax = 4000;
                    int batteryMin = 0;
                    int batteryTarget = 3200;
                    int batteryPercentage = 0;
                    int batteryPercentageMax = 100;
                    int batteryPercentageMin = 0;
                    int batteryPercentageTarget = 15;
                    int temperature = 0;
                    int temperatureMax = 200;
                    int temperatureMin = -55;
                    int temperatureTarget = 55;
                    int dataPointsCount = 0;
                    string measurementType = "";
                    int sensorIndex = 0;
                    int frequency = 0;
                    int magnitude = 0;

                    foreach (DeviceViewModel sensorMessage in evts)
                    {
                        if (firstItem)
                        {
                            batteryLevel = sensorMessage.BatteryLevel;
                            timestamp = sensorMessage.Timestamp;
                            measurementType = sensorMessage.MeasurementType;
                            dataPointsCount = sensorMessage.DataPointsCount;
                            sensorIndex = sensorMessage.SensorIndex;

                            firstItem = false;
                        }

                        for (int index = 0; index < sensorMessage.Frequency.Length; index++)
                        {
                            frequency = sensorMessage.Frequency[index];
                            magnitude = sensorMessage.Magnitude[index];

                            messages.Add(new DeviceReportModel(reportUniqueId,
                                    timestamp,
                                    devId,
                                    batteryLevel,
                                    batteryVoltage,
                                    batteryMax,
                                    batteryMin,
                                    batteryTarget,
                                    batteryPercentage,
                                    batteryPercentageMax,
                                    batteryPercentageMin,
                                    batteryPercentageTarget,
                                    temperature,
                                    temperatureMax,
                                    temperatureMin,
                                    temperatureTarget,
                                    dataPointsCount,
                                    measurementType,
                                    sensorIndex,
                                    frequency,
                                    magnitude)
                             );
                        }
                    }

                    bRet = await RESTHandler.ExecuteHttpPOST(DevicesDataStream01URL, messages, httpClient, appLifetime, serviceEventSource);

                    if (!bRet)
                    {
                        serviceEventSourceHelper.ServiceMessage(serviceContext, $"Embed Report - Error during data push for report data");
                        break;
                    }
                }
            }

            return bRet;
        }
    }
}
