using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Launchpad.Iot.Insight.DataService.Models
{
    public class UserDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Username { get; set; }
        public bool PassworCreated { get; set; }
        public string Password { get; set; }
    }
}
