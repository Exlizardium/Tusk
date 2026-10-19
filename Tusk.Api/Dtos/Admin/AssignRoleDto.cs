using System.ComponentModel.DataAnnotations;
using Tusk.Api.Models.Enums;

namespace Tusk.Api.Dtos.Admin
{
    public class AssignRoleDto
    {
        [Required]
        public string userId { get; set; } = null!;

        [EnumDataType(typeof(RoleType))]
        public RoleType RoleName { get; set; }
    }
}
