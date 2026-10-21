using System.ComponentModel.DataAnnotations;
using Tusk.Api.Models.Enums;

namespace Tusk.Api.Dtos.Admin
{
    public class SetUserRoleDto
    {
        [Required]
        [EnumDataType(typeof(RoleType))]
        public RoleType? Role { get; set; }
    }
}
