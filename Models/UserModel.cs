using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CleaverBrooks1.Models
{
    public class UserModel
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Mail { get; set; }

        public string GivenName { get; set; }
        public string Surname { get; set; }
        public string MailNickname { get; set; }

        public string Password { get; set; }

        public string UserPrincipalName { get; set; }

        public Dictionary<string, bool> AppPermission { get; set; }
    }
}
