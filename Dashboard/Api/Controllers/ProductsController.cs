// Dashboard.Api/Controllers/ProductsController.cs
using Microsoft.AspNetCore.Mvc;
using Dashboard.Application.DTOs;
using Dashboard.Application.Services;

namespace Dashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetById(int id)
    {
        var product = await _productService.GetProductAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductDto dto)
    {
        var created = await _productService.CreateProductAsync(dto, null);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
    // Dashboard.Api/Controllers/ProductsController.cs — add this method
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll()
    {
        var products = await _productService.GetAllProductsAsync();
        return Ok(products);
    }
}