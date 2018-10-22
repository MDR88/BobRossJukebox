using System;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using MusicAPI.Data;
using MusicAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace MusicAPI.Controllers
{

    [Route("/api/token")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private ApplicationDbContext _context;
        private readonly SignInManager<User> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        private string[] roles = new [] { "User", "Manager", "Administrator" };
        private async Task InitializeRoles (RoleManager<IdentityRole> roleManager) {
            foreach (var role in roles) {
                if (!await roleManager.RoleExistsAsync (role)) {
                    var newRole = new IdentityRole (role);
                    await roleManager.CreateAsync (newRole);
                    // In the real world, there might be claims associated with roles
                    // _roleManager.AddClaimAsync(newRole, new )
                }
            }
        }


        public TokenController(
            ApplicationDbContext ctx,
            SignInManager<User> signInManager,
            RoleManager<IdentityRole> roleManager
        )
        {
            _roleManager = roleManager;
            _context = ctx;
            _signInManager = signInManager;

            InitializeRoles(roleManager);
        }

        [HttpGet]
        [Authorize]
        public IActionResult Get(){
            return new ObjectResult(new {
                Username = User.Identity.Name
            });
        }

        [HttpPut]
        [Authorize]
        public IActionResult Put(){
            string role = "Administrator";
            return new ObjectResult(GenerateToken(User.Identity.Name));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody]NewUser postUser)
        {
            // Hard coding role here for now
            string role = "Administrator";

            // Check simplistic username and password validation rules
            bool isValid = IsValidUserAndPasswordCombination(postUser.Username, postUser.Password);

            if (isValid)
            {
                // Does the user already exist?
                User user = _context.User.SingleOrDefault(u => u.UserName == postUser.Username);

                if (user != null)
                {
                    // Found the user, verify credentials
                    var result = await _signInManager.PasswordSignInAsync(postUser.Username, postUser.Password, false, lockoutOnFailure: false);

                    // Password is correct, generate token and return it
                    if (result.Succeeded)
                    {
                        return new ObjectResult(GenerateToken(user.UserName));
                    }
                } else
                {
                    var userstore = new UserStore<User>(_context);

                    // User does not exist, create one
                    user = new User {
                        FirstName = "Generic",
                        LastName = "User",
                        UserName = postUser.Username,
                        NormalizedUserName = postUser.Username.ToUpper(),
                        Email = postUser.Username,
                        NormalizedEmail = postUser.Username.ToUpper(),
                        EmailConfirmed = true,
                        LockoutEnabled = false,
                        SecurityStamp = Guid.NewGuid().ToString("D")
                    };
                    var passwordHash = new PasswordHasher<User>();
                    user.PasswordHash = passwordHash.HashPassword(user, postUser.Password);
                    await userstore.CreateAsync(user);
                    // await userstore.AddToRoleAsync(user);
                    _context.SaveChanges();
                    return new ObjectResult(GenerateToken(user.UserName));
                }
            }
            return BadRequest();
        }

        private bool IsValidUserAndPasswordCombination(string username, string password)
        {
            return !string.IsNullOrEmpty(username) && username != password;
        }

        private string GenerateToken(string username)
        {
            var claims = new Claim[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(JwtRegisteredClaimNames.Nbf, new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds().ToString()),
                new Claim(JwtRegisteredClaimNames.Exp, new DateTimeOffset(DateTime.Now.AddDays(1)).ToUnixTimeSeconds().ToString()),
                new Claim(ClaimTypes.Role, "Administrator"),
            };

            var token = new JwtSecurityToken(
                new JwtHeader(new SigningCredentials(
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes("7A735D7B-1A19-4D8A-9CFA-99F55483013F")),
                        SecurityAlgorithms.HmacSha256)
                    ),
                new JwtPayload(claims)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}