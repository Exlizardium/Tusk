namespace Tusk.Api.Dtos.Admin
{
    public class UsersWithRolesDto
    {
        public string Id { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public List<string> Roles { get; set; } = new();
    }
}
