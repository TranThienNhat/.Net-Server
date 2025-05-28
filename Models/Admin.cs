using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using SHOPAPI.Models.Enum;

namespace SHOPAPI.Models
{
    public class Admin
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public Role Role { get; set; } = Role.ADMIN;
    }
}