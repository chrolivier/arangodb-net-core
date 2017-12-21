using System.Collections.Generic;
using System.Threading.Tasks;
using BorderEast.ArangoDB.Client.Connection;
using BorderEast.ArangoDB.Client.Models;
using BorderEast.ArangoDB.Client.Models.Collection;
using Newtonsoft.Json;

namespace BorderEast.ArangoDB.Client.Database
{
    public interface IArangoDatabase
    {
        /// <summary>
        /// Create a collection based on the passed in dynamic object 
        /// Example: CreateCollection(new { Name = somename}) 
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        Task<CollectionResult> CreateCollection(dynamic parameters);

        /// <summary>
        /// Create a collection based on the given ArangoCollection
        /// </summary>
        /// <param name="collection"></param>
        /// <returns></returns>
        Task<CollectionResult> CreateCollection(ArangoCollection collection);

        /// <summary>
        /// Ad hoc AQL query that will be serialized to give type T
        /// </summary>
        /// <typeparam name="T">Entity class</typeparam>
        /// <param name="query">AQL query text</param>
        /// <returns>List of entities</returns>
        Task<List<T>> Query<T>(string query);

        /// <summary>
        /// Ad hoc AQL query that will be serialized to give type T
        /// </summary>
        /// <typeparam name="T">Entity class</typeparam>
        /// <param name="query">AQL query text</param>
        /// <param name="parameters">Dynamic object of parmeters, use JSON names (_key instead of Key)</param>
        /// <returns>List of entities</returns>
        Task<List<T>> Query<T>(string query, dynamic parameters);

        /// <summary>
        /// Ad hoc AQL query that will be serialized to give type T
        /// </summary>
        /// <typeparam name="T">Entity class</typeparam>
        /// <param name="query">AQL query text</param>
        /// <param name="parameters">Dictionary of parmeters, use JSON names (_key instead of Key)</param>
        /// <returns>List of entities</returns>
        Task<List<T>> Query<T>(string query, Dictionary<string, object> parameters);

        JsonSerializerSettings GetJsonSettings();

        /// <summary>
        /// Get all keys of a given entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <returns>List of entity keys</returns>
        Task<List<string>> GetAllKeysAsync<T>();

        /// <summary>
        /// Get all keys of a given entity
        /// </summary>
        /// <param name="collection">Collection</param>
        /// <returns>List of entity keys</returns>
        Task<List<string>> GetAllKeysAsync(string collection);

        /// <summary>
        /// Get entities by example
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="parameters">Dynamic object of parmeters, use JSON names (_key instead of Key)</param>
        /// <returns>List of entities</returns>
        Task<List<T>> GetByExampleAsync<T>(dynamic parameters);

        /// <summary>
        /// Get entities by example
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="parameters">Dynamic object of parmeters, use JSON names (_key instead of Key)</param>
        /// <param name="collection">The collection to query</param>
        /// <returns>List of entities</returns>
        Task<List<T>> GetByExampleAsync<T>(dynamic parameters, string collection);

        /// <summary>
        /// Get entity by key
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="key">Key of entity</param>
        /// <returns>Single entity</returns>
        Task<T> GetByKeyAsync<T>(string key);

        /// <summary>
        /// Get entity by key
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="key">Key of entity</param>
        /// <param name="collection">The collection to query</param>
        /// <returns>Single entity</returns>
        Task<T> GetByKeyAsync<T>(string key, string collection);

        /// <summary>
        /// Get all entities of given type
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <returns>List of entities</returns>
        Task<List<T>> GetAllAsync<T>();

        /// <summary>
        /// Get all entities of given type
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="collection">The collection to query</param>
        /// <returns>List of entities</returns>
        Task<List<T>> GetAllAsync<T>(string collection);

        /// <summary>
        /// Update an entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="key">Key of entity</param>
        /// <param name="item">Entity with all or partial properties</param>
        /// <returns>UpdatedDocument with complete new Entity</returns>
        Task<UpdatedDocument<T>> UpdateAsync<T>(string key, T item);

        /// <summary>
        /// Update an entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="key">Key of entity</param>
        /// <param name="item">Entity with all or partial properties</param>
        /// <param name="collection">The collection in which the entity is stored</param>
        /// <returns>UpdatedDocument with complete new Entity</returns>
        Task<UpdatedDocument<T>> UpdateAsync<T>(string key, T item, string collection);

        /// <summary>
        /// Delete an entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="key">Entity key</param>
        /// <returns>True for success, false on error</returns>
        Task<bool> DeleteAsync<T>(string key);

        /// <summary>
        /// Delete an entity
        /// </summary>
        /// <param name="key">Entity key</param>
        /// <param name="collection">The collection to delete the entity from</param>
        /// <returns>True for success, false on error</returns>
        Task<bool> DeleteAsync(string key, string collection);

        /// <summary>
        /// Insert entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="item">Entity to insert</param>
        /// <returns>UpdatedDocument with new entity</returns>
        Task<UpdatedDocument<T>> InsertAsync<T>(T item);

        /// <summary>
        /// Insert entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="item">Entity to insert</param>
        /// <param name="collection">The collection to insert the entity into</param>
        /// <returns></returns>
        Task<UpdatedDocument<T>> InsertAsync<T>(T item, string collection);
    }
}