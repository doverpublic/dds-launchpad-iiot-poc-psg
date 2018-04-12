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
    using System.Diagnostics;
    using System.Fabric;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using System.Net.Http;
    using System.Linq;
    using System.Net.Http.Headers;
    using System.Web;

    using Newtonsoft.Json;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;

    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.PowerBI.Api.V2;
    using Microsoft.PowerBI.Api.V2.Models;
    using Microsoft.Rest;

    using global::Iot.Common;
    using Iot.Insight.WebService.ViewModels;

  
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
        [Route("run/report")]
        [Route("run/report/parm/{reportParm}")]
        public async Task<IActionResult> EmbedReport( string reportParm = null)
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
                this.ViewData["ReportParm"] = reportParm;

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
                bool newUserRegistration = false;
                bool userAllowedToLogin = false;

                if ((objUser.Password != null && objUser.Password.Length > 0) )
                {
                    // First let deal to see if this a user registration
                    if (objUser.FirstName != null)
                    {
                        newUserRegistration = true;
                        Task<bool> result = ExecutePOST(typeof(UserProfile),
                                                    Names.InsightDataServiceName,
                                                    "api/entities/user/withIdentity/" + objUser.UserName,
                                                    "user",
                                                    objUser,
                                                    this.httpClient,
                                                    this.fabricClient,
                                                    this.appLifetime);
                        if (result.Result)
                            userAllowedToLogin = true;
                        else
                            ViewBag.Message = "Error during new user registration";
                    }

                    if (!userAllowedToLogin && !newUserRegistration)
                    {
                        Task<object> userObject = ExecuteGET(typeof(UserProfile),
                                                    Names.InsightDataServiceName,
                                                    "api/entities/user/byIdentity/" + objUser.UserName,
                                                    "user",
                                                    objUser.UserName,
                                                    this.httpClient,
                                                    this.fabricClient,
                                                    this.appLifetime);
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

        // PRIVATE METHODS For Entity work

        // Read from the partitition associated with the entity name (hash of entity name determines with partitiion holds the data)
        private async Task<object> ExecuteGET(Type targetObjectType, string targetServiceType, string servicePathAndQuery, string entityName, string entityKey, HttpClient httpClient, FabricClient fabricClient, IApplicationLifetime appLifetime)
        {
            object objRet = null;
            ServiceUriBuilder uriBuilder = new ServiceUriBuilder(targetServiceType);
            Uri serviceUri = uriBuilder.Build();
            long targetSiteServicePartitionKey = FnvHash.Hash(entityName);
            Uri getUrl = new HttpServiceUriBuilder()
                .SetServiceName(serviceUri)
                .SetPartitionKey(targetSiteServicePartitionKey)
                .SetServicePathAndQuery(servicePathAndQuery)
                .Build();

            HttpResponseMessage response = await httpClient.GetAsync(getUrl, appLifetime.ApplicationStopping);

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return this.StatusCode((int)response.StatusCode);
            }

            JsonSerializer serializer = new JsonSerializer();
            using (StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync()))
            {
                using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
                {
                    objRet = serializer.Deserialize(jsonReader, targetObjectType);
                }
            }

            return objRet;
        }

        private async Task<bool> ExecutePOST(Type targetObjectType, string targetServiceType, string servicePathAndQuery, string entityName, object bodyObject, HttpClient httpClient, FabricClient fabricClient, IApplicationLifetime appLifetime)
        {
            bool bRet = false;
            ServiceUriBuilder uriBuilder = new ServiceUriBuilder(targetServiceType);
            Uri serviceUri = uriBuilder.Build();
            long targetSiteServicePartitionKey = FnvHash.Hash(entityName);

            Uri postUrl = new HttpServiceUriBuilder()
                .SetServiceName(serviceUri)
                .SetPartitionKey(targetSiteServicePartitionKey)
                .SetServicePathAndQuery(servicePathAndQuery)
                .Build();

            string jsonStr = JsonConvert.SerializeObject(bodyObject);
            MemoryStream mStrm = new MemoryStream(Encoding.UTF8.GetBytes(jsonStr));

            using (StreamContent postContent = new StreamContent(mStrm))
            {
                Debug.WriteLine("On ExecutePOST postContent=[" + await postContent.ReadAsStringAsync() + "]");

                postContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage response = await httpClient.PostAsync(postUrl, postContent, appLifetime.ApplicationStopping);

                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // This service expects the receiving target site service to return HTTP 400 if the device message was malformed.
                    // In this example, the message is simply logged.
                    // Your application should handle all possible error status codes from the receiving service
                    // and treat the message as a "poison" message.
                    // Message processing should be allowed to continue after a poison message is detected.

                    string responseContent = await response.Content.ReadAsStringAsync();

                    Debug.WriteLine("On Execute POST for entity[" + entityName + "] request[" + servicePathAndQuery + "] result=[" + responseContent + "]");
                }
                else
                {
                    bRet = true;
                }
            }

            return bRet;
        }
    }
}
