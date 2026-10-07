using BuildWithAi.Api.Data;
using BuildWithAi.Api.DTOs;
using BuildWithAi.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BuildWithAi.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CustomerResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return await db.Customers.AsNoTracking()
            .OrderBy(customer => customer.Id)
            .Select(CustomerResponse.Projection)
            .ToListAsync(cancellationToken);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .Where(customer => customer.Id == id)
            .Select(CustomerResponse.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        return customer is null ? CustomerNotFound(id) : Ok(customer);
    }

    [HttpPost]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> Create(
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

        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, CustomerResponse.From(customer));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> Update(
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

        return Ok(CustomerResponse.From(customer));
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
