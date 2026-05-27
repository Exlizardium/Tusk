using Tusk.Api.Models.Enums;

namespace Tusk.Api.Dtos.Admin
{
    public class AssignRoleDto
    {
        public string userId { get; set; } = null!;
        public RoleType RoleName { get; set; }
    }
}
