using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Context
{
    public class AgendaProDbContext : DbContext
    {
        public AgendaProDbContext(DbContextOptions<AgendaProDbContext> options): base(options) 
        {

        }

        public DbSet<User> Users { get; set; }
        public DbSet<Business> Businesses { get; set; }
        public DbSet<Plan> Plans { get; set; }
        public DbSet<Client> Clients {  get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<UserService> UserServices { get; set; }
        public DbSet<BusinessSchedule> BusinessSchedules { get; set; }
        public DbSet<UserSchedule> UserSchedules { get; set; }
        public DbSet<UserScheduleException> UserScheduleExceptions { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<TimeOffRequest> TimeOffRequests { get; set; }
        public DbSet<AppointmentHistory> AppointmentHistory { get; set; }
        public DbSet<StaffNotification> StaffNotifications { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<ValleyConfig> ValleyConfigs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            base.OnModelCreating(modelBuilder);

            // 🔗 Relación uno a muchos: un Business tiene varios Users
            modelBuilder.Entity<Business>()
                .HasMany(b => b.Users)
                .WithOne(u => u.Business)
                .HasForeignKey(u => u.BusinessId);

            // Configuración adicional
            modelBuilder.Entity<Appointment>()
                .Property(a => a.Status)
                .HasDefaultValue("Pending");

            modelBuilder.Entity<Appointment>()
                .Property(a => a.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // 🔹 UserService (clave compuesta)
            modelBuilder.Entity<UserService>()
                .HasKey(us => new { us.UserId, us.ServiceId });

            modelBuilder.Entity<UserService>()
                .HasOne(us => us.User)
                .WithMany(u => u.UserServices)
                .HasForeignKey(us => us.UserId);

            modelBuilder.Entity<UserService>()
                .HasOne(us => us.Service)
                .WithMany(s => s.UserServices)
                .HasForeignKey(us => us.ServiceId);

            // 🔹 Permission (catálogo)
            modelBuilder.Entity<Permission>()
                .HasIndex(p => p.Code)
                .IsUnique();

            modelBuilder.Entity<Permission>().HasData(
                PermissionCodes.Catalog.Select((kv, index) => new Permission
                {
                    PermissionId = index + 1,
                    Code = kv.Key,
                    Description = kv.Value
                })
            );

            // 🔹 UserPermission (clave compuesta)
            modelBuilder.Entity<UserPermission>()
                .HasKey(up => new { up.UserId, up.PermissionId });

            modelBuilder.Entity<UserPermission>()
                .HasOne(up => up.User)
                .WithMany(u => u.UserPermissions)
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserPermission>()
                .HasOne(up => up.Permission)
                .WithMany(p => p.UserPermissions)
                .HasForeignKey(up => up.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserPermission>()
                .HasOne(up => up.GrantedByUser)
                .WithMany()
                .HasForeignKey(up => up.GrantedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔗 Relación uno a muchos: un Business tiene su horario semanal (7 filas, una por día)
            modelBuilder.Entity<Business>()
                .HasMany(b => b.BusinessSchedules)
                .WithOne(bs => bs.Business)
                .HasForeignKey(bs => bs.BusinessId);

            // 🔹 Category: al desaparecer una categoría, sus servicios quedan sin categoría
            modelBuilder.Entity<Category>()
                .HasOne(c => c.Business)
                .WithMany()
                .HasForeignKey(c => c.BusinessId);

            // Las tablas originales (scripts SQL) guardan los Guid como char(36) utf8mb4;
            // las columnas nuevas deben coincidir para que las FK sean compatibles.
            modelBuilder.Entity<Category>().Property(c => c.CategoryId).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<Category>().Property(c => c.BusinessId).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<Service>().Property(s => s.CategoryId).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");

            modelBuilder.Entity<Business>().HasIndex(b => b.Slug).IsUnique().HasDatabaseName("IX_Businesses_Slug");
            modelBuilder.Entity<Business>().Property(b => b.CancelLimitHours).HasDefaultValue(2);
            modelBuilder.Entity<Business>().Property(b => b.Status).HasDefaultValue(BusinessStatus.Approved);
            modelBuilder.Entity<Client>().HasIndex(c => c.AccessToken).HasDatabaseName("IX_Clients_AccessToken");

            foreach (var prop in new[] { "ReviewId", "BusinessId", "AppointmentId", "ClientId", "UserId", "ServiceId", "RepliedByUserId" })
                modelBuilder.Entity<Review>().Property(prop).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<Review>().HasIndex(r => r.AppointmentId).IsUnique().HasDatabaseName("IX_Reviews_Appointment");
            modelBuilder.Entity<Review>().HasIndex(r => new { r.BusinessId, r.CreatedAt }).HasDatabaseName("IX_Reviews_Business");
            modelBuilder.Entity<Review>().HasOne(r => r.Appointment).WithMany().HasForeignKey(r => r.AppointmentId);
            modelBuilder.Entity<Review>().HasOne(r => r.Client).WithMany().HasForeignKey(r => r.ClientId);

            foreach (var prop in new[] { "NotificationId", "BusinessId", "UserId", "AppointmentId" })
                modelBuilder.Entity<StaffNotification>().Property(prop).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<StaffNotification>()
                .HasIndex(n => new { n.UserId, n.ReadAt, n.CreatedAt })
                .HasDatabaseName("IX_StaffNotifications_User");

            modelBuilder.Entity<AppointmentHistory>().Property(h => h.HistoryId).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<AppointmentHistory>().Property(h => h.AppointmentId).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<AppointmentHistory>().Property(h => h.ByUserId).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<Appointment>().Property(a => a.CreatedByUserId).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<AppointmentHistory>()
                .HasOne(h => h.Appointment)
                .WithMany(a => a.History)
                .HasForeignKey(h => h.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Appointment>()
                .HasIndex(a => new { a.BusinessId, a.DateTimeStart })
                .HasDatabaseName("IX_Appointments_Business_Start");

            modelBuilder.Entity<TimeOffRequest>().Property(t => t.TimeOffId).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<TimeOffRequest>().Property(t => t.BusinessId).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<TimeOffRequest>().Property(t => t.UserId).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<TimeOffRequest>().Property(t => t.CreatedByUserId).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<TimeOffRequest>().Property(t => t.ReviewedByUserId).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<TimeOffRequest>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId);
            modelBuilder.Entity<TimeOffRequest>()
                .HasIndex(t => new { t.UserId, t.Status, t.StartDate })
                .HasDatabaseName("IX_TimeOff_User_Status_Start");
            modelBuilder.Entity<TimeOffRequest>()
                .HasIndex(t => new { t.BusinessId, t.Status })
                .HasDatabaseName("IX_TimeOff_Business_Status");

            modelBuilder.Entity<ValleyConfig>().Property(v => v.BusinessId).HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
            modelBuilder.Entity<ValleyConfig>().Property(v => v.Mode).HasDefaultValue(ValleyModes.Percent);

            modelBuilder.Entity<Service>()
                .HasOne(s => s.Category)
                .WithMany(c => c.Services)
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // 🔒 Índices para que el SELECT ... FOR UPDATE de no-solapamiento (CU-04) bloquee
            // solo el rango de fechas relevante por colaborador/cliente, no toda su historia.
            modelBuilder.Entity<Appointment>()
                .HasIndex(a => new { a.UserId, a.DateTimeStart })
                .HasDatabaseName("IX_Appointments_UserId_DateTimeStart");

            modelBuilder.Entity<Appointment>()
                .HasIndex(a => new { a.ClientId, a.DateTimeStart })
                .HasDatabaseName("IX_Appointments_ClientId_DateTimeStart");
        }
    }
}
