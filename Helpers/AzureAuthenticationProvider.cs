using Microsoft.Graph;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CleaverBrooks1.Helpers
{
    public class AzureAuthenticationProvider : IAuthenticationProvider
    {
        private AuthenticationContext authContext;
        public string AccessToken { get; set; }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            string clientId = "6ff4eb90-b6f9-4d86-a111-600e0b15e90c";
            string clientSecret = "lctqDDOP8424(mbcMRY8:*?";
            authContext = new AuthenticationContext("https://login.microsoftonline.com/0af3281f-e5e0-41b9-9138-ffd48e753f56/oauth2/token");

            ClientCredential creds = new ClientCredential(clientId, clientSecret);

            AuthenticationResult authResult = await authContext.AcquireTokenAsync("https://graph.microsoft.com/", creds);

            this.AccessToken = authResult.AccessToken;

            request.Headers.Add("Authorization", "Bearer " + authResult.AccessToken);


        }
    }
}
