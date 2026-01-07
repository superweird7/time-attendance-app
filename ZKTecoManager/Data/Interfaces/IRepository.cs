using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZKTecoManager.Data.Interfaces
{
    /// <summary>
    /// Generic repository interface for basic CRUD operations.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    public interface IRepository<T> where T : class
    {
        /// <summary>
        /// Gets all entities.
        /// </summary>
        Task<List<T>> GetAllAsync();

        /// <summary>
        /// Gets an entity by its ID.
        /// </summary>
        Task<T> GetByIdAsync(int id);

        /// <summary>
        /// Adds a new entity and returns its ID.
        /// </summary>
        Task<int> AddAsync(T entity);

        /// <summary>
        /// Updates an existing entity.
        /// </summary>
        Task UpdateAsync(T entity);

        /// <summary>
        /// Deletes an entity by its ID.
        /// </summary>
        Task DeleteAsync(int id);
    }
}
