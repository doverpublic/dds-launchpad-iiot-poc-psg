using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Iot.Common
{
    [DataContract]
    public class UserProfile
    {
        static UserProfile() { EntityRegistry.RegisterEntity("userprofile", new UserProfile().GetType()); }

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

        [DataMember]
        public string UserName { get; set; }
        [DataMember]
        public string FirstName { get; set; }
        [DataMember]
        public string LastName { get; set; }
        [DataMember]
        public string Password { get; set; }
        [DataMember]
        public bool   NeedPasswordReset { get; set; }
        public bool   RegisterUser { get; set; }
        public string CurrentPersona { get; set; }
        public string CurrentPersonaHomePage { get; set; }
        public string DefaultUserHomePage { get; set; }
        public string ApplicationHomePage { get; set; }
    }
}

