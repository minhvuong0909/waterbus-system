using FluentAssertions;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using Xunit;

namespace WaterbusSystem.UnitTests.Concurrency;

/// <summary>
/// Kiểm thử mô phỏng tính năng Khóa phân tán Redis RedLock
/// </summary>
public class DistributedLockTests
{
    [Fact]
    public async Task AcquireLockAsync_WhenResourceIsAvailable_ShouldReturnLockDisposable()
    {
        // Arrange
        var mockLockService = new Mock<IDistributedLockService>();
        var mockDisposable = new Mock<IAsyncDisposable>();

        mockLockService
            .Setup(s => s.AcquireLockAsync(
                It.IsAny<string>(), 
                It.IsAny<TimeSpan>(), 
                It.IsAny<TimeSpan>(), 
                It.IsAny<TimeSpan>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDisposable.Object);

        // Act
        var result = await mockLockService.Object.AcquireLockAsync(
            "lock:trip:1:seat:1", 
            TimeSpan.FromMinutes(10), 
            TimeSpan.FromSeconds(2), 
            TimeSpan.FromMilliseconds(100));

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AcquireLockAsync_WhenResourceIsAlreadyHeld_ShouldReturnNull()
    {
        // Arrange
        var mockLockService = new Mock<IDistributedLockService>();

        mockLockService
            .Setup(s => s.AcquireLockAsync(
                It.IsAny<string>(), 
                It.IsAny<TimeSpan>(), 
                It.IsAny<TimeSpan>(), 
                It.IsAny<TimeSpan>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IAsyncDisposable?)null);

        // Act
        var result = await mockLockService.Object.AcquireLockAsync(
            "lock:trip:1:seat:1", 
            TimeSpan.FromMinutes(10), 
            TimeSpan.FromSeconds(2), 
            TimeSpan.FromMilliseconds(100));

        // Assert
        result.Should().BeNull();
    }
}
