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
