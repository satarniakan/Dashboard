namespace Dashboard.Domain.Entities;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    public Customer() { }

    public Customer(string name, string? phone = null, string? address = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نام مشتری الزامی است.", nameof(name));
        Name = name;
        Phone = phone;
        Address = address;
    }
}