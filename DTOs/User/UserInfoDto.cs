using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace API.DTOs.User
{
    public class UserInfoDto
    {
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
    }
}