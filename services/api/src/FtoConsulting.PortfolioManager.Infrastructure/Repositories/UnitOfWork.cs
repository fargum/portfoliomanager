using Microsoft.EntityFrameworkCore.Storage;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Infrastructure.Data;

namespace FtoConsulting.PortfolioManager.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly PortfolioManagerDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(PortfolioManagerDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        try
        {
            // Check if there's already an active transaction
            if (_transaction != null)
            {
                throw new InvalidOperationException("A transaction is already active. Call CommitTransactionAsync or RollbackTransactionAsync first.");
            }

            // Check if the database context already has a transaction
            if (_context.Database.CurrentTransaction != null)
            {
                throw new InvalidOperationException("The database context already has an active transaction.");
            }

            _transaction = await _context.Database.BeginTransactionAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to begin database transaction: {ex.Message}", ex);
        }
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}