using System.ComponentModel.DataAnnotations;
using CustomerManagement.Api.Data;
using CustomerManagement.Api.DTOs;
using CustomerManagement.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<CustomerResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<CustomerResponse>>> GetAll(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var customers = db.Customers.AsNoTracking().OrderBy(customer => customer.Id);
        var totalCount = await customers.CountAsync(cancellationToken);
        var data = await customers
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(CustomerResponse.Projection)
            .ToListAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (totalCount + pageSize - 1) / pageSize;

        return new PagedResponse<CustomerResponse>(data, new Pagination(page, pageSize, totalCount, totalPages));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<DataResponse<CustomerResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DataResponse<CustomerResponse>>> GetById(int id, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .Where(customer => customer.Id == id)
            .Select(CustomerResponse.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        return customer is null ? CustomerNotFound(id) : Ok(new DataResponse<CustomerResponse>(customer));
    }

    [HttpPost]
    [ProducesResponseType<DataResponse<CustomerResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DataResponse<CustomerResponse>>> Create(
        CustomerRequest request, CancellationToken cancellationToken)
    {
        if (await db.Customers.AnyAsync(customer => customer.Email == request.Email, cancellationToken))
        {
            return DuplicateEmail();
        }

        var customer = new Customer
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber
        };

        db.Customers.Add(customer);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The unique index also protects requests that pass the pre-check concurrently.
            return DuplicateEmail();
        }

        return CreatedAtAction(
            nameof(GetById), new { id = customer.Id }, new DataResponse<CustomerResponse>(CustomerResponse.From(customer)));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<DataResponse<CustomerResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DataResponse<CustomerResponse>>> Update(
        int id, CustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(customer => customer.Id == id, cancellationToken);
        if (customer is null)
        {
            return CustomerNotFound(id);
        }

        if (await db.Customers.AnyAsync(
            other => other.Id != id && other.Email == request.Email, cancellationToken))
        {
            return DuplicateEmail();
        }

        customer.FirstName = request.FirstName;
        customer.LastName = request.LastName;
        customer.Email = request.Email;
        customer.PhoneNumber = request.PhoneNumber;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return DuplicateEmail();
        }

        return Ok(new DataResponse<CustomerResponse>(CustomerResponse.From(customer)));
    }

    private ObjectResult DuplicateEmail() => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Email already in use",
        detail: "A customer with this email address already exists.");

    private ObjectResult CustomerNotFound(int id) => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Customer not found",
        detail: $"Customer {id} does not exist.");
}
