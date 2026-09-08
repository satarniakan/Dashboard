namespace Dashboard.Application.DTOs;

public record WarehouseDto(int Id, string Name, string? Code, string? Address, bool IsActive);

public class CreateWarehouseDto
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Address { get; set; }
}
