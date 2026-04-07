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
public class CachedProductServiceTests
{
    private Mock<IProductRepository> _mockRepository = null!;
    private Mock<IConnectionMultiplexer> _mockRedis = null!;
    private Mock<IDatabase> _mockDatabase = null!;
    private Logger _logger = null!;
    private CachedProductService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IProductRepository>();
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _logger = new Logger();

        _mockRedis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(_mockDatabase.Object);

        _service = new CachedProductService(_mockRepository.Object, _mockRedis.Object, _logger);
    }

    // ── GetProductsAsync ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetProductsAsync_ReturnsCachedData_WhenCacheHit()
    {
        var products = new List<Products.Api.Entities.Products>
        {
            new() { Id = Guid.NewGuid(), Name = "Cached Product", Description = "From Cache", Ski = "SKI-C1", Subcategory = Guid.NewGuid() }
        };
        var json = JsonSerializer.Serialize(products);

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)json);

        var result = await _service.GetProductsAsync(CancellationToken.None, 1, 10, "");

        Assert.IsNotNull(result);
        var list = new List<Products.Api.Entities.Products>(result);
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("Cached Product", list[0].Name);
        _mockRepository.Verify(r => r.GetProductsAsync(It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task GetProductsAsync_FetchesFromRepository_WhenCacheMiss()
    {
        var dbProducts = new List<Products.Api.Entities.Products>
        {
            new() { Id = Guid.NewGuid(), Name = "DB Product", Description = "From DB", Ski = "SKI-DB1", Subcategory = Guid.NewGuid() }
        };

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockDatabase
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockRepository
            .Setup(r => r.GetProductsAsync(It.IsAny<CancellationToken>(), 1, 10, ""))
            .ReturnsAsync(dbProducts);

        var result = await _service.GetProductsAsync(CancellationToken.None, 1, 10, "");

        Assert.IsNotNull(result);
        _mockRepository.Verify(r => r.GetProductsAsync(It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task GetProductsAsync_FallsBackToRepository_WhenRedisThrows()
    {
        var dbProducts = new List<Products.Api.Entities.Products>
        {
            new() { Id = Guid.NewGuid(), Name = "Fallback Product", Description = "From DB", Ski = "SKI-FB1", Subcategory = Guid.NewGuid() }
        };

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis unavailable"));

        _mockRepository
            .Setup(r => r.GetProductsAsync(It.IsAny<CancellationToken>(), 1, 10, ""))
            .ReturnsAsync(dbProducts);

        var result = await _service.GetProductsAsync(CancellationToken.None, 1, 10, "");

        Assert.IsNotNull(result);
        _mockRepository.Verify(r => r.GetProductsAsync(It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task GetProductsAsync_CachesResult_WhenRepositoryReturnsProducts()
    {
        var dbProducts = new List<Products.Api.Entities.Products>
        {
            new() { Id = Guid.NewGuid(), Name = "Product To Cache", Description = "Desc", Ski = "SKI-TC1", Subcategory = Guid.NewGuid() }
        };

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockDatabase
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockRepository
            .Setup(r => r.GetProductsAsync(It.IsAny<CancellationToken>(), 1, 10, ""))
            .ReturnsAsync(dbProducts);

        await _service.GetProductsAsync(CancellationToken.None, 1, 10, "");

        _mockDatabase.Verify(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [TestMethod]
    public async Task GetProductsAsync_FiltersByName_PassesNameToRepository()
    {
        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockDatabase
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockRepository
            .Setup(r => r.GetProductsAsync(It.IsAny<CancellationToken>(), 1, 10, "Play"))
            .ReturnsAsync(new List<Products.Api.Entities.Products>());

        await _service.GetProductsAsync(CancellationToken.None, 1, 10, "Play");

        _mockRepository.Verify(r => r.GetProductsAsync(It.IsAny<CancellationToken>(), 1, 10, "Play"), Times.AtLeastOnce);
    }

    // ── GetProductbyIdAsync ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetProductbyIdAsync_ReturnsCachedProduct_WhenCacheHit()
    {
        var id = Guid.NewGuid();
        var product = new Products.Api.Entities.Products
        {
            Id = id,
            Name = "Cached Single",
            Description = "From Cache",
            Ski = "SKI-SC1",
            Subcategory = Guid.NewGuid()
        };
        var json = JsonSerializer.Serialize(product);

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)json);

        var result = await _service.GetProductbyIdAsync(id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(id, result.Id);
        Assert.AreEqual("Cached Single", result.Name);
        _mockRepository.Verify(r => r.GetProductbyIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetProductbyIdAsync_FetchesFromRepository_WhenCacheMiss()
    {
        var id = Guid.NewGuid();
        var product = new Products.Api.Entities.Products
        {
            Id = id,
            Name = "DB Single",
            Description = "From DB",
            Ski = "SKI-DS1",
            Subcategory = Guid.NewGuid()
        };

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockDatabase
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockRepository
            .Setup(r => r.GetProductbyIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var result = await _service.GetProductbyIdAsync(id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(id, result.Id);
        _mockRepository.Verify(r => r.GetProductbyIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetProductbyIdAsync_CachesResult_AfterRepositoryFetch()
    {
        var id = Guid.NewGuid();
        var product = new Products.Api.Entities.Products
        {
            Id = id,
            Name = "To Cache",
            Description = "Desc",
            Ski = "SKI-TC2",
            Subcategory = Guid.NewGuid()
        };

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockDatabase
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockRepository
            .Setup(r => r.GetProductbyIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        await _service.GetProductbyIdAsync(id, CancellationToken.None);

        _mockDatabase.Verify(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [TestMethod]
    public async Task GetProductbyIdAsync_DoesNotCache_WhenProductNotFoundInRepository()
    {
        var id = Guid.NewGuid();

        _mockDatabase
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockRepository
            .Setup(r => r.GetProductbyIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Products.Api.Entities.Products?)null);

        var result = await _service.GetProductbyIdAsync(id, CancellationToken.None);

        Assert.IsNull(result);
        _mockDatabase.Verify(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);
    }
}
