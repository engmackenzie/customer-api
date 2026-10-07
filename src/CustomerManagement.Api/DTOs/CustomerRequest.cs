using System.ComponentModel.DataAnnotations;

namespace CustomerManagement.Api.DTOs;

public sealed class CustomerRequest
{
    [Required, MaxLength(100)]
    public string FirstName { get; init => field = value?.Trim() ?? string.Empty; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init => field = value?.Trim() ?? string.Empty; } = string.Empty;

    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; init => field = value?.Trim() ?? string.Empty; } = string.Empty;

    [Required, MaxLength(32)]
    public string PhoneNumber { get; init => field = value?.Trim() ?? string.Empty; } = string.Empty;
}
