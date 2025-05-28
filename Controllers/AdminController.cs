using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Web.Http;
using Microsoft.AspNet.Identity;
using Microsoft.IdentityModel.Tokens;
using SHOPAPI.Data;
using SHOPAPI.DTOs.Admin;

namespace SHOPAPI.Controllers
{
    [RoutePrefix("api/admin")]
    public class AdminController : ApiController
    {
        private readonly AppDbContext db = new AppDbContext();

        [HttpPost]
        [Route("login")]
        public IHttpActionResult Login(AdminLoginDto dto)
        {
            var admin = db.admins.FirstOrDefault(a => a.Username == dto.Username);
            if (admin == null)
                return Unauthorized();

            var hasher = new PasswordHasher();
            var result = hasher.VerifyHashedPassword(admin.PasswordHash, dto.Password);
            if (result != PasswordVerificationResult.Success)
                return Unauthorized();

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes("2acd08cb52af1c851161fb4837cb699c05bb87e5c55c66cd83359720c2a8fe02");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] {
                    new Claim(ClaimTypes.Name, admin.Username),
                    new Claim(ClaimTypes.Role, admin.Role.ToString())
                }),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);


            return Ok(new AdminResponseDto
            {
                Username = admin.Username,
                Role = admin.Role.ToString(),
                Token = tokenString
            });
        }
    }
}
