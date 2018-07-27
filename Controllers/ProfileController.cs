using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CleaverBrooks1.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.AspNetCore.Hosting;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using CleaverBrooks1.Models;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.ActiveDirectory.GraphClient;
using System;
using System.DirectoryServices.AccountManagement;
using System.Security.Claims;
using System.Net.Http.Formatting;

namespace CleaverBrooks1.Controllers
{
    public class ProfileController : Controller
    {

        private readonly IConfiguration _configuration;
        private readonly IHostingEnvironment _env;
        private readonly IGraphSdkHelper _graphSdkHelper;

        private const string graphApiUrl = "https://graph.windows.net/0af3281f-e5e0-41b9-9138-ffd48e753f56";

        private Dictionary<string, string> Apps = new Dictionary<string, string>() {
            {"30853f0f-41e2-4231-908b-5fa34c8f4e68", "5c524867-abfb-4f06-92ea-f600614a45a4"},
            {"616869ed-8ee7-4fc5-9cee-af390406c075", "a74b79d8-fd49-41ec-9003-998b17309264"},
            { "df1ee4c1-6dd8-4834-86c6-3d281025ab4a", "f873611b-0cf5-4d1a-8c14-32d68d67efe4"},
            { "be8f8005-ab73-4478-81db-fd80e7899058","909013fa-0679-44eb-89a8-e6c3c09e49fc"},
            { "6ff4eb90-b6f9-4d86-a111-600e0b15e90c","154b06fe-e106-440d-b050-e08537f3b6eb"},
            { "93e84f3c-9a2c-4c78-bd17-7cdc998f82f1", "78f5c370-463c-41d7-a2c8-10a84264e290"},
            {"8d0ff177-840b-42e2-9c09-abb0fa80809b","273ec6d7-1fdd-4185-8062-c52c22c45f63"},
            {"2aa156d2-cc9c-4c45-b8ea-1c89136a3829","1f2b9888-7c64-4b0b-8808-1149fe313db4" },
            { "c5b8ed60-6ce8-40ed-b09b-c320815a036b","391f8a8b-9352-4131-b38b-1b3f769daf45"}
};


        public ProfileController(IConfiguration configuration, IHostingEnvironment hostingEnvironment, IGraphSdkHelper graphSdkHelper)
        {
            _configuration = configuration;
            _env = hostingEnvironment;
            _graphSdkHelper = graphSdkHelper;
        }

        [AllowAnonymous]
        // Load user's profile.
        public async Task<IActionResult> Index(string email)
        {
            if (User.Identity.IsAuthenticated)
            {
                // Get users's email.
                email = email ?? User.Identity.Name ?? User.FindFirst("preferred_username").Value;
                ViewData["Email"] = email;

                // Get user's id for token cache.

                var identifier = User.FindFirst(Startup.ObjectIdentifierType)?.Value;

                // Initialize the GraphServiceClient.
                var graphClient = _graphSdkHelper.GetAuthenticatedClient(identifier);

                ViewData["Response"] = await GraphService.GetUserJson(graphClient, email, HttpContext);

                ViewData["Picture"] = await GraphService.GetPictureBase64(graphClient, email, HttpContext);

                // var users = await GetUserDataAsync();

                //var u = users.CurrentPage.Select(m => m.MemberOf != null).ToList();

                //  var m = await GetUserGroupsAsync(new HttpClient(), "75bbe757-768a-447c-bae8-0e722b9b3a92");

                // var members = await GetGroupMembersAsync(group[4].Id);

                // var user = graphClient.Users.GetByObjectId(objectId).ExecuteAsync().Result as User;

                //here I can get Members/MemberOf

                //  var groups = ((IUserFetcher)user).MemberOf.OfType<Group>().ExecuteAsync().Result.CurrentPage.ToList();



            }

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


        public async Task<IActionResult> ProjectPermission(string Id)
        {
            //List<GroupModel> groupList = await GetGroupsAsync();

            //ViewBag.listOfGroup = groupList;
            // var role = User.IsInRole("admin");

            var principalName = User.FindFirst("preferred_username")?.Value;
            var userName = User.FindFirst("name")?.Value;
            var userObjId = await GetUserObjectId(userName, principalName);

            var groups = await GetUserGroupsAsync(new HttpClient(), userObjId);

            ViewBag.listOfGroup = groups;

            if (string.IsNullOrEmpty(Id) && groups.Count > 0)
            {
                Id = groups[0].Id;
            }
            var groupUsers = new List<UserModel>();
            var apps = new List<ApplicationModel>();

            if (!string.IsNullOrEmpty(Id))
            {
                groupUsers = await GetGroupMembersAsync(Id);
                apps = await GetApplications(new HttpClient());
            }

            var identifier = User.FindFirst(Startup.ObjectIdentifierType)?.Value;
            var adminRoleName = await GetUserAdminRole(identifier);


            ViewBag.applications = apps;
            ViewBag.IsAdmin = !string.IsNullOrEmpty(adminRoleName) ? true : false;
            if (string.IsNullOrEmpty(adminRoleName))
            {
                groupUsers = groupUsers.Where(m => m.Id == userObjId).ToList();
            }

            ViewData["Users"] = groupUsers;

            // await CreateUserAsync();
            //var status = await DeleteUser("9a180e02-a841-4e51-a1fe-1a8f2a9e4f4f");
            /* var loogedinUserId = "75bbe757-768a-447c-bae8-0e722b9b3a92"*/

            return View();
        }


        public async Task<ActionResult> CreateUser()
        {
            return View();
        }

        public async Task<IGraphServiceUsersCollectionPage> GetUserDataAsync()
        {
            GraphServiceClient client = new GraphServiceClient(new AzureAuthenticationProvider());
            IGraphServiceUsersCollectionPage users = await client.Users.Request().Select("GivenName, MemberOf, Surname, City, State").GetAsync();

            var member = await client.Users["75bbe757-768a-447c-bae8-0e722b9b3a92"].Request().Expand("memberOf").GetAsync();
            return users;
        }


        public static async Task<List<GroupModel>> GetGroupsAsync()

        {
            //GraphServiceClient client = new GraphServiceClient(new AzureAuthenticationProvider());

            // var graphClient = AuthenticationHelper.GetAuthenticatedClient();

            try
            {
                //var groups = await client.Groups.Request().GetAsync(); 
                //var x = new List<GroupModel>();
                //var groupModels = groups.CurrentPage.Select(m => new GroupModel() { Id = m.Id, DisplayName = m.DisplayName, Description = m.Description }).ToList();
                //return groupModels;

                using (var client = new HttpClient())
                {
                    //  string accessToken = await GetAppTokenAsync();

                    string clientId = "be8f8005-ab73-4478-81db-fd80e7899058";
                    string clientSecret = "xMaOJxq58LJjmY/W1IzwdI9FEvIuEjWcHy8qumKcdBE=";
                    AuthenticationContext authContext = new AuthenticationContext("https://login.microsoftonline.com/0af3281f-e5e0-41b9-9138-ffd48e753f56/oauth2/token");

                    ClientCredential creds = new ClientCredential(clientId, clientSecret);

                    AuthenticationResult authResult = await authContext.AcquireTokenAsync("https://graph.microsoft.com/", creds);

                    string accessToken = authResult.AccessToken;

                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);


                    var payload = await client.GetStringAsync("https://graph.microsoft.com/v1.0/groups?$filter=startswith(DisplayName,'C')");
                    var obj = JsonConvert.DeserializeObject<JObject>(payload);
                    var apps = from g in obj["value"]
                               select new GroupModel { Id = g["id"].Value<string>(), DisplayName = g["displayName"].Value<string>(), Description = g["description"].Value<string>() };

                    return apps.ToList();

                }

            }

            catch (ServiceException e)
            {
                return null;
            }

        }

        public static async Task<List<UserModel>> GetGroupMembersAsync(string groupId)
        {
            // IGroupMembersCollectionWithReferencesPage members = null;
            GraphServiceClient graphClient = new GraphServiceClient(new AzureAuthenticationProvider());
            var userList = new List<UserModel>();
            try
            {
                var group = await graphClient.Groups[groupId].Request().Expand("members").GetAsync();
                var members = group.Members.CurrentPage;
                // members = group.Members.CurrentPage.Select(m => new UserModel() {Id = m.Id, DisplayName = m.DisplayName, Mail = m.Mail });

                foreach (var member in members)
                {
                    //Debug.WriteLine("Member Id:" + member.Id);

                    //var Id = member.Id;
                    var name = member.GetType().GetProperty("DisplayName").GetValue(member, null);
                    var mail = member.GetType().GetProperty("Mail").GetValue(member, null);

                    var propInfo = member.GetType().GetProperty("Id");
                    var Id = propInfo.GetValue(member, null);

                    var apps = await GetAssignedAppForUser((string)Id);

                    var permission = new Dictionary<string, bool>();
                    foreach (var app in apps)
                    {
                        bool result;
                        if (!permission.TryGetValue(app.DisplayName, out result))
                        {
                            permission.Add(app.DisplayName, true);
                        }

                    }

                    userList.Add(new UserModel() { Id = (string)Id, DisplayName = (string)name, Mail = (string)mail, AppPermission = permission });


                }

            }
            catch (ServiceException e)
            {

                return userList;
            }


            return userList;

        }

        public static async Task<List<GroupModel>> GetUserGroupsAsync(HttpClient client, string userId)
        {

            string clientId = "6ff4eb90-b6f9-4d86-a111-600e0b15e90c";
            string clientSecret = "lctqDDOP8424(mbcMRY8:*?";
            AuthenticationContext authContext = new AuthenticationContext("https://login.microsoftonline.com/0af3281f-e5e0-41b9-9138-ffd48e753f56/oauth2/token");

            ClientCredential creds = new ClientCredential(clientId, clientSecret);

            AuthenticationResult authResult = await authContext.AcquireTokenAsync("https://graph.microsoft.com/", creds);

            string accessToken = authResult.AccessToken;

            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

            var payload = await client.GetStringAsync($"https://graph.microsoft.com/v1.0/users/{userId}/memberOf");
            var obj = JsonConvert.DeserializeObject<JObject>(payload);
            var groupDescription = from g in obj["value"]
                                   select g["displayName"].Value<string>();

            var groups = from g in obj["value"]
                         select new GroupModel { Id = g["id"].Value<string>(), DisplayName = g["displayName"].Value<string>(), Description = g["description"].Value<string>() };

            var groupsList = groups.ToList().Where(m => m.DisplayName.StartsWith("Comp_")).ToList();
            //return groupDescription.ToArray();
            return groupsList;
        }


        public static async Task<List<ApplicationModel>> GetApplications(HttpClient client)
        {

            string clientId = "6ff4eb90-b6f9-4d86-a111-600e0b15e90c";
            string clientSecret = "lctqDDOP8424(mbcMRY8:*?";
            AuthenticationContext authContext = new AuthenticationContext("https://login.microsoftonline.com/0af3281f-e5e0-41b9-9138-ffd48e753f56/oauth2/token");

            ClientCredential creds = new ClientCredential(clientId, clientSecret);

            AuthenticationResult authResult = await authContext.AcquireTokenAsync("https://graph.microsoft.com/", creds);

            string accessToken = authResult.AccessToken;

            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);


            var payload = await client.GetStringAsync("https://graph.microsoft.com/beta/applications?$select=displayName,appId");
            var obj = JsonConvert.DeserializeObject<JObject>(payload);
            var apps = from g in obj["value"]
                       select new ApplicationModel { DisplayName = g["displayName"].Value<string>(), AppId = g["appId"].Value<string>() };

            return apps.ToList();
        }

        public static async Task<List<ApplicationModel>> GetAssignedAppForUser(string Id)
        {

            using (var client = new HttpClient())
            {
                string clientId = "be8f8005-ab73-4478-81db-fd80e7899058";
                string clientSecret = "xMaOJxq58LJjmY/W1IzwdI9FEvIuEjWcHy8qumKcdBE=";
                AuthenticationContext authContext = new AuthenticationContext("https://login.microsoftonline.com/0af3281f-e5e0-41b9-9138-ffd48e753f56/oauth2/token");

                ClientCredential creds = new ClientCredential(clientId, clientSecret);

                AuthenticationResult authResult = await authContext.AcquireTokenAsync("https://graph.windows.net/", creds);

                string accessToken = authResult.AccessToken;

                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);


                var url = $"https://graph.windows.net/0af3281f-e5e0-41b9-9138-ffd48e753f56/users/{Id}/appRoleAssignments?api-version=1.6";
                var payload = await client.GetStringAsync(url);
                var obj = JsonConvert.DeserializeObject<JObject>(payload);

                var apps = from g in obj["value"]
                           select new ApplicationModel { DisplayName = g["resourceDisplayName"].Value<string>(), AppId = g["resourceId"].Value<string>() };

                return apps.ToList();
            }
        }


        [HttpPost]
        public async Task<IActionResult> ProjectPermission(IFormCollection formCollection)
        {
            foreach (var key in formCollection.Keys.Where(m => m.StartsWith("chk")).ToList())
            {
                string value = formCollection[key];
                var keyArr = key.Split("_");
                if (keyArr.Length > 2)
                {
                    string userId = keyArr[1];
                    string appId = keyArr[2];

                    if (value.Equals("on"))
                    {
                        var assignedApps = await GetAssignedAppForUser((string)userId);

                        string objectId = "";

                        if (this.Apps.TryGetValue(appId, out objectId))
                        {

                        }

                        var exists = (from p in assignedApps where p.AppId == objectId select p.AppId).Any();

                        if (!exists)
                        {
                            objectId = "";
                            if (this.Apps.TryGetValue(appId, out objectId))
                            {
                                var result = await AssignAppToUser(userId, objectId);
                            }
                        }

                    }
                    else
                    {
                        //delete
                        var assignedApps = await GetAssignedAppForUser((string)userId);
                        string objectId = "";
                        if (this.Apps.TryGetValue(appId, out objectId))
                        {
                            var exists = (from p in assignedApps where p.AppId == objectId select p.AppId).Any();

                            if (exists)
                            {
                                //delete
                                var result = await RemoveAssignedAppToUser(userId, objectId);
                            }
                        }

                    }

                }
            }

            return RedirectToAction("ProjectPermission");
            //return View();
        }


        public static async Task<bool> AssignAppToUser(string userId, string appId)
        {

            using (var client = new HttpClient())
            {
                string clientId = "be8f8005-ab73-4478-81db-fd80e7899058";
                string clientSecret = "xMaOJxq58LJjmY/W1IzwdI9FEvIuEjWcHy8qumKcdBE=";
                AuthenticationContext authContext = new AuthenticationContext("https://login.microsoftonline.com/0af3281f-e5e0-41b9-9138-ffd48e753f56/oauth2/token");

                ClientCredential creds = new ClientCredential(clientId, clientSecret);

                AuthenticationResult authResult = await authContext.AcquireTokenAsync("https://graph.windows.net/", creds);

                string accessToken = authResult.AccessToken;

                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);


                var jsonObject = new { principalId = userId, resourceId = appId, id = "00000000-0000-0000-0000-000000000000" };
                //jsonObject.principalId = userId;
                //jsonObject.resourceId = appId;
                //jsonObject.id = "00000000-0000-0000-0000-000000000000";

                var data = JsonConvert.SerializeObject(jsonObject);
                var buffer = System.Text.Encoding.UTF8.GetBytes(data);
                var byteContent = new ByteArrayContent(buffer);
                byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                var content = new StringContent(JsonConvert.SerializeObject(jsonObject), System.Text.Encoding.UTF8, "application/json");

                var url = $"https://graph.windows.net/0af3281f-e5e0-41b9-9138-ffd48e753f56/users/{userId}/appRoleAssignments?api-version=1.6";

                var payload = await client.PostAsync(url, byteContent);

                var obj = JsonConvert.DeserializeObject<JObject>(payload.Content.ReadAsStringAsync().Result);

                //var apps = from g in obj["value"]
                //           select new ApplicationModel { DisplayName = g["resourceDisplayName"].Value<string>(), AppId = g["resourceId"].Value<string>() };

                bool status = (int)payload.StatusCode == 201 ? true : false;

                return status;
            }
        }

        private ActiveDirectoryClient GetAADClient()
        {
            Uri url = new Uri(graphApiUrl);
            ActiveDirectoryClient adClient = new ActiveDirectoryClient(url, async () => await GetAppTokenAsync("https://graph.windows.net/"));

            return adClient;
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


        [HttpPost]
        public async Task<ActionResult> CreateUser(IFormCollection formCollection)
        {
            var user = new UserModel();

            //foreach (var key in formCollection.Keys)
            //{
            //    string value = formCollection[key];

            //    switch (key)
            //    {

            //    }
            //}

            user.DisplayName = formCollection["DisplayName"];
            user.GivenName = formCollection["GivenName"];
            user.Surname = formCollection["Surname"];
            user.Mail = formCollection["Email"];
            user.Password = formCollection["Password"];

            await CreateUserAsync(user);
            return View();

        }

        private async Task<Microsoft.Graph.User> CreateUserAsync(UserModel userModel)
        {

            try
            {
                var adClient = GetAADClient();

                var userEmail = userModel.Mail;// "testuser@CBTESTDIRECTORY1.onmicrosoft.com";
                string nickName = userEmail.Split("@")[0];

                var userObj = new Microsoft.Graph.User()
                {
                    GivenName = userModel.GivenName,
                    Surname = userModel.Surname,
                    MailNickname = nickName,
                    DisplayName = userModel.DisplayName,
                    //Mail = userEmail,

                    AccountEnabled = true
                };



                // userObj.Mail = userEmail;

                string tenantName = "CBTESTDIRECTORY1.onmicrosoft.com";
                var userPrincipalName = userModel.GivenName + Guid.NewGuid().ToString() + "@" + tenantName;
                userObj.UserPrincipalName = userPrincipalName;

                // var pwd = "password@123";
                var passwordProfile = new Microsoft.Graph.PasswordProfile()
                {
                    Password = userModel.Password,
                    //ForceChangePasswordNextLogin = true
                };

                userObj.PasswordProfile = passwordProfile;
                //adClient.Users.AddUserAsync(userObj).Wait();

                GraphServiceClient client = new GraphServiceClient(new AzureAuthenticationProvider());

                var user = await client.Users.Request().AddAsync(userObj);

                return user;

            }
            catch (Exception ex)
            {

                string msg = ex.Message;
                return null;
            }

        }


        public async Task<ActionResult> DeleteUser(string Id)
        {
            //var adClient = GetAADClient();
            //GraphServiceClient client = new GraphServiceClient(new AzureAuthenticationProvider());            
            //var deletedUser = await client.Users.Request().Select("ObjectId").GetAsync();
            //try {
            // ;
            //  //  RedirectToAction("GetAllUsers");
            //}
            //catch (Exception ex)
            //{
            //    //return false;
            //}


            using (var client = new HttpClient())
            {
                string clientId = "be8f8005-ab73-4478-81db-fd80e7899058";
                string clientSecret = "xMaOJxq58LJjmY/W1IzwdI9FEvIuEjWcHy8qumKcdBE=";
                AuthenticationContext authContext = new AuthenticationContext("https://login.microsoftonline.com/0af3281f-e5e0-41b9-9138-ffd48e753f56/oauth2/token");

                ClientCredential creds = new ClientCredential(clientId, clientSecret);

                AuthenticationResult authResult = await authContext.AcquireTokenAsync("https://graph.microsoft.com/", creds);

                string accessToken = authResult.AccessToken;

                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

                var url = $"https://graph.microsoft.com/v1.0/users/{Id}";
                var payload = await client.DeleteAsync(url);
                //var obj = JsonConvert.DeserializeObject<JObject>(payload);

                //var apps = from g in obj["value"]
                //           select new ApplicationModel { DisplayName = g["resourceDisplayName"].Value<string>(), AppId = g["resourceId"].Value<string>() };

                //return apps.ToList();
            }

            return RedirectToAction("GetAllUsers");
        }

        public async Task<ActionResult> GetAllUsers(string searchTxt = "")
        {

            var userList = new List<UserModel>();
            try
            {
                GraphServiceClient client = new GraphServiceClient(new AzureAuthenticationProvider());

                // https://graph.microsoft.com/v1.0/users?$filter=startswith(displayName, 'cb' )  or startswith(mail, 'b.white@perficient.com' ) 

                var query = string.Empty;
                if (!string.IsNullOrEmpty(searchTxt))
                {
                    query = $"startswith(displayName, '{searchTxt}' )  or startswith(mail, '{searchTxt}') or startswith(userPrincipalName, '{searchTxt}')";
                }
                List<QueryOption> options = new List<QueryOption>()
                {
                    new QueryOption("$filter", query)
                };

                var users = await client.Users.Request(options).GetAsync();

                userList = users.CurrentPage.Select(m => new UserModel() { Id = m.Id, UserPrincipalName = m.UserPrincipalName, DisplayName = m.DisplayName, GivenName = m.GivenName, Surname = m.Surname }).ToList();

                var identifier = User.FindFirst(Startup.ObjectIdentifierType)?.Value;
                var adminRoleName = await GetUserAdminRole(identifier);
                ViewBag.IsAdmin = !string.IsNullOrEmpty(adminRoleName) ? true : false;

                var companyGroups = await GetAllGroupsAsync("Company");
                var personaGroups = await GetAllGroupsAsync("Persona");
                ViewBag.listOfGroup = companyGroups;
                ViewBag.listOfPersonaGroup = personaGroups;

                //ViewBag.UserList = userModels;
            }
            catch (Exception ex)
            {

            }

            return View(userList);
        }


        public async Task<string> GetUserAdminRole(string userId)
        {
            // var userId = "5f7969cb-7e5d-4eb5-8493-db2043a2b02f";

            //var principalName = User.FindFirst("emailaddress").Value;
            var principalName = string.Empty;
            var userName = string.Empty;
            var userObjId = string.Empty;
            string userRole = string.Empty;

            try
            {
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
                         principalName = User.FindFirst("preferred_username").Value;
                         userName = User.FindFirst("name").Value;
                         userObjId = await GetUserObjectId(userName, principalName);
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

            return userRole;
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

            //public static async Task<bool> RemoveAssignedAppToUser(string userId, string appId)
            //{

            //    using (var client = new HttpClient())
            //    {
            //        string clientId = "be8f8005-ab73-4478-81db-fd80e7899058";
            //        string clientSecret = "xMaOJxq58LJjmY/W1IzwdI9FEvIuEjWcHy8qumKcdBE=";
            //        AuthenticationContext authContext = new AuthenticationContext("https://login.microsoftonline.com/0af3281f-e5e0-41b9-9138-ffd48e753f56/oauth2/token");

            //        ClientCredential creds = new ClientCredential(clientId, clientSecret);

            //        AuthenticationResult authResult = await authContext.AcquireTokenAsync("https://graph.windows.net/", creds);

            //        string accessToken = authResult.AccessToken;

            //        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

            //        var url = $"https://graph.windows.net/0af3281f-e5e0-41b9-9138-ffd48e753f56/users/{userId}/appRoleAssignments/{appId}?api-version=1.6";

            //        var payload = await client.DeleteAsync(url);

            //        var obj = JsonConvert.DeserializeObject<JObject>(payload.Content.ReadAsStringAsync().Result);

            //        //var apps = from g in obj["value"]
            //        //           select new ApplicationModel { DisplayName = g["resourceDisplayName"].Value<string>(), AppId = g["resourceId"].Value<string>() };

            //        bool status = (int)payload.StatusCode == 200 ? true : false;

            //        return status;
            //    }
            //}



        }


        public static async Task<bool> RemoveAssignedAppToUser(string userId, string appId)
        {

            using (var client = new HttpClient())
            {
                string clientId = "be8f8005-ab73-4478-81db-fd80e7899058";
                string clientSecret = "xMaOJxq58LJjmY/W1IzwdI9FEvIuEjWcHy8qumKcdBE=";
                AuthenticationContext authContext = new AuthenticationContext("https://login.microsoftonline.com/0af3281f-e5e0-41b9-9138-ffd48e753f56/oauth2/token");

                ClientCredential creds = new ClientCredential(clientId, clientSecret);

                AuthenticationResult authResult = await authContext.AcquireTokenAsync("https://graph.windows.net/", creds);

                string accessToken = authResult.AccessToken;

                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

                var url = $"https://graph.windows.net/0af3281f-e5e0-41b9-9138-ffd48e753f56/users/{userId}/appRoleAssignments/{appId}?api-version=1.6";

                var payload = await client.DeleteAsync(url);

                var obj = JsonConvert.DeserializeObject<JObject>(payload.Content.ReadAsStringAsync().Result);

                //var apps = from g in obj["value"]
                //           select new ApplicationModel { DisplayName = g["resourceDisplayName"].Value<string>(), AppId = g["resourceId"].Value<string>() };

                bool status = (int)payload.StatusCode == 200 ? true : false;

                return status;
            }
        }

        public async Task<ActionResult> GetMyApplications()
        {
            //var userId = "946055ac-8f00-4029-bcdd-d300e3ede17e";
            var userId = User.FindFirst(Startup.ObjectIdentifierType)?.Value;
            var apps = new List<ApplicationModel>();
            try
            {
                apps = await GetAssignedAppForUser(userId);
            }
            catch (Exception)
            {


            }


            return View(apps);
        }

        public async Task<ActionResult> RegisterApplication()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> RegisterApplication(IFormCollection formCollection)
        {
            var urls = new string[] { Convert.ToString(formCollection["signonUrl"]) };
            var jsonObject = new
            {
                allowPublicClient = true,
                displayName = Convert.ToString(formCollection["displayName"]),
                web = new
                {
                    redirectUrls = urls
                }
            };


            try
            {
                using (var client = new HttpClient())
                {
                    string accessToken = await GetAppTokenAsync("https://graph.microsoft.com/");
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

                    var data = JsonConvert.SerializeObject(jsonObject);
                    var buffer = System.Text.Encoding.UTF8.GetBytes(data);
                    var byteContent = new ByteArrayContent(buffer);
                    byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                    var content = new StringContent(JsonConvert.SerializeObject(jsonObject), System.Text.Encoding.UTF8, "application/json");

                    var url = $"https://graph.microsoft.com/beta/applications";

                    var payload = await client.PostAsync(url, byteContent);

                    var obj = JsonConvert.DeserializeObject<JObject>(payload.Content.ReadAsStringAsync().Result);

                    bool status = (int)payload.StatusCode == 201 ? true : false;

                    return RedirectToAction("GetAllEnterpriseApps");

                }
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<ActionResult> GetAllEnterpriseApps()
        {
            var apps = new List<ApplicationModel>();

            try
            {
                using (var client = new HttpClient())
                {

                    string accessToken = await GetAppTokenAsync("https://graph.microsoft.com");
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
                    var url = $"https://graph.microsoft.com/beta/applications";
                    var payload = await client.GetStringAsync(url);
                    var obj = JsonConvert.DeserializeObject<JObject>(payload);

                    //var value = from g in obj["value"]
                    //           select new ApplicationModel { DisplayName = g["resourceDisplayName"].Value<string>(), AppId = g["resourceId"].Value<string>() };

                    var app = from g in obj["value"]
                              select new { Id = g["id"].Value<string>(), DisplayName = g["displayName"].Value<string>(), AppId = g["appId"].Value<string>(), url = g["web"].Value<dynamic>()?.redirectUrls };

                    var list = app.ToList();

                    foreach (var item in list)
                    {
                        var signUrl = item.url;
                        apps.Add(new ApplicationModel() { AppId = item.AppId, DisplayName = item.DisplayName });
                    }
                }
            }
            catch (Exception)
            {

                throw;
            }

            return View(apps);
        }

        public async Task<ActionResult> DeleteApplication(string appId)
        {
            try
            {

                using (var client = new HttpClient())
                {
                    string accessToken = await GetAppTokenAsync("https://graph.microsoft.com");
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
                    var url = $"https://graph.microsoft.com/beta/applications/{appId}";
                    var payload = await client.DeleteAsync(url);

                    return RedirectToAction("GetAllEnterpriseApps");
                }

            }
            catch (Exception ex)
            {

                throw;
            }

        }


        public async Task<ActionResult> GetAllGroups()
        {
            var groups = new List<GroupModel>();
            groups = await GetAllGroupsAsync("Company");
            return View(groups);

        }

        public async Task<List<GroupModel>> GetAllGroupsAsync(string groupType = "")
        {
            var groups = new List<GroupModel>();

            try
            {
                using (var client = new HttpClient())
                {

                    string accessToken = await GetAppTokenAsync("https://graph.microsoft.com");
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
                    var url = $"https://graph.microsoft.com/v1.0/groups";
                    var payload = await client.GetStringAsync(url);
                    var obj = JsonConvert.DeserializeObject<JObject>(payload);

                    //var value = from g in obj["value"]
                    //           select new ApplicationModel { DisplayName = g["resourceDisplayName"].Value<string>(), AppId = g["resourceId"].Value<string>() };

                    var group = from g in obj["value"]
                                select new GroupModel { Id = g["id"].Value<string>(), DisplayName = g["displayName"].Value<string>(), Description = g["description"].Value<string>() };

                    //groups = group.ToList();
                    string searchstr = string.Empty;
                    if (groupType.Equals("Company"))
                    {
                        searchstr = "Comp_";
                    }
                    else if (groupType.Equals("Persona"))
                    {
                        searchstr = "Per_";
                    }

                    groups = group.ToList().Where(m => m.DisplayName.StartsWith(searchstr)).ToList();
                    //return groupDescription.ToArray();

                }
            }
            catch (Exception)
            {

                throw;
            }

            return groups;

        }

        public async Task<ActionResult> CreateGroup()
        {

            return View();
        }

        [HttpPost]
        public async Task<ActionResult> CreateGroup(IFormCollection formCollection)
        {
            try
            {
                var adClient = GetAADClient();
                var groupObj = new Microsoft.Graph.Group()
                {
                    DisplayName = Convert.ToString(formCollection["displayName"]),
                    Description = Convert.ToString(formCollection["description"]),
                    MailEnabled = false,
                    MailNickname = Convert.ToString(formCollection["displayName"]),
                    SecurityEnabled = true

                };

                GraphServiceClient client = new GraphServiceClient(new AzureAuthenticationProvider());
                var group = await client.Groups.Request().AddAsync(groupObj);

            }
            catch (Exception ex)
            {

                string msg = ex.Message;
                return null;
            }

            return RedirectToAction("GetAllGroups");
        }


        public async Task<ActionResult> DeleteGroup(string Id)
        {
            try
            {

                using (var client = new HttpClient())
                {
                    string accessToken = await GetAppTokenAsync("https://graph.microsoft.com");
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
                    var url = $"https://graph.microsoft.com/v1.0/groups/{Id}";
                    var payload = await client.DeleteAsync(url);

                    return RedirectToAction("GetAllGroups");
                }

            }
            catch (Exception ex)
            {

                throw;
            }

        }


        [HttpPost]
        public async Task<JsonResult> AddToCompany(string action, string groupId, List<string> values)
        {
            try
            {
                GraphServiceClient graphClient = new GraphServiceClient(new AzureAuthenticationProvider());
                foreach (var item in values)
                {
                    var user = new Microsoft.Graph.User { Id = item };
                    if (action.Equals("add"))
                    {
                        await graphClient.Groups[groupId].Members.References.Request().AddAsync(user);
                    }
                    else if (action.Equals("delete"))
                    {
                        await graphClient.Groups[groupId].Members[item].Reference.Request().DeleteAsync();
                    }
                }

                return new JsonResult(true);
            }
            catch (Exception)
            {

                return new JsonResult(true);
            }

        }


        public async Task<ActionResult> CreatePersona(IFormCollection formCollection)
        {

            try
            {
                var adClient = GetAADClient();
                var groupObj = new Microsoft.Graph.Group()
                {
                    DisplayName = Convert.ToString(formCollection["displayName"]),
                    Description = Convert.ToString(formCollection["description"]),
                    MailEnabled = false,
                    MailNickname = Convert.ToString(formCollection["displayName"]),
                    SecurityEnabled = true

                };

                GraphServiceClient client = new GraphServiceClient(new AzureAuthenticationProvider());
                var group = await client.Groups.Request().AddAsync(groupObj);

            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                return null;
            }

            return RedirectToAction("GetAllGroups");
        }



        public async Task<ActionResult> DeletePersona(string Id)
        {
            try
            {

                using (var client = new HttpClient())
                {
                    string accessToken = await GetAppTokenAsync("https://graph.microsoft.com");
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
                    var url = $"https://graph.microsoft.com/v1.0/groups/{Id}";
                    var payload = await client.DeleteAsync(url);

                    return RedirectToAction("GetAllGroups");
                }

            }
            catch (Exception ex)
            {

                throw;
            }

        }

        public async Task<ActionResult> InviteGuestUsers()
        {
            return View();
        }

        public async Task<ActionResult> SendInvitation(string usersCsv)
        {
            InvitationModel invitation = new InvitationModel();
            invitation.InvitedUserEmailAddresserty = usersCsv;
            invitation.InviteRedirectUrl = "";
            invitation.SendInvitationMessage = true;

            //using (HttpClient client = new HttpClient())
            //{
            //    string accessToken = await GetAppTokenAsync("https://graph.microsoft.com");
            //   //lient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
            //    client.BaseAddress = new Uri("https://graph.microsoft.com/");
            //    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            //    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            //    HttpResponseMessage response = client.PostAsJsonAsync<InvitationModel>("v1.0/invitations", invitation).Result;
            //    dynamic inviteResult = response.Content.ReadAsAsync<dynamic>().Result;
            //    if (inviteResult.status != "Error")
            //    {
            //        //

            //    }
            //}

            try
            {
                var jsonObject = new
                {
                    invitedUserEmailAddress = usersCsv,
                    inviteRedirectUrl = "https://localhost:4433",
                    sendInvitationMessage = true
                };

                using (var client = new HttpClient())
                {
                    string accessToken = await GetAppTokenAsync("https://graph.microsoft.com/");
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

                    var data = JsonConvert.SerializeObject(jsonObject);
                    var buffer = System.Text.Encoding.UTF8.GetBytes(data);
                    var byteContent = new ByteArrayContent(buffer);
                    byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                    //r content = new StringContent(JsonConvert.SerializeObject(invitation), System.Text.Encoding.UTF8, "application/json");

                    var url = $"https://graph.microsoft.com/v1.0/invitations";

                    var payload = await client.PostAsync(url, byteContent);

                    var obj = JsonConvert.DeserializeObject<JObject>(payload.Content.ReadAsStringAsync().Result);

                    bool status = (int)payload.StatusCode == 200 ? true : false;

                    return RedirectToAction("InviteGuestUsers");

                }
            }
            catch (Exception)
            {

                throw;
            }


        }

        //[HttpPost]
        //public async void GetUsersForGroup(string Id)
        //{
        //    var groupUsers = await GetGroupMembersAsync(Id);

        //    ViewData["Users"] = groupUsers;
        //
    }



    //public async Task<ActionResult> DeleteApplication((IFormCollection formCollection)
    //{
    //    return View();
    //}




}