using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CleaverBrooks1.Models
{
    public class InvitationModel
    {
        public string InvitedUserEmailAddresserty { get; set; }
        public string InviteRedirectUrl { get; set; }
        public bool SendInvitationMessage { get; set; }
    }
}
