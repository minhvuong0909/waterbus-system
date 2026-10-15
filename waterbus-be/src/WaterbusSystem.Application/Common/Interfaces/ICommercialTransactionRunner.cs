namespace WaterbusSystem.Application.Common.Interfaces;

/// <summary>Thực thi thay đổi tiền/ghế nguyên tử; mọi writer thương mại dùng cùng khóa DB.</summary>
public interface ICommercialTransactionRunner
{
    Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
}
