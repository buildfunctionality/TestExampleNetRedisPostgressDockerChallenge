using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Products.Api.Contracts;
using Products.Api.Entities;
using Products.Api.Services;
using Products.Api.Services.Interfaces;

namespace NationalBenefits.Test;

[TestClass]
public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _mockRepository;
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _mockRepository = new Mock<IProductRepository>();
        _service = new ProductService(_mockRepository.Object);
    }

    [TestMethod]
    public async Task GetProductsAsync_ReturnsProducts()
    {
        var expected = new List<Products.Api.Entities.Products>
        {
            new() { Id = Guid.NewGuid(), Name = "Product A", Description = "Desc A", Ski = "SKI001", Subcategory = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), Name = "Product B", Description = "Desc B", Ski = "SKI002", Subcategory = Guid.NewGuid() }
        };

        _mockRepository
            .Setup(r => r.GetProductsAsync(It.IsAny<CancellationToken>(), 1, 10, It.IsAny<string>()))
            .ReturnsAsync(expected);

        var result = await _service.GetProductsAsync(CancellationToken.None, 1, 10);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task GetProductsAsync_WithPagination_CallsRepositoryWithCorrectParams()
    {
        _mockRepository
            .Setup(r => r.GetProductsAsync(It.IsAny<CancellationToken>(), 2, 5, It.IsAny<string>()))
            .ReturnsAsync(new List<Products.Api.Entities.Products>());

        await _service.GetProductsAsync(CancellationToken.None, 2, 5);

        _mockRepository.Verify(r => r.GetProductsAsync(It.IsAny<CancellationToken>(), 2, 5, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task GetProductbyIdAsync_ReturnsProduct_WhenFound()
    {
        var id = Guid.NewGuid();
        var expected = new Products.Api.Entities.Products
        {
            Id = id,
            Name = "Product X",
            Description = "Desc X",
            Ski = "SKI999",
            Subcategory = Guid.NewGuid()
        };

        _mockRepository
            .Setup(r => r.GetProductbyIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetProductbyIdAsync(id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(id, result.Id);
        Assert.AreEqual("Product X", result.Name);
    }

    [TestMethod]
    public async Task GetProductbyIdAsync_ReturnsNull_WhenNotFound()
    {
        var id = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetProductbyIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Products.Api.Entities.Products?)null);

        var result = await _service.GetProductbyIdAsync(id, CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SaveProductAsync_ReturnsTrue_OnSuccess()
    {
        var request = new CreateProductRequest
        {
            Id = Guid.NewGuid(),
            Name = "New Product",
            Description = "New Desc",
            Ski = "SKI111",
            SubCategoryId = Guid.NewGuid()
        };

        _mockRepository
            .Setup(r => r.SaveProduct(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.SaveProductAsync(request, CancellationToken.None);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task SaveProductAsync_ReturnsFalse_WhenRepositoryFails()
    {
        var request = new CreateProductRequest
        {
            Id = Guid.NewGuid(),
            Name = "Failing Product",
            Description = "Desc",
            Ski = "SKI000",
            SubCategoryId = Guid.NewGuid()
        };

        _mockRepository
            .Setup(r => r.SaveProduct(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.SaveProductAsync(request, CancellationToken.None);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task UpdateProductAsync_ReturnsTrue_OnSuccess()
    {
        var request = new UpdateProductRequest
        {
            Id = Guid.NewGuid(),
            Name = "Updated Product",
            Description = "Updated Desc",
            Ski = "SKI222",
            SubCategory = Guid.NewGuid()
        };

        _mockRepository
            .Setup(r => r.UpdateProduct(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.UpdateProductAsync(request, CancellationToken.None);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task UpdateProductAsync_ReturnsFalse_WhenProductNotFound()
    {
        var request = new UpdateProductRequest
        {
            Id = Guid.NewGuid(),
            Name = "Missing Product",
            Description = "Desc",
            Ski = "SKI333",
            SubCategory = Guid.NewGuid()
        };

        _mockRepository
            .Setup(r => r.UpdateProduct(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.UpdateProductAsync(request, CancellationToken.None);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task DeleteProductAsync_ReturnsTrue_WhenProductExists()
    {
        var id = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.DeleteProduct(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DeleteProductAsync(id, CancellationToken.None);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task DeleteProductAsync_ReturnsFalse_WhenProductNotFound()
    {
        var id = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.DeleteProduct(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.DeleteProductAsync(id, CancellationToken.None);

        Assert.IsFalse(result);
    }
}
