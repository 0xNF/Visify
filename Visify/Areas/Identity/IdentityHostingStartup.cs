using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Visify.Areas.Identity.Data;
using Visify.Models;
using Visify.Services;

[assembly: HostingStartup(typeof(Visify.Areas.Identity.IdentityHostingStartup))]
namespace Visify.Areas.Identity
{
    public class IdentityHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            EnvironmentVariableService.PopulateEnvironmentVariables();
            builder.ConfigureServices((context, services) => {
            services.AddDbContext<VisifyContext>(options =>
                options.UseSqlite(AppConstants.ConnectionString));
                        //context.Configuration.GetConnectionString("VisifyContextConnection")));

                services.AddIdentity<VisifyUser, IdentityRole>((x) => {
                    x.Password.RequiredLength = 4;
                    x.Password.RequiredUniqueChars = 0;
                    x.Password.RequireNonAlphanumeric = false;
                    x.Password.RequireDigit = false;
                    x.Password.RequireLowercase = false;
                    x.Password.RequireUppercase = false;
                })
                    .AddDefaultUI()
                    .AddDefaultTokenProviders()
                    .AddEntityFrameworkStores<VisifyContext>();
            });
        }
    }
}