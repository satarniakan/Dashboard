namespace Dashboard.Domain.Entities;

public class City
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public int ProvinceId { get; set; }
    public Province? Province { get; set; }
}