using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using WaterbusSystem.Infrastructure.Services.Payment;
using Xunit;

namespace WaterbusSystem.UnitTests.Security;

/// <summary>
/// Kiểm thử bảo mật chống Timing Attack và kiểm tra thứ tự sắp xếp ASCII cho VNPAY
/// </summary>
public class VnPaySecurityTests
{
    [Fact]
    public void VnPayAsciiComparer_ShouldSortKeysInAsciiOrdinalOrder()
    {
        // Arrange
        var keys = new List<string> { "vnp_Version", "vnp_Amount", "vnp_TxnRef", "vnp_Command" };
        var comparer = new VnPayAsciiComparer();

        // Act
        keys.Sort(comparer);

        // Assert
        keys.Should().ContainInOrder("vnp_Amount", "vnp_Command", "vnp_TxnRef", "vnp_Version");
    }

    [Fact]
    public void CryptographicOperations_FixedTimeEquals_ShouldReturnTrueForIdenticalHashes()
    {
        // Arrange
        var hash1 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        var hash2 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        var bytes1 = Encoding.UTF8.GetBytes(hash1);
        var bytes2 = Encoding.UTF8.GetBytes(hash2);

        // Act
        var result = CryptographicOperations.FixedTimeEquals(bytes1, bytes2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CryptographicOperations_FixedTimeEquals_ShouldReturnFalseForTamperedHashes()
    {
        // Arrange
        var originalHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        var tamperedHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b850"; // Sai byte cuối

        var bytes1 = Encoding.UTF8.GetBytes(originalHash);
        var bytes2 = Encoding.UTF8.GetBytes(tamperedHash);

        // Act
        var result = CryptographicOperations.FixedTimeEquals(bytes1, bytes2);

        // Assert
        result.Should().BeFalse();
    }
}
