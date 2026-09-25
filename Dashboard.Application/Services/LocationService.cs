using Dashboard.Domain.Interfaces;
using Dashboard.Application.DTOs;

namespace Dashboard.Application.Services;

public interface ILocationService
{
    Task<List<ProvinceDto>> GetProvincesAsync();
    Task<List<CityDto>> GetCitiesByProvinceAsync(int provinceId);
    Task<List<CityDto>> GetAllCitiesAsync();
}

public class LocationService : ILocationService
{
    private readonly IUnitOfWork _unitOfWork;
    public LocationService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<List<ProvinceDto>> GetProvincesAsync()
    {
        var provinces = await _unitOfWork.Locations.GetProvincesAsync();
        return provinces.Select(p => new ProvinceDto(p.Id, p.Name)).ToList();
    }

    public async Task<List<CityDto>> GetCitiesByProvinceAsync(int provinceId)
    {
        var cities = await _unitOfWork.Locations.GetCitiesByProvinceAsync(provinceId);
        return cities.Select(c => new CityDto(c.Id, c.Name, c.ProvinceId)).ToList();
    }

    public async Task<List<CityDto>> GetAllCitiesAsync()
    {
        var cities = await _unitOfWork.Locations.GetAllCitiesAsync();
        return cities.Select(c => new CityDto(c.Id, c.Name, c.ProvinceId)).ToList();
    }
}