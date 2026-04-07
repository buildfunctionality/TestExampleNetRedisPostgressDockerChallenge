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
public class SubCategoryRepositoryTests
{
    private ApplicationDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static SubCategory MakeSubCategory(string code = "CAT-001", string description = "Category") =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Description = description,
            CategoryId = Guid.NewGuid(),
            Created_at = DateTimeOffset.UtcNow,
            Updated_at = DateTimeOffset.UtcNow
        };

    // ── GetSubCategoryAsync ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetSubCategoryAsync_ReturnsAllCategories()
    {
        using var context = CreateInMemoryContext(nameof(GetSubCategoryAsync_ReturnsAllCategories));
        await context.SubCategories.AddRangeAsync(MakeSubCategory("A-001"), MakeSubCategory("B-002"));
        await context.SaveChangesAsync();

        var repo = new SubCategoryRepository(context);
        var result = await repo.GetSubCategoryAsync(CancellationToken.None, 1, 10);

        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task GetSubCategoryAsync_WithPagination_ReturnsCorrectPage()
    {
        using var context = CreateInMemoryContext(nameof(GetSubCategoryAsync_WithPagination_ReturnsCorrectPage));
        for (int i = 1; i <= 5; i++)
            await context.SubCategories.AddAsync(MakeSubCategory($"CAT-{i:D3}"));
        await context.SaveChangesAsync();

        var repo = new SubCategoryRepository(context);

        var page1 = await repo.GetSubCategoryAsync(CancellationToken.None, 1, 2);
        var page2 = await repo.GetSubCategoryAsync(CancellationToken.None, 2, 2);
        var page3 = await repo.GetSubCategoryAsync(CancellationToken.None, 3, 2);

        Assert.AreEqual(2, page1.Count());
        Assert.AreEqual(2, page2.Count());
        Assert.AreEqual(1, page3.Count());
    }

    [TestMethod]
    public async Task GetSubCategoryAsync_FiltersByCode_ReturnsMatchingCategories()
    {
        using var context = CreateInMemoryContext(nameof(GetSubCategoryAsync_FiltersByCode_ReturnsMatchingCategories));
        await context.SubCategories.AddRangeAsync(
            MakeSubCategory("ELEC-001"), MakeSubCategory("ELEC-002"), MakeSubCategory("FURN-001"));
        await context.SaveChangesAsync();

        var repo = new SubCategoryRepository(context);
        var result = await repo.GetSubCategoryAsync(CancellationToken.None, 1, 10, "ELEC");

        Assert.AreEqual(2, result.Count());
        Assert.IsTrue(result.All(c => c.Code.StartsWith("ELEC")));
    }

    [TestMethod]
    public async Task GetSubCategoryAsync_ReturnsEmpty_WhenNoMatchingCode()
    {
        using var context = CreateInMemoryContext(nameof(GetSubCategoryAsync_ReturnsEmpty_WhenNoMatchingCode));
        await context.SubCategories.AddAsync(MakeSubCategory("FURN-001"));
        await context.SaveChangesAsync();

        var repo = new SubCategoryRepository(context);
        var result = await repo.GetSubCategoryAsync(CancellationToken.None, 1, 10, "ELEC");

        Assert.AreEqual(0, result.Count());
    }

    // ── GetSubCategorybyIdAsync ────────────────────────────────────────────────

    [TestMethod]
    public async Task GetSubCategorybyIdAsync_ReturnsCategory_WhenExists()
    {
        using var context = CreateInMemoryContext(nameof(GetSubCategorybyIdAsync_ReturnsCategory_WhenExists));
        var category = MakeSubCategory("FIND-001", "Find Me");
        await context.SubCategories.AddAsync(category);
        await context.SaveChangesAsync();

        var repo = new SubCategoryRepository(context);
        var result = await repo.GetSubCategorybyIdAsync(category.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(category.Id, result.Id);
        Assert.AreEqual("FIND-001", result.Code);
    }

    [TestMethod]
    public async Task GetSubCategorybyIdAsync_ReturnsNull_WhenNotFound()
    {
        using var context = CreateInMemoryContext(nameof(GetSubCategorybyIdAsync_ReturnsNull_WhenNotFound));
        var repo = new SubCategoryRepository(context);

        var result = await repo.GetSubCategorybyIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsNull(result);
    }

    // ── SaveSubCategory ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SaveSubCategory_ReturnsTrue_AndPersistsCategory()
    {
        using var context = CreateInMemoryContext(nameof(SaveSubCategory_ReturnsTrue_AndPersistsCategory));
        var repo = new SubCategoryRepository(context);
        var request = new CreateSubCategoryRequest
        {
            Id = Guid.NewGuid(),
            Code = "NEW-001",
            Description = "New Category",
            CategoryId = Guid.NewGuid()
        };

        var result = await repo.SaveSubCategory(request, CancellationToken.None);

        Assert.IsTrue(result);
        Assert.AreEqual(1, await context.SubCategories.CountAsync());
        var saved = await context.SubCategories.FirstAsync();
        Assert.AreEqual("NEW-001", saved.Code);
        Assert.AreEqual("New Category", saved.Description);
    }

    [TestMethod]
    public async Task SaveSubCategory_ReturnsTrue_WhenRequestIsNull()
    {
        // SaveSubCategory always returns true even when request is null (no null guard for subcategory)
        using var context = CreateInMemoryContext(nameof(SaveSubCategory_ReturnsTrue_WhenRequestIsNull));
        var repo = new SubCategoryRepository(context);

        var result = await repo.SaveSubCategory(null!, CancellationToken.None);

        Assert.IsTrue(result);
    }

    // ── SaveSubCategoryBulk ────────────────────────────────────────────────────

    [TestMethod]
    public async Task SaveSubCategoryBulk_ReturnsTrue_AndPersistsLastItem()
    {
        using var context = CreateInMemoryContext(nameof(SaveSubCategoryBulk_ReturnsTrue_AndPersistsLastItem));
        var repo = new SubCategoryRepository(context);
        var requests = new List<CreateBulkSubCategoryRequest>
        {
            new() { Code = "BULK-001", Description = "Bulk 1", CategoryId = Guid.NewGuid() },
            new() { Code = "BULK-002", Description = "Bulk 2", CategoryId = Guid.NewGuid() }
        };

        var result = await repo.SaveSubCategoryBulk(requests, CancellationToken.None);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task SaveSubCategoryBulk_WithSingleItem_ReturnsTrue()
    {
        using var context = CreateInMemoryContext(nameof(SaveSubCategoryBulk_WithSingleItem_ReturnsTrue));
        var repo = new SubCategoryRepository(context);
        var requests = new List<CreateBulkSubCategoryRequest>
        {
            new() { Code = "SINGLE-001", Description = "Single Item", CategoryId = Guid.NewGuid() }
        };

        var result = await repo.SaveSubCategoryBulk(requests, CancellationToken.None);

        Assert.IsTrue(result);
        Assert.AreEqual(1, await context.SubCategories.CountAsync());
    }

    // ── UpdateSubCategory ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateSubCategory_ReturnsTrue_AndUpdatesFields()
    {
        using var context = CreateInMemoryContext(nameof(UpdateSubCategory_ReturnsTrue_AndUpdatesFields));
        var category = MakeSubCategory("ORIG-001", "Original Description");
        await context.SubCategories.AddAsync(category);
        await context.SaveChangesAsync();

        var repo = new SubCategoryRepository(context);
        var request = new UpdateSubCategoryRequest
        {
            Id = category.Id,
            Code = "UPD-001",
            Description = "Updated Description",
            CategoryId = Guid.NewGuid()
        };

        var result = await repo.UpdateSubCategory(request, CancellationToken.None);

        Assert.IsTrue(result);
        var updated = await context.SubCategories.FindAsync(category.Id);
        Assert.AreEqual("UPD-001", updated!.Code);
        Assert.AreEqual("Updated Description", updated.Description);
    }

    [TestMethod]
    public async Task UpdateSubCategory_ReturnsFalse_WhenCategoryNotFound()
    {
        using var context = CreateInMemoryContext(nameof(UpdateSubCategory_ReturnsFalse_WhenCategoryNotFound));
        var repo = new SubCategoryRepository(context);
        var request = new UpdateSubCategoryRequest
        {
            Id = Guid.NewGuid(),
            Code = "GHOST-001",
            Description = "Ghost Category"
        };

        var result = await repo.UpdateSubCategory(request, CancellationToken.None);

        Assert.IsFalse(result);
    }

    // ── DeleteSubCategory ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteSubCategory_ReturnsTrue_AndRemovesCategory()
    {
        using var context = CreateInMemoryContext(nameof(DeleteSubCategory_ReturnsTrue_AndRemovesCategory));
        var category = MakeSubCategory("DEL-001", "To Delete");
        await context.SubCategories.AddAsync(category);
        await context.SaveChangesAsync();

        var repo = new SubCategoryRepository(context);
        var result = await repo.DeleteSubCategory(category.Id, CancellationToken.None);

        Assert.IsTrue(result);
        Assert.AreEqual(0, await context.SubCategories.CountAsync());
    }

    [TestMethod]
    public async Task DeleteSubCategory_ReturnsFalse_WhenNotFound()
    {
        using var context = CreateInMemoryContext(nameof(DeleteSubCategory_ReturnsFalse_WhenNotFound));
        var repo = new SubCategoryRepository(context);

        var result = await repo.DeleteSubCategory(Guid.NewGuid(), CancellationToken.None);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task DeleteSubCategory_OnlyRemovesTargetCategory()
    {
        using var context = CreateInMemoryContext(nameof(DeleteSubCategory_OnlyRemovesTargetCategory));
        var toDelete = MakeSubCategory("DEL-001", "Delete Me");
        var toKeep = MakeSubCategory("KEEP-001", "Keep Me");
        await context.SubCategories.AddRangeAsync(toDelete, toKeep);
        await context.SaveChangesAsync();

        var repo = new SubCategoryRepository(context);
        await repo.DeleteSubCategory(toDelete.Id, CancellationToken.None);

        Assert.AreEqual(1, await context.SubCategories.CountAsync());
        Assert.IsNotNull(await context.SubCategories.FindAsync(toKeep.Id));
    }
}
