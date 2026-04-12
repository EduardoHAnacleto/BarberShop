using BarberShop.Data;
using BarberShop.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BarberShop.Repositories;

public class GenericRepository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(AppDbContext context)
    {
        _context = context;
        _dbSet = _context.Set<T>();
    }
    public async Task<T> AddAsync(T entity, params Expression<Func<T, object>>[] includes)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    public async Task<List<T>> GetAllAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        // Includes
        if (includes != null && includes.Length > 0)
        {
            foreach (var include in includes)
                query = query.Include(include);
        }

        // Filter
        if (filter != null)
            query = query.Where(filter);

        // Order
        if (orderBy != null)
            query = orderBy(query);

        return await query.ToListAsync();
    }

    public async Task<T?> GetByIdAsync(int id, params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        // Includes
        if (includes != null && includes.Length > 0)
        {
            foreach (var include in includes)
                query = query.Include(include);
        }

        var entity = await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);

        return entity;
    }

    public void Update(T entity, params Expression<Func<T, object>>[] includes)
    {
        _dbSet.Update(entity);
    }

    public void Delete(T entity, params Expression<Func<T, object>>[] includes)
    {
        _dbSet.Remove(entity);
    }
}
