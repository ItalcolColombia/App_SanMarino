
namespace ZooSanMarino.Domain.Entities
{
    public class UserRole
{
    public Guid UserId   { get; set; }
    public int  RoleId   { get; set; }
    public int  CompanyId{ get; set; }

    public User    User    { get; set; } = null!;
    public Role    Role    { get; set; } = null!;
    public Company Company { get; set; } = null!;
}

}