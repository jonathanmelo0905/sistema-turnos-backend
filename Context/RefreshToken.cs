using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Context
{
    // Token guarda el hash SHA-256 del refresh token (el valor real solo lo tiene el cliente).
    [Index(nameof(Token))]
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
