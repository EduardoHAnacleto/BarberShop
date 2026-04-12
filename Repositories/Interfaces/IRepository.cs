using System.Linq.Expressions;

namespace BarberShop.Repositories.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, params Expression<Func<T, object>>[] includes);
    Task<List<T>> GetAllAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        params Expression<Func<T, object>>[] includes);
    Task<T> AddAsync(T entity, params Expression<Func<T, object>>[] includes);
    void Update(T entity, params Expression<Func<T, object>>[] includes);
    void Delete(T entity, params Expression<Func<T, object>>[] includes);
}
