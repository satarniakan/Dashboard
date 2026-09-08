namespace Dashboard.Domain.Entities;

public class Warehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Warehouse() { }

    public Warehouse(string name, string? code = null, string? address = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نام انبار الزامی است.", nameof(name));
        Name = name;
        Code = code;
        Address = address;
    }
}
