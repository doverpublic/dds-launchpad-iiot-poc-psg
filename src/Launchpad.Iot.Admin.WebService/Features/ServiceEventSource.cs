// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Admin.WebService
{
    using System.Diagnostics.Tracing;
    using global::Iot.Common;

    [EventSource(Name = "Microsoft-Launchpad.IoT.Admin.WebService")]
    internal sealed class ServiceEventSource : ServiceEventSourceBase { }
}
