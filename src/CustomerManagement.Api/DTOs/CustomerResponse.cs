using System.Linq.Expressions;
using CustomerManagement.Api.Entities;

namespace CustomerManagement.Api.DTOs;

public sealed record CustomerResponse(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt)
{
    public static Expression<Func<Customer, CustomerResponse>> Projection => customer => new(
        customer.Id, customer.FirstName, customer.LastName,
        customer.Email, customer.PhoneNumber, customer.CreatedAt);

    public static CustomerResponse From(Customer customer) => new(
        customer.Id, customer.FirstName, customer.LastName,
        customer.Email, customer.PhoneNumber, customer.CreatedAt);
}
