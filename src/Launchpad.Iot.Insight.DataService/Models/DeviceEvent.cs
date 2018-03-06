// ------------------------------------------------------------
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
        public DeviceEvent(DateTimeOffset timestamp)
        {
            this.Timestamp = timestamp;
        }

        [DataMember]
        public DateTimeOffset Timestamp { get; private set; }
    }
}
