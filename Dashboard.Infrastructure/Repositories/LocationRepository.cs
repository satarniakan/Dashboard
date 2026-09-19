using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class LocationRepository : ILocationRepository
{
    private readonly AppDbContext _context;
    public LocationRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<Province>> GetProvincesAsync() =>
        await _context.Provinces.OrderBy(p => p.Name).ToListAsync();

    public async Task<IEnumerable<City>> GetAllCitiesAsync() =>
        await _context.Cities.OrderBy(c => c.Name).ToListAsync();

    public async Task<IEnumerable<City>> GetCitiesByProvinceAsync(int provinceId) =>
        await _context.Cities.Where(c => c.ProvinceId == provinceId).OrderBy(c => c.Name).ToListAsync();

    public async Task<Province?> GetProvinceByIdAsync(int id) =>
        await _context.Provinces.FindAsync(id);

    public async Task<City?> GetCityByIdAsync(int id) =>
        await _context.Cities.FindAsync(id);
}