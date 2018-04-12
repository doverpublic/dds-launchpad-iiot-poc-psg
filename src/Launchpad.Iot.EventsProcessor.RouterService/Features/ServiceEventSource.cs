// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.EventsProcessor.RouterService
{
    using System.Diagnostics.Tracing;
    using global::Iot.Common;

    [EventSource(Name = "Microsoft-Launchpad.Iot.EventsProcessor.RouterService")]
    internal sealed class ServiceEventSource : ServiceEventSourceBase { }
}
