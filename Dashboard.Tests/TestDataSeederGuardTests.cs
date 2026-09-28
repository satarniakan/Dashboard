using Dashboard.Application.Services;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dashboard.Tests;

/// <summary>
/// سیدِ دیتای نمایشی پیش از این هیچ گارد محیطی‌ای در خودِ سرویس نداشت (فقط صفحه چک می‌کرد)
/// و «خالی‌بودن دیتابیس» را تنها با Products می‌سنجد.
/// </summary>
public class TestDataSeederGuardTests
{
    private sealed class Env : IHostEnvironment
    {
        public Env(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
        public Guid InstanceId { get; } = Guid.NewGuid();
    }

    private static TestDataSeederService Build(IUnitOfWork uow, string environment)
        => new(uow,
            Mock.Of<IStockService>(), Mock.Of<ISalesService>(), Mock.Of<ITreasuryService>(),
            Mock.Of<IInstallmentService>(), Mock.Of<ICartService>(),
            new Env(environment), NullLogger<TestDataSeederService>.Instance);

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Seed_NonDevelopment_Throws_BeforeTouchingAnything(string environment)
    {
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Build(uow.Object, environment).SeedAsync(null));

        // هیچ خواند/نوشتی نباید انجام شده باشد
        uow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IsDatabaseEmpty_NoProductsButCustomers_IsNotEmpty()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.Products).Returns(Mock.Of<IProductRepository>());
        uow.Setup(u => u.Customers).Returns(Mock.Of<ICustomerRepository>());

        // Products خالی ولی یک مشتریِ واقعی ثبت‌شده
        uow.Setup(u => u.Products.GetAllAsync()).ReturnsAsync(Array.Empty<Product>());
        uow.Setup(u => u.Customers.GetAllAsync())
            .ReturnsAsync(new List<Customer> { new() { Name = "مشتری واقعی" } });

        Assert.False(await Build(uow.Object, Environments.Development).IsDatabaseEmptyAsync());
    }

    [Fact]
    public async Task IsDatabaseEmpty_BothEmpty_IsEmpty()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.Products).Returns(Mock.Of<IProductRepository>());
        uow.Setup(u => u.Customers).Returns(Mock.Of<ICustomerRepository>());
        uow.Setup(u => u.Products.GetAllAsync()).ReturnsAsync(Array.Empty<Product>());
        uow.Setup(u => u.Customers.GetAllAsync()).ReturnsAsync(Array.Empty<Customer>());

        Assert.True(await Build(uow.Object, Environments.Development).IsDatabaseEmptyAsync());
    }
}
