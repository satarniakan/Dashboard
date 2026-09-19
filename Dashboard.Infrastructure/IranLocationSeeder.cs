using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Dashboard.Domain.Entities;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure;

public static class IranLocationSeeder
{
    public static async Task SeedAsync(IServiceProvider services, string contentRootPath)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // فقط یک‌بار Seed می‌کنیم؛ اگر قبلاً استانی وجود دارد، کاری نمی‌کنیم
        if (await context.Provinces.AnyAsync())
        {
            return;
        }

        var filePath = Path.Combine(contentRootPath, "Data", "iran-provinces.json");
        if (!File.Exists(filePath))
        {
            return; // فایل موجود نیست؛ بی‌سروصدا رد می‌شویم تا اپ کرش نکند
        }

        var json = await File.ReadAllTextAsync(filePath);

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var provincesJson = JsonSerializer.Deserialize<List<ProvinceJson>>(json, options) ?? new();

        foreach (var provinceJson in provincesJson)
        {
            var province = new Province { Name = provinceJson.Name };

            foreach (var cityJson in provinceJson.Cities)
            {
                province.Cities.Add(new City { Name = cityJson.Name });
            }

            context.Provinces.Add(province);
        }

        await context.SaveChangesAsync();
    }

    // این دو کلاس فقط برای خواندن فایل JSON هستند، Entity واقعی نیستند
    private class ProvinceJson
    {
        public string Name { get; set; } = string.Empty;
        public List<CityJson> Cities { get; set; } = new();
    }

    private class CityJson
    {
        public string Name { get; set; } = string.Empty;
    }
}