using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace API.DTOs.User
{
    public class Register
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
    }
}