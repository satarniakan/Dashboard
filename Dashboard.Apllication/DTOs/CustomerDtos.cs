namespace Dashboard.Application.DTOs;

public record CustomerDto(int Id, string Name, string? Phone, string? Address);

public class CreateCustomerDto
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
}