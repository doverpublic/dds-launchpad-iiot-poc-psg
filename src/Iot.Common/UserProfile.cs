using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace Iot.Common
{
    public class UserProfile
    {
        public UserProfile()
        {
            RegisterUser = false;
            NeedPasswordReset = false;
        }

        public UserProfile( bool registerUser )
        {
            RegisterUser = registerUser;
            NeedPasswordReset = false;
        }

        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Password { get; set; }
        public bool   NeedPasswordReset { get; set; }
        public bool   RegisterUser { get; set; }
        public string CurrentPersona { get; set; }
        public string CurrentPersonaHomePage { get; set; }
        public string DefaultUserHomePage { get; set; }
        public string ApplicationHomePage { get; set; }
    }
}
