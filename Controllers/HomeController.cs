

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CleaverBrooks1.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Linq;
using System;
using Microsoft.AspNetCore.Http;

namespace CleaverBrooks1.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHostingEnvironment _env;
        private readonly IGraphSdkHelper _graphSdkHelper;

        public HomeController(IConfiguration configuration, IHostingEnvironment hostingEnvironment, IGraphSdkHelper graphSdkHelper)
        {
            _configuration = configuration;
            _env = hostingEnvironment;
            _graphSdkHelper = graphSdkHelper;
        }

        public async Task<IActionResult> Index()
        {
            var identifier = User.FindFirst(Startup.ObjectIdentifierType)?.Value;
            var adminRoleName = string.Empty;
            if (null != identifier)
            {
                adminRoleName = await GetUserAdminRole(identifier);
                //  HttpContext.Session.("IsAdmin", adminRoleName);
                HttpContext.Session.SetBoolean("IsAdmin", !string.IsNullOrEmpty(adminRoleName) ? true : false);
            }
            TempData["IsAdmin"] = !string.IsNullOrEmpty(adminRoleName) ? true : false;
            return View();
        }

        [Authorize]
        [HttpPost]
        // Send an email message from the current user.
        public async Task<IActionResult> SendEmail(string recipients)
        {
            if (string.IsNullOrEmpty(recipients))
            {
                TempData["Message"] = "Please add a valid email address to the recipients list!";
                return RedirectToAction("Index");
            }

            try
            {
                // Get user's id for token cache.
                var identifier = User.FindFirst(Startup.ObjectIdentifierType)?.Value;

                // Initialize the GraphServiceClient.
                var graphClient = _graphSdkHelper.GetAuthenticatedClient(identifier);

                // Send the email.
                await GraphService.SendEmail(graphClient, _env, recipients, HttpContext);

                // Reset the current user's email address and the status to display when the page reloads.
                TempData["Message"] = "Success! Your mail was sent.";
                return RedirectToAction("Index");
            }
            catch (ServiceException se)
            {
                if (se.Error.Code == "Caller needs to authenticate.") return new EmptyResult();
                return RedirectToAction("Error", "Home", new { message = "Error: " + se.Error.Message });
            }
        }

        [AllowAnonymous]
        public IActionResult About()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Contact()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Error()
        {
            return View();
        }

        private async Task<string> GetUserAdminRole(string userId)
        {
           
            try
            {
                // var userId = "5f7969cb-7e5d-4eb5-8493-db2043a2b02f";
                var principalName = User.FindFirst("preferred_username").Value;
                //var principalName = User.FindFirst("emailaddress").Value;
                var userName = User.FindFirst("name").Value;
                var userObjId = await GetUserObjectId(userName, principalName);
                string userRole = string.Empty;

                using (var client = new HttpClient())
                {
                    string accessToken = await GetAppTokenAsync("https://graph.windows.net/");
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

                    //get admin role
                    var directoryRoleUrl = "https://graph.windows.net/CBTESTDIRECTORY1.onmicrosoft.com/directoryRoles?api-version=1.6";
                    var dirPayload = await client.GetStringAsync(directoryRoleUrl);
                    var dirObj = JsonConvert.DeserializeObject<JObject>(dirPayload);
                    var dirRoles = from role in dirObj["value"] select new { objectType = role["objectType"], displayName = role["displayName"], objectId = role["objectId"] };

                    // var dirRoles = from role in dirObj["value"] select new { objectType = role["objectType"], UserPrincipal = role["principalName"], objectId = role["objectId"] };

                    try
                    {
                        var globalAdminRoleObjId = dirRoles.ToList().Where(m => (string)m.displayName == "Company Administrator").ToList()[0].objectId;
                        if (globalAdminRoleObjId != null)
                        {
                            var url = $"https://graph.windows.net/CBTESTDIRECTORY1.onmicrosoft.com/directoryRoles/{globalAdminRoleObjId}/members?$filter=(objectId eq '{userObjId}')&api-version=1.6";
                            var payload = await client.GetStringAsync(url);
                            var obj = JsonConvert.DeserializeObject<JObject>(payload);
                            if (obj["value"].Count() > 0)
                            {
                                userRole = "CompanyAdministrator";
                            }
                        }
                    }
                    catch (Exception)
                    {

                    }
                    //Checking global admin
                    if (string.IsNullOrEmpty(userRole))
                    {
                        //Checking user admin
                        var userAdminRoleObjId = dirRoles.ToList().Where(m => (string)m.displayName == "User Account Administrator").ToList()[0].objectId;
                        if (userAdminRoleObjId != null)
                        {
                            var url = $"https://graph.windows.net/CBTESTDIRECTORY1.onmicrosoft.com/directoryRoles/{userAdminRoleObjId}/members?$filter=(objectId eq '{userObjId}')&api-version=1.6";
                            var payload = await client.GetStringAsync(url);
                            var obj = JsonConvert.DeserializeObject<JObject>(payload);
                            if (obj["value"].Count() > 0)
                            {
                                userRole = "UserAccountAdministrator";
                            }
                        }
                    }
                    return userRole;
                }
            }
            catch (HttpRequestException ex)
            {

            }
            catch (Exception ex)
            {

            }

            return null;
        }

        private async Task<string> GetUserObjectId(string userName, string principalName)
        {
            using (var client = new HttpClient())
            {
                string accessToken = await GetAppTokenAsync("https://graph.microsoft.com");
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

                //  var url = $"https://graph.windows.net/CBTESTDIRECTORY1.onmicrosoft.com/users/{email}/objectId?api-version=1.6";
                //var url = $"https://graph.microsoft.com/v1.0/users?$select=id&$filter=mail eq '{email}'";
                //var url = $"https://graph.microsoft.com/v1.0/users?$select=id&$filter=displayName eq '{displayNamee}'";
                var url = $"https://graph.microsoft.com/v1.0/users?$select=id&$filter=userPrincipalName eq '{principalName}' or displayName eq '{userName}' ";
                var payload = await client.GetStringAsync(url);
                string objectId = string.Empty;
                if (payload != null)
                {
                    var obj = JsonConvert.DeserializeObject<JObject>(payload);
                    if (obj["value"].HasValues)
                    {
                        var id = (from g in obj["value"]
                                  select g["id"].Value<string>());

                        objectId = id.FirstOrDefault();

                    }
                }


                return objectId;

            }



        }

        private static async Task<string> GetAppTokenAsync(string graphApiUrl)
        {
            string clientId = "be8f8005-ab73-4478-81db-fd80e7899058";
            string clientSecret = "xMaOJxq58LJjmY/W1IzwdI9FEvIuEjWcHy8qumKcdBE=";
            AuthenticationContext authContext = new AuthenticationContext("https://login.microsoftonline.com/0af3281f-e5e0-41b9-9138-ffd48e753f56/oauth2/token");

            ClientCredential creds = new ClientCredential(clientId, clientSecret);

            AuthenticationResult authResult = await authContext.AcquireTokenAsync($"{graphApiUrl}", creds);

            string accessToken = authResult.AccessToken;

            return accessToken;
        }
    }
}
