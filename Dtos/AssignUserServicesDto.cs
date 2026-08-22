using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    // DTO para asignar servicios a un usuario
    public class AssignUserServicesDto
    {
        [Required]
        public Guid UserId { get; set; } // ID del colaborador

        [Required]
        public List<Guid> ServiceIds { get; set; } = new(); // Lista de IDs de servicios asignados
    }

}
