namespace ZooSanMarino.Domain.Entities
{
    public class CompanyLogo
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public byte[] LogoBytes { get; set; } = null!;
        public string LogoContentType { get; set; } = null!;

        public Company Company { get; set; } = null!;
    }
}
