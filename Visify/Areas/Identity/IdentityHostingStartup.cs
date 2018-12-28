﻿using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Visify.Areas.Identity.Data;
using Visify.Models;

[assembly: HostingStartup(typeof(Visify.Areas.Identity.IdentityHostingStartup))]
namespace Visify.Areas.Identity
{
    public class IdentityHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) => {
                services.AddDbContext<VisifyContext>(options =>
                    options.UseSqlite(
                        context.Configuration.GetConnectionString("VisifyContextConnection")));

                services.AddDefaultIdentity<VisifyUser>((x) => {
                    x.Password.RequiredLength = 4;
                    x.Password.RequiredUniqueChars = 0;
                    x.Password.RequireNonAlphanumeric = false;
                    x.Password.RequireDigit = false;
                    x.Password.RequireLowercase = false;
                    x.Password.RequireUppercase = false;
                })
                    .AddRoles<IdentityRole>()
                    .AddEntityFrameworkStores<VisifyContext>();
            });
        }
    }
}