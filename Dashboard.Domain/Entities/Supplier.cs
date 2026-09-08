namespace Dashboard.Domain.Entities;

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    public Supplier() { }

    public Supplier(string name, string? contactPerson = null, string? phone = null, string? address = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نام تأمین‌کننده الزامی است.", nameof(name));
        Name = name;
        ContactPerson = contactPerson;
        Phone = phone;
        Address = address;
    }
}
