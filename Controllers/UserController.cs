using API.DTOs.User;
using API.Models;
using Microsoft.AspNet.Identity;
using Microsoft.IdentityModel.Tokens;
using SHOPAPI.Data;
using SHOPAPI.DTOs.Admin;
using SHOPAPI.Models;
using SHOPAPI.Models.Enum;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Web.Http;

namespace SHOPAPI.Controllers
{
    [RoutePrefix("api")]
    public class UserController : ApiController
    {
        private readonly AppDbContext db = new AppDbContext();

        [HttpPost]
        [Route("login")]
        public IHttpActionResult Login(LoginDto dto)
        {
            var user = db.Users.FirstOrDefault(a => a.Username == dto.Username);
            if (user == null)
                return Unauthorized();

            var hasher = new PasswordHasher();
            var result = hasher.VerifyHashedPassword(user.PasswordHash, dto.Password);
            if (result != PasswordVerificationResult.Success)
                return Unauthorized();

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes("2acd08cb52af1c851161fb4837cb699c05bb87e5c55c66cd83359720c2a8fe02");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role.ToString())
                }),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);


            return Ok(new UserResponseDto
            {
                Username = user.Username,
                Role = user.Role.ToString(),
                Token = tokenString
            });
        }

        [HttpPost]
        [Route("register")]
        public IHttpActionResult Register(Register dto)
        {
            if (db.Users.Any(u => u.Username == dto.Username))
                return BadRequest("Username đã được sử dụng.");

            var hasher = new PasswordHasher();
            var passwordHash = hasher.HashPassword(dto.Password);

            var newUser = new Users
            {
                Username = dto.Username,
                PasswordHash = passwordHash,
                Email = dto.Email,
                Role = Role.USER,
                Name = dto.Name,
                PhoneNumber = dto.PhoneNumber,
                Address = dto.Address
            };

            db.Users.Add(newUser);
            db.SaveChanges();

            db.Carts.Add(new Cart { UserId = newUser.Id });
            db.SaveChanges();

            return Ok(new
            {
                message = "Đăng ký thành công và đã thêm thông tin người dùng."
            });
        }

        [Authorize(Roles = "USER")]
        [HttpGet]
        [Route("user/info")]
        public IHttpActionResult GetUserInfo()
        {
            var identity = (ClaimsIdentity)User.Identity;
            var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();

            int userId = Convert.ToInt32(userIdClaim.Value);
            var user = db.Users.Find(userId);
            if (user == null) return NotFound();

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Name,
                user.Email,
                user.PhoneNumber,
                user.Address
            });
        }

        [Authorize(Roles = "USER")]
        [HttpPut]
        [Route("user/info")]
        public IHttpActionResult UpdateInfo(UserInfoDto dto)
        {
            var identity = (ClaimsIdentity)User.Identity;
            var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();

            int userId = Convert.ToInt32(userIdClaim.Value);
            var user = db.Users.Find(userId);
            if (user == null) return NotFound();

            user.Name = dto.Name;
            user.PhoneNumber = dto.PhoneNumber;
            user.Address = dto.Address;

            db.SaveChanges();

            return Ok(new
            {
                message = "Thông tin đã được cập nhật",
                user = new { user.Id, user.Name, user.PhoneNumber, user.Address }
            });
        }
    }
}
