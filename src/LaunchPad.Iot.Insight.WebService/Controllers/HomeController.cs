// ------------------------------------------------------------
//  Copyright (c) Dover Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Launchpad.Iot.Insight.WebService.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Collections.Specialized;
    using System.Threading.Tasks;
    using System.Fabric;
    using System.Linq;
    using System.Web;

    using Microsoft.AspNetCore.Mvc;

    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.PowerBI.Api.V2;
    using Microsoft.PowerBI.Api.V2.Models;
    using Microsoft.Rest;

    using global::Iot.Common;
    using Iot.Insight.WebService.ViewModels;

  
    public class HomeController : Controller
    {
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


        public HomeController(StatelessServiceContext context)
        {
            this.context = context;
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
        [Route("run/report")]
        public async Task<IActionResult> EmbedReport()
        {
            // Manage session and Context
            HttpServiceUriBuilder contextUri = new HttpServiceUriBuilder().SetServiceName(this.context.ServiceName);

            if (HTTPHelper.IsSessionExpired(HttpContext,this))
            {
                return Redirect(contextUri.GetServiceNameSiteHomePath());
            }
            else
            {
                this.ViewData["TargetSite"] = contextUri.GetServiceNameSite();
                this.ViewData["PageTitle"] = "Report";
                this.ViewData["HeaderTitle"] = "Last Posted Events";

                EmbedConfig task = await EmbedReportConfigData();
                this.ViewData["EmbedToken"] = task.EmbedToken.Token;
                this.ViewData["EmbedURL"] = task.EmbedUrl;
                this.ViewData["EmbedId"] = task.Id;

                return this.View();
            }
        }

        [HttpPost]
        [Route("[Controller]/login")]
        [ValidateAntiForgeryToken]
        public ActionResult Login(UserProfile objUser)
        {
            // Manage session and Context
            HttpServiceUriBuilder contextUri = new HttpServiceUriBuilder().SetServiceName(this.context.ServiceName);

            if (ModelState.IsValid)
            {
                ViewBag.Message = "";
                if (objUser.Password != null && objUser.Password.Length > 0)
                {
                    try
                    {
                        string redirectTo = HTTPHelper.StartSession(HttpContext, this, objUser, "User", "/api/devices", contextUri.GetServiceNameSiteHomePath());

                        //TODO : make the redirection configurable as part of insight application
                        return Redirect(redirectTo);
                    }
                    catch ( System.Exception ex )
                    {
                        ViewBag.Message = "Internal Error During User Login- Report to the System Administrator";
                        Console.WriteLine("On Login Session exception msg=[" + ex.Message + "]");
                    }
                }
                else
                {
                    if (!HTTPHelper.IsSessionExpired(HttpContext, this))
                        HTTPHelper.EndSession(HttpContext, this);

                    ViewBag.Message = "Invalid username and/or password";
                }
            }
            return View( "Index", objUser );
        }

        [HttpPost]
        [Route("[Controller]/logout")]
        [ValidateAntiForgeryToken]
        public ActionResult Logout(UserProfile objUser)
        {
            // Manage session
            if (ModelState.IsValid)
            {
                if (!HTTPHelper.IsSessionExpired(HttpContext, this))
                    HTTPHelper.EndSession(HttpContext, this);
            }
            return View("Index");
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
        private async Task<EmbedConfig> EmbedReportConfigData()
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
                        result.ErrorMessage = "Group has no reports.";
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
                        result.ErrorMessage = "Failed to generate embed token.";
                        return result;
                    }

                    // Generate Embed Configuration.
                    result.EmbedToken = tokenResponse;
                    result.EmbedUrl = report.EmbedUrl;
                    result.Id = report.Id;

                    return result;
                }
            }
            catch (HttpOperationException exc)
            {
                result.ErrorMessage = string.Format("Status: {0} ({1})\r\nResponse: {2}\r\nRequestId: {3}", exc.Response.StatusCode, (int)exc.Response.StatusCode, exc.Response.Content, exc.Response.Headers["RequestId"].FirstOrDefault());
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
