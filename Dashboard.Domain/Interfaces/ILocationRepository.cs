using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface ILocationRepository
{
    Task<IEnumerable<Province>> GetProvincesAsync();
    Task<IEnumerable<City>> GetAllCitiesAsync();
    Task<IEnumerable<City>> GetCitiesByProvinceAsync(int provinceId);
    Task<Province?> GetProvinceByIdAsync(int id);
    Task<City?> GetCityByIdAsync(int id);
}