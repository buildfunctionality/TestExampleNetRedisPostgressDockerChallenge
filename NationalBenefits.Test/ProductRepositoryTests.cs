using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Products.Api.Contracts;
using Products.Api.Database;
using Products.Api.Entities;
using Products.Api.Services;

namespace NationalBenefits.Test;

[TestClass]
public class ProductRepositoryTests
{
    private ApplicationDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Products.Api.Entities.Products MakeProduct(string name = "Product", string ski = "SKI001") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"Description for {name}",
            Ski = ski,
            Subcategory = Guid.NewGuid(),
            Createdat = DateTimeOffset.UtcNow,
            Updatedat = DateTimeOffset.UtcNow
        };

    // ── GetProductsAsync ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetProductsAsync_ReturnsAllProducts()
    {
        using var context = CreateInMemoryContext(nameof(GetProductsAsync_ReturnsAllProducts));
        await context.Products.AddRangeAsync(MakeProduct("Alpha"), MakeProduct("Beta"));
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var result = await repo.GetProductsAsync(CancellationToken.None, 1, 10);

        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task GetProductsAsync_WithPagination_ReturnsCorrectPage()
    {
        using var context = CreateInMemoryContext(nameof(GetProductsAsync_WithPagination_ReturnsCorrectPage));
        await context.Products.AddRangeAsync(
            MakeProduct("A"), MakeProduct("B"), MakeProduct("C"), MakeProduct("D"), MakeProduct("E"));
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);

        var page1 = await repo.GetProductsAsync(CancellationToken.None, 1, 2);
        var page2 = await repo.GetProductsAsync(CancellationToken.None, 2, 2);
        var page3 = await repo.GetProductsAsync(CancellationToken.None, 3, 2);

        Assert.AreEqual(2, page1.Count());
        Assert.AreEqual(2, page2.Count());
        Assert.AreEqual(1, page3.Count());
    }

    [TestMethod]
    public async Task GetProductsAsync_FiltersByName_ReturnsMatchingProducts()
    {
        using var context = CreateInMemoryContext(nameof(GetProductsAsync_FiltersByName_ReturnsMatchingProducts));
        await context.Products.AddRangeAsync(
            MakeProduct("Playstation 5"), MakeProduct("Playstation 4"), MakeProduct("Xbox Series X"));
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var result = await repo.GetProductsAsync(CancellationToken.None, 1, 10, "Play");

        Assert.AreEqual(2, result.Count());
        Assert.IsTrue(result.All(p => p.Name.StartsWith("Play")));
    }

    [TestMethod]
    public async Task GetProductsAsync_ReturnsEmpty_WhenNoMatchingName()
    {
        using var context = CreateInMemoryContext(nameof(GetProductsAsync_ReturnsEmpty_WhenNoMatchingName));
        await context.Products.AddAsync(MakeProduct("Xbox Series X"));
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var result = await repo.GetProductsAsync(CancellationToken.None, 1, 10, "Nintendo");

        Assert.AreEqual(0, result.Count());
    }

    // ── GetProductbyIdAsync ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetProductbyIdAsync_ReturnsProduct_WhenExists()
    {
        using var context = CreateInMemoryContext(nameof(GetProductbyIdAsync_ReturnsProduct_WhenExists));
        var product = MakeProduct("Find Me");
        await context.Products.AddAsync(product);
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var result = await repo.GetProductbyIdAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(product.Id, result.Id);
        Assert.AreEqual("Find Me", result.Name);
    }

    [TestMethod]
    public async Task GetProductbyIdAsync_ReturnsNull_WhenNotFound()
    {
        using var context = CreateInMemoryContext(nameof(GetProductbyIdAsync_ReturnsNull_WhenNotFound));
        var repo = new ProductRepository(context);

        var result = await repo.GetProductbyIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsNull(result);
    }

    // ── SaveProduct ────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SaveProduct_ReturnsTrue_AndPersistsProduct()
    {
        using var context = CreateInMemoryContext(nameof(SaveProduct_ReturnsTrue_AndPersistsProduct));
        var repo = new ProductRepository(context);
        var request = new CreateProductRequest
        {
            Id = Guid.NewGuid(),
            Name = "New Product",
            Description = "New Desc",
            Ski = "SKI-NEW",
            SubCategoryId = Guid.NewGuid()
        };

        var result = await repo.SaveProduct(request, CancellationToken.None);

        Assert.IsTrue(result);
        Assert.AreEqual(1, await context.Products.CountAsync());
        var saved = await context.Products.FirstAsync();
        Assert.AreEqual("New Product", saved.Name);
        Assert.AreEqual("SKI-NEW", saved.Ski);
    }

    [TestMethod]
    public async Task SaveProduct_ReturnsFalse_WhenRequestIsNull()
    {
        using var context = CreateInMemoryContext(nameof(SaveProduct_ReturnsFalse_WhenRequestIsNull));
        var repo = new ProductRepository(context);

        var result = await repo.SaveProduct(null!, CancellationToken.None);

        Assert.IsFalse(result);
        Assert.AreEqual(0, await context.Products.CountAsync());
    }

    // ── UpdateProduct ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateProduct_ReturnsTrue_AndUpdatesFields()
    {
        using var context = CreateInMemoryContext(nameof(UpdateProduct_ReturnsTrue_AndUpdatesFields));
        var product = MakeProduct("Original Name", "SKI-OLD");
        await context.Products.AddAsync(product);
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var request = new UpdateProductRequest
        {
            Id = product.Id,
            Name = "Updated Name",
            Description = "Updated Desc",
            Ski = "SKI-NEW",
            SubCategory = Guid.NewGuid()
        };

        var result = await repo.UpdateProduct(request, CancellationToken.None);

        Assert.IsTrue(result);
        var updated = await context.Products.FindAsync(product.Id);
        Assert.AreEqual("Updated Name", updated!.Name);
        Assert.AreEqual("Updated Desc", updated.Description);
        Assert.AreEqual("SKI-NEW", updated.Ski);
    }

    [TestMethod]
    public async Task UpdateProduct_ReturnsFalse_WhenProductNotFound()
    {
        using var context = CreateInMemoryContext(nameof(UpdateProduct_ReturnsFalse_WhenProductNotFound));
        var repo = new ProductRepository(context);
        var request = new UpdateProductRequest
        {
            Id = Guid.NewGuid(),
            Name = "Ghost Product",
            Description = "Desc",
            Ski = "SKI-GHOST"
        };

        var result = await repo.UpdateProduct(request, CancellationToken.None);

        Assert.IsFalse(result);
    }

    // ── DeleteProduct ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteProduct_ReturnsTrue_AndRemovesProduct()
    {
        using var context = CreateInMemoryContext(nameof(DeleteProduct_ReturnsTrue_AndRemovesProduct));
        var product = MakeProduct("To Delete");
        await context.Products.AddAsync(product);
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        var result = await repo.DeleteProduct(product.Id, CancellationToken.None);

        Assert.IsTrue(result);
        Assert.AreEqual(0, await context.Products.CountAsync());
    }

    [TestMethod]
    public async Task DeleteProduct_ReturnsFalse_WhenProductNotFound()
    {
        using var context = CreateInMemoryContext(nameof(DeleteProduct_ReturnsFalse_WhenProductNotFound));
        var repo = new ProductRepository(context);

        var result = await repo.DeleteProduct(Guid.NewGuid(), CancellationToken.None);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task DeleteProduct_OnlyRemovesTargetProduct()
    {
        using var context = CreateInMemoryContext(nameof(DeleteProduct_OnlyRemovesTargetProduct));
        var toDelete = MakeProduct("Delete Me");
        var toKeep = MakeProduct("Keep Me");
        await context.Products.AddRangeAsync(toDelete, toKeep);
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);
        await repo.DeleteProduct(toDelete.Id, CancellationToken.None);

        Assert.AreEqual(1, await context.Products.CountAsync());
        Assert.IsNotNull(await context.Products.FindAsync(toKeep.Id));
    }
}
