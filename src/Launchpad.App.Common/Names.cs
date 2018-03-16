// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using global::Iot.Common;

namespace Launchpad.App.Common
{
    public static class Names
    {
        public const string EventKeyFieldDeviceId                   = Iot.Common.Names.EVENT_KEY_DEVICE_ID;
        public const string EventKeyFieldTargetSite                  = Iot.Common.Names.EVENT_KEY_TARGET_SITE;

        public const string EventsProcessorApplicationPrefix        = "fabric:/Launchpad.Iot.EventsProcessor";
        public const string EventsProcessorApplicationTypeName      = "LaunchpadIotEventsProcessorApplicationType";
        public const string EventsProcessorRouterServiceName        = "RouterService";
        public const string EventsProcessorRouterServiceTypeName    = "RouterServiceType";

        public const string InsightApplicationNamePrefix            = "fabric:/Launchpad.Iot.Insight";
        public const string InsightApplicationTypeName              = "LaunchpadIotInsightApplicationType";
        public const string InsightDataServiceName                  = "DataService";
        public const string InsightDataServiceTypeName              = "DataServiceType";
        public const string InsightWebServiceName                   = "WebService";
        public const string InsightWebServiceTypeName               = "WebServiceType";
    }
}
