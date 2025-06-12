using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SHOPAPI.DTOs.Admin
{
    public class UserResponseDto
    {
        public string Username { get; set; }
        public string Role { get; set; }
        public string Token { get; set; }
    }
}