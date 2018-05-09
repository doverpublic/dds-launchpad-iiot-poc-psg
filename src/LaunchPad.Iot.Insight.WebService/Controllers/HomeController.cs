// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Insight.WebService.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Configuration;
    using System.Diagnostics;
    using System.Fabric;
    using System.Fabric.Query;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using System.Net.Http;
    using System.Linq;
    using System.Net.Http.Headers;

    using Newtonsoft.Json;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;

    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.PowerBI.Api.V2;
    using Microsoft.PowerBI.Api.V2.Models;
    using Microsoft.Rest;

    using global::Iot.Common;
    using global::Iot.Common.REST;
    using Iot.Insight.WebService.ViewModels;

    using Launchpad.Iot.PSG.Model;

    public class HomeController : Controller
    {
        private readonly FabricClient fabricClient;
        private readonly IApplicationLifetime appLifetime;
        private readonly HttpClient httpClient;

        private readonly StatelessServiceContext context;
        private static NameValueCollection appSettings = ConfigurationManager.AppSettings;

        private static readonly string Username = appSettings["pbiUsername"];
        private static readonly string Password = appSettings["pbiPassword"];
        private static readonly string AuthorityUrl = appSettings["authorityUrl"];
        private static readonly string ResourceUrl = appSettings["resourceUrl"];
        private static readonly string ClientId = appSettings["clientId"];
        private static readonly string ApiUrl = appSettings["apiUrl"];
        private static readonly string GroupId = appSettings["groupId"];
        private static readonly string ReportId = appSettings["reportId"];
        private static readonly string DatasetId = appSettings["datasetId"];

        private static readonly string DevicesDataStream01URL = "https://api.powerbi.com/beta/3d2d2b6f-061a-48b6-b4b3-9312d687e3a1/datasets/ac227ec0-5bfe-4184-85b1-a9643778f1e4/rows?key=zrg4K1om2l4mj97GF6T3p0ze3SlyynHWYRQMdUUSC0BWetzC7bF3RZgPMG4ukznAhGub5aPsDXuQMq540X8hZA%3D%3D";

        public HomeController(StatelessServiceContext context, FabricClient fabricClient, HttpClient httpClient, IApplicationLifetime appLifetime )
        {
            this.context = context;
            this.fabricClient = fabricClient;
            this.httpClient = httpClient;
            this.appLifetime = appLifetime;
        }

        [HttpGet]
        [Route("")]
        public IActionResult Index()
        {
            // Manage session
            HttpServiceUriBuilder contextUri = new HttpServiceUriBuilder().SetServiceName(this.context.ServiceName);

            // if there is an ongoing session this method will make sure to pass along the session information
            // to the view 
            HTTPHelper.IsSessionExpired(HttpContext, this);

            this.ViewData["TargetSite"] = contextUri.GetServiceNameSite();
            this.ViewData["PageTitle"] = "Home";
            this.ViewData["HeaderTitle"] = "Vibration Device Insights";

            ViewBag.Message = "";
            return View("Index");
        }

        [HttpGet]
        [Route("/healthProbe")]
        public IActionResult HealthProbe()
        {
            ServiceEventSource.Current.Message("Insight Webservice - Health Probe From Azure");

            return Ok();
        }

        [HttpGet]
        [Route("run/report")]
        [Route("run/report/parm/{reportParm}")]
        public async Task<IActionResult> EmbedReport( string reportParm = null)
        {
            // Manage session and Context
            HttpServiceUriBuilder contextUri = new HttpServiceUriBuilder().SetServiceName(this.context.ServiceName);

            if (HTTPHelper.IsSessionExpired(HttpContext,this))
            {
                return Ok(contextUri.GetServiceNameSiteHomePath());
            }
            else
            {
                this.ViewData["TargetSite"] = contextUri.GetServiceNameSite();
                this.ViewData["PageTitle"] = "Report";
                this.ViewData["HeaderTitle"] = "Last Posted Events";

                string reportUniqueId = FnvHash.GetUniqueId();


                EmbedConfig task = await EmbedReportConfigData(reportUniqueId, reportParm);
                this.ViewData["EmbedToken"] = task.EmbedToken.Token;
                this.ViewData["EmbedURL"] = task.EmbedUrl;
                this.ViewData["EmbedId"] = task.Id;
                this.ViewData["ReportUniqueId"] = reportUniqueId;

                return this.View();
            }
        }

        [HttpGet]
        [Route("run/streamReport/parm/{reportUrl}")]
        public IActionResult EmbedStreamReport(string reportUrl)
        {
            // Manage session and Context
            HttpServiceUriBuilder contextUri = new HttpServiceUriBuilder().SetServiceName(this.context.ServiceName);

            if (HTTPHelper.IsSessionExpired(HttpContext, this))
            {
                return Ok(contextUri.GetServiceNameSiteHomePath());
            }
            else
            {
                this.ViewData["EmbedURL"] = reportUrl;
                return this.View();
            }
        }

        [HttpPost]
        [Route("[Controller]/login")]
        public ActionResult Login(UserProfile objUser)
        {
            // Manage session and Context
            HttpServiceUriBuilder contextUri = new HttpServiceUriBuilder().SetServiceName(this.context.ServiceName);

            if (ModelState.IsValid)
            {
                ViewBag.Message = "";
                bool newUserRegistration = false;
                bool userAllowedToLogin = false;

                if ((objUser.Password != null && objUser.Password.Length > 0) )
                {
                    // First let deal to see if this a user registration
                    if (objUser.FirstName != null)
                    {
                        newUserRegistration = true;
                        Task<bool> result = RESTHandler.ExecuteFabricPOSTForEntity(typeof(UserProfile),
                                                    Names.InsightDataServiceName,
                                                    "api/entities/user/withIdentity/" + objUser.UserName,
                                                    "user",
                                                    objUser,
                                                    this.context,
                                                    this.httpClient,                                                 
                                                    this.appLifetime.ApplicationStopping,
                                                    ServiceEventSource.Current);
                        if (result.Result)
                            userAllowedToLogin = true;
                        else
                            ViewBag.Message = "Error during new user registration";
                    }

                    if (!userAllowedToLogin && !newUserRegistration)
                    {
                        Task<object> userObject = RESTHandler.ExecuteFabricGETForEntity(typeof(UserProfile),
                                                    Names.InsightDataServiceName,
                                                    "api/entities/user/byIdentity/" + objUser.UserName,
                                                    "user",
                                                    this.context,
                                                    this.httpClient,
                                                    this.appLifetime.ApplicationStopping,
                                                    ServiceEventSource.Current);
                        if (userObject != null)
                        {
                            UserProfile userProfile = (UserProfile)userObject.Result;

                            if (objUser.Password.Equals(userProfile.Password))
                                userAllowedToLogin = true;
                            else
                                ViewBag.Message = "Invalid Username and/or Password";
                        }
                        else
                        {
                            ViewBag.Message = "Error checking user credentials";
                        }
                    }

                    if (userAllowedToLogin)
                    {
                            try
                        {
                            string redirectTo = HTTPHelper.StartSession(HttpContext, this, objUser, "User", "/api/devices", contextUri.GetServiceNameSiteHomePath());

                            //TODO : make the redirection configurable as part of insight application
                            return Redirect(redirectTo);
                        }
                        catch (System.Exception ex)
                        {
                            ViewBag.Message = "Internal Error During User Login- Report to the System Administrator";
                            Console.WriteLine("On Login Session exception msg=[" + ex.Message + "]");
                        }
                    }
                }
                else
                {
                    ViewBag.Message = "Either username and/or password not provided";
                }
            }

            if (!HTTPHelper.IsSessionExpired(HttpContext, this))
                HTTPHelper.EndSession(HttpContext, this);

            return View( "Index", objUser );
        }

        [HttpGet]
        [Route("[Controller]/logout")]
        public IActionResult Logout()
        {
            HttpServiceUriBuilder contextUri = new HttpServiceUriBuilder().SetServiceName(this.context.ServiceName);

            // Manage session
            if (!HTTPHelper.IsSessionExpired(HttpContext, this))
                HTTPHelper.EndSession(HttpContext, this);
            return Ok(contextUri.GetServiceNameSiteHomePath());
        }

        public IActionResult About()
        {
            // Manage session
            string sessionId = HTTPHelper.GetCookieValueFor(HttpContext, SessionManager.GetSessionCookieName());

            this.ViewData["Message"] = "Your application description page.";

            return this.View();
        }

        public IActionResult Contact()
        {
            // Manage session
            string sessionId = HTTPHelper.GetCookieValueFor(HttpContext, SessionManager.GetSessionCookieName());

            this.ViewData["Message"] = "Your contact page.";

            return this.View();
        }

        public IActionResult Error()
        {
            return this.View();
        }


        // PRIVATE METHODS
        private async Task<EmbedConfig> EmbedReportConfigData(string reportUniqueId, string reportParm)
        {
            var result = new EmbedConfig();
            var username = "";
            var roles = "";

            try
            {
                var error = GetWebConfigErrors();
                if (error != null)
                {
                    result.ErrorMessage = error;
                    return result;
                }

                // Create a user password cradentials.
                var credential = new UserPasswordCredential(Username, Password);

                // Authenticate using created credentials
                var authenticationContext = new AuthenticationContext(AuthorityUrl);
                var authenticationResult = await authenticationContext.AcquireTokenAsync(ResourceUrl, ClientId, credential);

                if (authenticationResult == null)
                {
                    result.ErrorMessage = "Authentication Failed.";
                    return result;
                }

                var tokenCredentials = new TokenCredentials(authenticationResult.AccessToken, "Bearer");

                // Create a Power BI Client object. It will be used to call Power BI APIs.
                using (var client = new PowerBIClient(new Uri(ApiUrl), tokenCredentials))
                {
                    // Get a list of reports.
                    var reports = await client.Reports.GetReportsInGroupAsync(GroupId);

                    Report report;
                    if (string.IsNullOrEmpty(ReportId))
                    {
                        // Get the first report in the group.
                        report = reports.Value.FirstOrDefault();
                    }
                    else
                    {
                        report = reports.Value.FirstOrDefault(r => r.Id == ReportId);
                    }

                    if (report == null)
                    {
                        result.ErrorMessage = $"PowerBI Group has no report registered for id[{ReportId}].";
                        return result;
                    }

                    var datasets = await client.Datasets.GetDatasetByIdInGroupAsync(GroupId, report.DatasetId);
                    result.IsEffectiveIdentityRequired = datasets.IsEffectiveIdentityRequired;
                    result.IsEffectiveIdentityRolesRequired = datasets.IsEffectiveIdentityRolesRequired;
                    GenerateTokenRequest generateTokenRequestParameters;
                    // This is how you create embed token with effective identities
                    if (!string.IsNullOrEmpty(username))
                    {
                        var rls = new EffectiveIdentity(username, new List<string> { report.DatasetId });
                        if (!string.IsNullOrWhiteSpace(roles))
                        {
                            var rolesList = new List<string>();
                            rolesList.AddRange(roles.Split(','));
                            rls.Roles = rolesList;
                        }
                        // Generate Embed Token with effective identities.
                        generateTokenRequestParameters = new GenerateTokenRequest(accessLevel: "view", identities: new List<EffectiveIdentity> { rls });
                    }
                    else
                    {
                        // Generate Embed Token for reports without effective identities.
                        generateTokenRequestParameters = new GenerateTokenRequest(accessLevel: "view");
                    }

                    var tokenResponse = await client.Reports.GenerateTokenInGroupAsync(GroupId, report.Id, generateTokenRequestParameters);

                    if (tokenResponse == null)
                    {
                        ServiceEventSource.Current.ServiceMessage(this.context, $"Embed Report - Error during user authentication for report - Result=[{tokenResponse.ToString()}]");
                        result.ErrorMessage = "Failed to authenticate user for report request";
                        ViewBag.Message = "Failed to authenticate user for report request";
                        return result;
                    }

                    // Now it is time to refresh the data set

                    List<DeviceViewModelList> deviceViewModelList = await DevicesController.GetDevicesDataAsync(reportParm, httpClient, fabricClient, appLifetime );


/*                    List<DeviceViewModelList> deviceViewModelList = new List<DeviceViewModelList>();
                    ServiceUriBuilder uriBuilder = new ServiceUriBuilder(Names.InsightDataServiceName);
                    Uri serviceUri = uriBuilder.Build();

                    // service may be partitioned.
                    // this will aggregate device IDs from all partitions
                    ServicePartitionList partitions = await fabricClient.QueryManager.GetPartitionListAsync(serviceUri);

                    foreach (Partition partition in partitions)
                    {
                        Uri getUrl = new HttpServiceUriBuilder()
                            .SetServiceName(serviceUri)
                            .SetPartitionKey(((Int64RangePartitionInformation)partition.PartitionInformation).LowKey)
                            .SetServicePathAndQuery($"/api/devices{reportParm}")
                            .Build();

                        HttpResponseMessage response = await httpClient.GetAsync(getUrl, appLifetime.ApplicationStopping);

                        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            JsonSerializer serializer = new JsonSerializer();
                            using (StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync()))
                            {
                                using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
                                {
                                    List<DeviceViewModelList> localResult = serializer.Deserialize<List<DeviceViewModelList>>(jsonReader);

                                    if (localResult != null)
                                    {
                                        foreach (DeviceViewModelList device in localResult)
                                        {
                                            if (device.DeviceId.Equals(device.DeviceId, StringComparison.InvariantCultureIgnoreCase))
                                                deviceViewModelList.Add(device);
                                        }
                                    }
                                }
                            }
                        }
                    }
*/

                    var refreshDataresult = await ReportsDataHandler.PublishReportDataFor(reportUniqueId, DevicesDataStream01URL, deviceViewModelList, context, httpClient, appLifetime.ApplicationStopping, ServiceEventSource.Current );

                    if (refreshDataresult)
                    {
                        // Generate Embed Configuration.
                        result.EmbedToken = tokenResponse;
                        result.EmbedUrl = report.EmbedUrl;
                        result.Id = report.Id;
                    }
                    else
                    {
                        ServiceEventSource.Current.ServiceMessage(this.context, $"Embed Report - Error during the data report refresh - Result=[{refreshDataresult.ToString()}]");
                        result.ErrorMessage = "Error during reporting data refresh";
                        ViewBag.Message = "Error during new report refresh";
                    }

                    return result;
                }
            }
            catch (HttpOperationException exc)
            {
                result.ErrorMessage = string.Format($"Status: {exc.Response.StatusCode} Response Content: [{exc.Response.Content}] RequestId: {exc.Response.Headers["RequestId"].FirstOrDefault()}");
            }
            catch (Exception exc)
            {
                result.ErrorMessage = exc.ToString();
            }

            return result;
        }

        private string GetWebConfigErrors()
        {
            // Client Id must have a value.
            if (string.IsNullOrEmpty(ClientId))
            {
                return "ClientId is empty. please register your application as Native app in https://dev.powerbi.com/apps and fill client Id in web.config.";
            }

            // Client Id must be a Guid object.
            if (!Guid.TryParse(ClientId, out Guid result))
            {
                return "ClientId must be a Guid object. please register your application as Native app in https://dev.powerbi.com/apps and fill client Id in web.config.";
            }

            // Group Id must have a value.
            if (string.IsNullOrEmpty(GroupId))
            {
                return "GroupId is empty. Please select a group you own and fill its Id in web.config";
            }

            // Group Id must be a Guid object.
            if (!Guid.TryParse(GroupId, out result))
            {
                return "GroupId must be a Guid object. Please select a group you own and fill its Id in web.config";
            }

            // Username must have a value.
            if (string.IsNullOrEmpty(Username))
            {
                return "Username is empty. Please fill Power BI username in web.config";
            }

            // Password must have a value.
            if (string.IsNullOrEmpty(Password))
            {
                return "Password is empty. Please fill password of Power BI username in web.config";
            }

            return null;
        }


    }
}
