namespace Dashboard.Application.DTOs;

public record ProvinceDto(int Id, string Name);
public record CityDto(int Id, string Name, int ProvinceId);