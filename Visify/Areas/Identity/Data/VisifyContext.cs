using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Visify.Areas.Identity.Data;

namespace Visify.Models
{
    public class VisifyContext : IdentityDbContext<VisifyUser>
    {
        public VisifyContext(DbContextOptions<VisifyContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<VisifyTrack>().HasMany<VisifyArtist>();
            builder.Entity<VisifySavedTrack>().HasKey(x => new { x.UserId, x.TrackId });
            builder.Entity<VisifySavedTrack>().HasOne<VisifyTrack>().WithMany().OnDelete(DeleteBehavior.Cascade);
            builder.Entity<RateLimit>().HasKey(x => x.UserId);

            // Customize the ASP.NET Identity model and override the defaults if needed.
            // For example, you can rename the ASP.NET Identity table names and more.
            // Add your customizations after calling base.OnModelCreating(builder);
        }
    }
}
