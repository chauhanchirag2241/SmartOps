using Microsoft.AspNetCore.Identity;
using SmartOps.Domain.Modules.Identity.Entities;
var hasher = new PasswordHasher<ApplicationUser>();
var user = new ApplicationUser { Id = Guid.NewGuid() };
Console.WriteLine(hasher.HashPassword(user, "Vivek2@19052026"));
