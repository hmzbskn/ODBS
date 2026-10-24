using Advisor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advisor.Infrastructure.Persistence;

/// <summary>Ana uygulamanın kendi veritabanı — OBİS verisini ASLA tutmaz (o veri her
/// zaman <see cref="Advisor.Application.Obis.IObisClient"/> üzerinden canlı çekilir).
/// Burada sadece ana uygulamaya özgü kararlar/kayıtlar var: danışman onayları,
/// anketler, randevular, duyurular, talepler, kullanıcı hesapları.</summary>
public class AdvisorDbContext : DbContext
{
    public AdvisorDbContext(DbContextOptions<AdvisorDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RegistrationReview> RegistrationReviews => Set<RegistrationReview>();

    public DbSet<Survey> Surveys => Set<Survey>();
    public DbSet<SurveyQuestion> SurveyQuestions => Set<SurveyQuestion>();
    public DbSet<SurveyQuestionOption> SurveyQuestionOptions => Set<SurveyQuestionOption>();
    public DbSet<SurveyResponse> SurveyResponses => Set<SurveyResponse>();
    public DbSet<SurveyAnswer> SurveyAnswers => Set<SurveyAnswer>();
    public DbSet<NeedScoreResult> NeedScoreResults => Set<NeedScoreResult>();

    public DbSet<AppointmentSlot> AppointmentSlots => Set<AppointmentSlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<MeetingNote> MeetingNotes => Set<MeetingNote>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementRead> AnnouncementReads => Set<AnnouncementRead>();
    public DbSet<PetitionRequest> PetitionRequests => Set<PetitionRequest>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<Survey>(e =>
        {
            e.HasMany(s => s.Questions).WithOne(q => q.Survey).HasForeignKey(q => q.SurveyId);
            e.HasMany(s => s.Responses).WithOne(r => r.Survey).HasForeignKey(r => r.SurveyId);
        });

        modelBuilder.Entity<SurveyQuestion>(e =>
        {
            e.HasMany(q => q.Options).WithOne(o => o.Question).HasForeignKey(o => o.QuestionId);
        });

        modelBuilder.Entity<SurveyResponse>(e =>
        {
            e.HasMany(r => r.Answers).WithOne(a => a.Response).HasForeignKey(a => a.ResponseId);
        });

        modelBuilder.Entity<Announcement>(e =>
        {
            e.HasMany(a => a.Reads).WithOne(r => r.Announcement).HasForeignKey(r => r.AnnouncementId);
        });

        modelBuilder.Entity<Appointment>(e =>
        {
            e.HasOne(a => a.Slot).WithMany().HasForeignKey(a => a.SlotId);
        });
    }
}
