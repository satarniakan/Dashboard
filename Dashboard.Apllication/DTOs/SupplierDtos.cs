namespace Dashboard.Application.DTOs;

public record SupplierDto(int Id, string Name, string? ContactPerson, string? Phone, string? Address);

public class CreateSupplierDto
{
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}
