using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
    public class ReportsDataHandler
    {
        public static async Task<bool> PublishReportDataFor( string reportUniqueId, string publishUrl, List<DeviceViewModelList> deviceViewModelList, ServiceContext serviceContext, HttpClient httpClient, CancellationToken cancellationToken, IServiceEventSource serviceEventSource )
        {
            bool bRet = false;
            ServiceEventSourceHelper serviceEventSourceHelper = new ServiceEventSourceHelper(serviceEventSource);

            if (deviceViewModelList.Count > 0)
            {
                DateTimeOffset timestamp = DateTimeOffset.UtcNow; ;
                int messageCounter = 0;
                List<DeviceReportModel> messages = new List<DeviceReportModel>();

                foreach (DeviceViewModelList deviceModel in deviceViewModelList)
                {
                    bool firstItem = true;
                    string devId = deviceModel.DeviceId;
                    IEnumerable<DeviceViewModel> evts = deviceModel.Events;
                    int batteryLevel = 3300;
                    int batteryVoltage = 0;
                    int batteryMax = 4;
                    int batteryMin = 2;
                    int batteryTarget = 3;
                    int batteryPercentage = 30;
                    int batteryPercentageMax = 100;
                    int batteryPercentageMin = 0;
                    int batteryPercentageTarget = 15;
                    int temperatureExternal = 0;
                    int temperatureExternalMax = 200;
                    int temperatureExternalMin = -50;
                    int temperatureExternalTarget = 60;
                    int temperatureInternal = 0;
                    int temperatureInternalMax = 200;
                    int temperatureInternalMin = -50;
                    int temperatureInternalTarget = 60;
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
                            batteryVoltage = batteryLevel / 1000;

                            if (batteryLevel < 2800)
                                batteryPercentage = 0;
                            else if (batteryLevel > 3600)
                                batteryPercentage = 100;
                            else
                                batteryPercentage = (batteryLevel - 2800) / 10;

                            timestamp = sensorMessage.Timestamp;
                            measurementType = sensorMessage.MeasurementType;
                            dataPointsCount = sensorMessage.DataPointsCount;
                            sensorIndex = sensorMessage.SensorIndex;
                            temperatureExternal = sensorMessage.TempExternal;
                            temperatureInternal = sensorMessage.TempInternal;

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
                                    temperatureExternal,
                                    temperatureExternalMax,
                                    temperatureExternalMin,
                                    temperatureExternalTarget,
                                    temperatureInternal,
                                    temperatureInternalMax,
                                    temperatureInternalMin,
                                    temperatureInternalTarget,
                                    dataPointsCount,
                                    measurementType,
                                    sensorIndex,
                                    frequency,
                                    magnitude)
                             );
                            messageCounter++;

                            if( messageCounter == 9000)
                            {
                                await RESTHandler.ExecuteHttpPOST(publishUrl, messages, httpClient, cancellationToken, serviceEventSource);

                                messages.Clear();
                                messageCounter = 0;
                            }
                        }
                    }

                    if ( messageCounter > 0 )
                        await RESTHandler.ExecuteHttpPOST(publishUrl, messages, httpClient, cancellationToken, serviceEventSource);
                }
                bRet = true;
            }
            else
            {
                serviceEventSourceHelper.ServiceMessage(serviceContext, $"Embed Report - No data found to publish to [{publishUrl}]");
            }

            return bRet;
        }
    }
}
