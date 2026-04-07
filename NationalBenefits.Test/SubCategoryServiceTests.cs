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
public class SubCategoryServiceTests
{
    private readonly Mock<ISubCategoryRepository> _mockRepository;
    private readonly SubCategoryService _service;

    public SubCategoryServiceTests()
    {
        _mockRepository = new Mock<ISubCategoryRepository>();
        _service = new SubCategoryService(_mockRepository.Object);
    }

    [TestMethod]
    public async Task GetSubCategoryAsync_ReturnsSubCategories()
    {
        var expected = new List<SubCategory>
        {
            new() { Id = Guid.NewGuid(), Code = "CAT-001", Description = "Category A", CategoryId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), Code = "CAT-002", Description = "Category B", CategoryId = Guid.NewGuid() }
        };

        _mockRepository
            .Setup(r => r.GetSubCategoryAsync(It.IsAny<CancellationToken>(), 1, 10, It.IsAny<string>()))
            .ReturnsAsync(expected);

        var result = await _service.GetSubCategoryAsync(CancellationToken.None, 1, 10);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task GetSubCategoryAsync_WithPagination_CallsRepositoryWithCorrectParams()
    {
        _mockRepository
            .Setup(r => r.GetSubCategoryAsync(It.IsAny<CancellationToken>(), 3, 5, It.IsAny<string>()))
            .ReturnsAsync(new List<SubCategory>());

        await _service.GetSubCategoryAsync(CancellationToken.None, 3, 5);

        _mockRepository.Verify(r => r.GetSubCategoryAsync(It.IsAny<CancellationToken>(), 3, 5, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task GetSubCategorybyIdAsync_ReturnsSubCategory_WhenFound()
    {
        var id = Guid.NewGuid();
        var expected = new SubCategory
        {
            Id = id,
            Code = "CAT-X",
            Description = "Category X",
            CategoryId = Guid.NewGuid()
        };

        _mockRepository
            .Setup(r => r.GetSubCategorybyIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetSubCategorybyIdAsync(id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(id, result.Id);
        Assert.AreEqual("CAT-X", result.Code);
    }

    [TestMethod]
    public async Task GetSubCategorybyIdAsync_ReturnsNull_WhenNotFound()
    {
        var id = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetSubCategorybyIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubCategory?)null);

        var result = await _service.GetSubCategorybyIdAsync(id, CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SaveSubCategoryAsync_ReturnsTrue_OnSuccess()
    {
        var request = new CreateSubCategoryRequest
        {
            Id = Guid.NewGuid(),
            Code = "CAT-NEW",
            Description = "New Category",
            CategoryId = Guid.NewGuid()
        };

        _mockRepository
            .Setup(r => r.SaveSubCategory(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.SaveSubCategoryAsync(request, CancellationToken.None);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task SaveSubCategoryAsync_ReturnsFalse_WhenRepositoryFails()
    {
        var request = new CreateSubCategoryRequest
        {
            Id = Guid.NewGuid(),
            Code = "CAT-FAIL",
            Description = "Failing Category",
            CategoryId = Guid.NewGuid()
        };

        _mockRepository
            .Setup(r => r.SaveSubCategory(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.SaveSubCategoryAsync(request, CancellationToken.None);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SaveSubCategoryBulkAsync_ReturnsTrue_OnSuccess()
    {
        var requests = new List<CreateBulkSubCategoryRequest>
        {
            new() { Code = "BULK-001", Description = "Bulk Cat 1", CategoryId = Guid.NewGuid() },
            new() { Code = "BULK-002", Description = "Bulk Cat 2", CategoryId = Guid.NewGuid() }
        };

        _mockRepository
            .Setup(r => r.SaveSubCategoryBulk(requests, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.SaveSubCategoryBulkAsync(requests, CancellationToken.None);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task SaveSubCategoryBulkAsync_CallsRepositoryOnce()
    {
        var requests = new List<CreateBulkSubCategoryRequest>
        {
            new() { Code = "BULK-001", Description = "Bulk Cat 1", CategoryId = Guid.NewGuid() }
        };

        _mockRepository
            .Setup(r => r.SaveSubCategoryBulk(It.IsAny<List<CreateBulkSubCategoryRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _service.SaveSubCategoryBulkAsync(requests, CancellationToken.None);

        _mockRepository.Verify(r => r.SaveSubCategoryBulk(It.IsAny<List<CreateBulkSubCategoryRequest>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UpdateSubCategoryAsync_ReturnsTrue_OnSuccess()
    {
        var request = new UpdateSubCategoryRequest
        {
            Id = Guid.NewGuid(),
            Code = "CAT-UPD",
            Description = "Updated Category",
            CategoryId = Guid.NewGuid()
        };

        _mockRepository
            .Setup(r => r.UpdateSubCategory(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.UpdateSubCategoryAsync(request, CancellationToken.None);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task UpdateSubCategoryAsync_ReturnsFalse_WhenNotFound()
    {
        var request = new UpdateSubCategoryRequest
        {
            Id = Guid.NewGuid(),
            Code = "CAT-MISS",
            Description = "Missing Category",
            CategoryId = Guid.NewGuid()
        };

        _mockRepository
            .Setup(r => r.UpdateSubCategory(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.UpdateSubCategoryAsync(request, CancellationToken.None);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task DeleteSubCategoryAsync_ReturnsTrue_WhenExists()
    {
        var id = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.DeleteSubCategory(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DeleteSubCategoryAsync(id, CancellationToken.None);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task DeleteSubCategoryAsync_ReturnsFalse_WhenNotFound()
    {
        var id = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.DeleteSubCategory(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.DeleteSubCategoryAsync(id, CancellationToken.None);

        Assert.IsFalse(result);
    }
}
