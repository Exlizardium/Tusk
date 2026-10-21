using Tusk.Api.Models.Enums;

namespace Tusk.Api.Dtos.Admin
{
    public class UserWithRoleDto
    {
        public string Id { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public RoleType? Role { get; set; }
    }
}
