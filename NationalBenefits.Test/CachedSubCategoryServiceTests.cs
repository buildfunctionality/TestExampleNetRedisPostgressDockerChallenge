using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Products.Api.Entities;
using Products.Api.Services;
using Products.Api.Services.Interfaces;
using Products.Api.Utils;
using StackExchange.Redis;

namespace NationalBenefits.Test;

[TestClass]
public class CachedSubCategoryServiceTests
{
    private Mock<ISubCategoryRepository> _mockRepository = null!;
    private Mock<IConnectionMultiplexer> _mockRedis = null!;
    private Mock<IDatabase> _mockDatabase = null!;
    private Logger _logger = null!;
    private CachedSubCategoryService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<ISubCategoryRepository>();
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _logger = new Logger();

        _mockRedis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(_mockDatabase.Object);

        _service = new CachedSubCategoryService(_mockRepository.Object, _mockRedis.Object, _logger);
    }

    // ── GetSubCategoriesAsync ──────────────────────────────────────────────────

    [TestMethod]
    public async Task GetSubCategoriesAsync_ReturnsCachedData_WhenCacheHit()
    {
        var categories = new List<SubCategory>
        {
            new() { Id = Guid.NewGuid(), Code = "CACHED-CAT", Description = "From Cache", CategoryId = Guid.NewGuid() }
        };
        var json = JsonSerializer.Serialize(categories);

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)json);

        var result = await _service.GetSubCategoriesAsync(CancellationToken.None, 1, 10, "");

        Assert.IsNotNull(result);
        var list = new List<SubCategory>(result);
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("CACHED-CAT", list[0].Code);
        _mockRepository.Verify(r => r.GetSubCategoryAsync(It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task GetSubCategoriesAsync_FetchesFromRepository_WhenCacheMiss()
    {
        var dbCategories = new List<SubCategory>
        {
            new() { Id = Guid.NewGuid(), Code = "DB-CAT", Description = "From DB", CategoryId = Guid.NewGuid() }
        };

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockDatabase
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockRepository
            .Setup(r => r.GetSubCategoryAsync(It.IsAny<CancellationToken>(), 1, 10, ""))
            .ReturnsAsync(dbCategories);

        var result = await _service.GetSubCategoriesAsync(CancellationToken.None, 1, 10, "");

        Assert.IsNotNull(result);
        _mockRepository.Verify(r => r.GetSubCategoryAsync(It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task GetSubCategoriesAsync_FallsBackToRepository_WhenRedisThrows()
    {
        var dbCategories = new List<SubCategory>
        {
            new() { Id = Guid.NewGuid(), Code = "FB-CAT", Description = "Fallback", CategoryId = Guid.NewGuid() }
        };

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis unavailable"));

        _mockRepository
            .Setup(r => r.GetSubCategoryAsync(It.IsAny<CancellationToken>(), 1, 10, ""))
            .ReturnsAsync(dbCategories);

        var result = await _service.GetSubCategoriesAsync(CancellationToken.None, 1, 10, "");

        Assert.IsNotNull(result);
        _mockRepository.Verify(r => r.GetSubCategoryAsync(It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task GetSubCategoriesAsync_CachesResult_WhenRepositoryReturnsCategories()
    {
        var dbCategories = new List<SubCategory>
        {
            new() { Id = Guid.NewGuid(), Code = "TC-CAT", Description = "To Cache", CategoryId = Guid.NewGuid() }
        };

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockDatabase
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockRepository
            .Setup(r => r.GetSubCategoryAsync(It.IsAny<CancellationToken>(), 1, 10, ""))
            .ReturnsAsync(dbCategories);

        await _service.GetSubCategoriesAsync(CancellationToken.None, 1, 10, "");

        _mockDatabase.Verify(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [TestMethod]
    public async Task GetSubCategoriesAsync_FiltersByCode_PassesCodeToRepository()
    {
        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockDatabase
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockRepository
            .Setup(r => r.GetSubCategoryAsync(It.IsAny<CancellationToken>(), 1, 10, "ELEC"))
            .ReturnsAsync(new List<SubCategory>());

        await _service.GetSubCategoriesAsync(CancellationToken.None, 1, 10, "ELEC");

        _mockRepository.Verify(r => r.GetSubCategoryAsync(It.IsAny<CancellationToken>(), 1, 10, "ELEC"), Times.AtLeastOnce);
    }

    // ── GetSubCategorybyIdAsync ────────────────────────────────────────────────

    [TestMethod]
    public async Task GetSubCategorybyIdAsync_ReturnsCachedCategory_WhenCacheHit()
    {
        var id = Guid.NewGuid();
        var category = new SubCategory
        {
            Id = id,
            Code = "CACHED-ID",
            Description = "From Cache",
            CategoryId = Guid.NewGuid()
        };
        var json = JsonSerializer.Serialize(category);

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)json);

        var result = await _service.GetSubCategorybyIdAsync(id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(id, result.Id);
        Assert.AreEqual("CACHED-ID", result.Code);
        _mockRepository.Verify(r => r.GetSubCategorybyIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetSubCategorybyIdAsync_FetchesFromRepository_WhenCacheMiss()
    {
        var id = Guid.NewGuid();
        var category = new SubCategory
        {
            Id = id,
            Code = "DB-ID",
            Description = "From DB",
            CategoryId = Guid.NewGuid()
        };

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockDatabase
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockRepository
            .Setup(r => r.GetSubCategorybyIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var result = await _service.GetSubCategorybyIdAsync(id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(id, result.Id);
        _mockRepository.Verify(r => r.GetSubCategorybyIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetSubCategorybyIdAsync_CachesResult_AfterRepositoryFetch()
    {
        var id = Guid.NewGuid();
        var category = new SubCategory
        {
            Id = id,
            Code = "TC-ID",
            Description = "To Cache",
            CategoryId = Guid.NewGuid()
        };

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockDatabase
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockRepository
            .Setup(r => r.GetSubCategorybyIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        await _service.GetSubCategorybyIdAsync(id, CancellationToken.None);

        _mockDatabase.Verify(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [TestMethod]
    public async Task GetSubCategorybyIdAsync_DoesNotCache_WhenCategoryNotFoundInRepository()
    {
        var id = Guid.NewGuid();

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockRepository
            .Setup(r => r.GetSubCategorybyIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubCategory?)null);

        var result = await _service.GetSubCategorybyIdAsync(id, CancellationToken.None);

        Assert.IsNull(result);
        _mockDatabase.Verify(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);
    }
}
