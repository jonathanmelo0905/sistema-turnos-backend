using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Context
{
    public class RefreshToken
    {
        [Key]
        public Guid TokenId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(250)]
        public string Token { get; set; }

        [Required]
        public DateTime Expiration { get; set; }

        public bool Revoked { get; set; } = false;

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; }
    }
}
