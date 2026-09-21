using FluentValidation.Results;

namespace WaterbusSystem.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ ném ra khi request không vượt qua các quy tắc kiểm tra của FluentValidation
/// </summary>
public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException()
        : base("Đã xảy ra một hoặc nhiều lỗi kiểm tra tính hợp lệ của dữ liệu.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : this()
    {
        Errors = failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());
    }
}
