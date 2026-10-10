using Microsoft.EntityFrameworkCore;

namespace CvApi;

public sealed class CvDb : DbContext
{
    public CvDb(DbContextOptions<CvDb> options)
        : base(options)
    {
    }

    public DbSet<CvEntity> Cvs
    {
        get
        {
            return Set<CvEntity>();
        }
    }

    public DbSet<ApiUser> Users
    {
        get
        {
            return Set<ApiUser>();
        }
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ApiUser>()
            .HasKey(user => user.Username);

        var cv = model.Entity<CvEntity>();
        cv.HasIndex(x => x.Owner);
        cv.HasOne<ApiUser>()
            .WithMany()
            .HasForeignKey(x => x.Owner)
            .OnDelete(DeleteBehavior.Cascade);

        cv.OwnsOne(x => x.Data, d =>
        {
            d.OwnsMany(x => x.Languages, languages =>
            {
                languages.ToTable("CvLanguages");
                languages.WithOwner()
                    .HasForeignKey("CvId");
                languages.Property<int>("Id");
                languages.HasKey("Id");
            });

            d.OwnsMany(x => x.WorkExperience, workExperience =>
            {
                workExperience.ToTable("CvWorkExperience");
                workExperience.WithOwner()
                    .HasForeignKey("CvId");
                workExperience.Property<int>("Id");
                workExperience.HasKey("Id");
            });

            d.OwnsMany(x => x.Education, education =>
            {
                education.ToTable("CvEducation");
                education.WithOwner()
                    .HasForeignKey("CvId");
                education.Property<int>("Id");
                education.HasKey("Id");
            });
        });
        cv.Navigation(x => x.Data)
            .IsRequired();
    }
}
