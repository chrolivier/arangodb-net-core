﻿using BorderEast.ArangoDB.Client.Connection;
using BorderEast.ArangoDB.Client.Database.Meta;
using BorderEast.ArangoDB.Client.Models;
using BorderEast.ArangoDB.Client.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;
using BorderEast.ArangoDB.Client.Database.AQLCursor;
using BorderEast.ArangoDB.Client.Models.Collection;

namespace BorderEast.ArangoDB.Client.Database
{
    /// <summary>
    /// Public methods for interacting with ArangoDB
    /// </summary>
    public class ArangoDatabase : IArangoDatabase
    {
        public ClientSettings DatabaseSettings { get; }
        private readonly ConnectionPool<IConnection> connectionPool;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="databaseSettings"></param>
        /// <param name="connectionPool"></param>
        public ArangoDatabase(ClientSettings databaseSettings, ConnectionPool<IConnection> connectionPool) {
            DatabaseSettings = databaseSettings;
            this.connectionPool = connectionPool;
        }

        #region collections

        /// <summary>
        /// Create a collection based on the passed in dynamic object 
        /// Example: CreateCollection(new { Name = somename}) 
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public async Task<CollectionResult> CreateCollection(dynamic parameters) {

            var ctype = typeof(ArangoCollection);
            // get settable public properties of the type
            var props = ctype.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.GetSetMethod() != null);

            // create an instance of the type
            var collection = Activator.CreateInstance(ctype);

            // set property values using reflection
            var values = DynamicUtil.DynamicToDict(parameters);
            foreach (var prop in props) {
                if (values.ContainsKey(prop.Name)) {
                    prop.SetValue(collection, values[prop.Name]);
                }
                
            }

            return await CreateCollection(collection as ArangoCollection);
        }

        /// <summary>
        /// Create a collection based on the given ArangoCollection
        /// </summary>
        /// <param name="collection"></param>
        /// <returns></returns>
        public async Task<CollectionResult> CreateCollection(ArangoCollection collection) {

            Payload payload = new Payload()
            {
                Content = JsonConvert.SerializeObject(collection, DatabaseSettings.JsonSettings),
                Method = HttpMethod.Post,
                Path = "_api/collection"
            };

            var result = await GetResultAsync(payload);

            if (result == null) {
                return default(CollectionResult);
            }

            var json = JsonConvert.DeserializeObject<CollectionResult>(result.Content);
            return json;

        }

        #endregion

        #region query

        /// <summary>
        /// Ad hoc AQL query that will be serialized to give type T
        /// </summary>
        /// <typeparam name="T">Entity class</typeparam>
        /// <param name="query">AQL query text</param>
        /// <returns>ArangoQuery of T</returns>
        public async Task<List<T>> Query<T>(string query) {
            return await Query<T>(query, null);
        }

        /// <summary>
        /// Ad hoc AQL query that will be serialized to give type T
        /// </summary>
        /// <typeparam name="T">Entity class</typeparam>
        /// <param name="query">AQL query text</param>
        /// <param name="parameters">Dynamic object of parmeters, use JSON names (_key instead of Key)</param>
        /// <returns>ArangoQuery of T</returns>
        public async Task<List<T>> Query<T>(string query, dynamic parameters) {
            Dictionary<string, object> dParams = DynamicUtil.DynamicToDict(parameters);
            return await Query<T>(query, dParams);
        }

        /// <summary>
        /// Ad hoc AQL query that will be serialized to give type T
        /// </summary>
        /// <typeparam name="T">Entity class</typeparam>
        /// <param name="query">AQL query text</param>
        /// <param name="parameters">Dictionary of parmeters, use JSON names (_key instead of Key)</param>
        /// <returns>List of entities</returns>
        public async Task<List<T>> Query<T>(string query, Dictionary<string, object> parameters) {
            var arangoQuery = new ArangoQuery<T>(query, parameters, DatabaseSettings.JsonSettings, DatabaseSettings.IsDebug);
            return await ExecuteQuery(arangoQuery);
        }

        private async Task<List<T>> Query<T>(AQLQuery query) {
            var arangoQuery = new ArangoQuery<T>(query, DatabaseSettings.JsonSettings, DatabaseSettings.IsDebug);
            return await ExecuteQuery(arangoQuery);
        }

        #endregion


        #region get

        public JsonSerializerSettings GetJsonSettings() {
            return DatabaseSettings.JsonSettings;
        }

        /// <summary>
        /// Get all keys of a given entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <returns>List of entity keys</returns>
        public async Task<List<string>> GetAllKeysAsync<T>()
        {
            return await GetAllKeysAsync(DynamicUtil.GetTypeName(typeof(T)));
        }

        /// <summary>
        /// Get all keys of a given entity
        /// </summary>
        /// <param name="collection">Collection</param>
        /// <returns>List of entity keys</returns>
        public async Task<List<string>> GetAllKeysAsync(string collection)
        {
            if (string.IsNullOrWhiteSpace(collection))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(collection));
            return await Query<string>("for x in @@col return x._key",
                new Dictionary<string, object> { { "@col", collection } });
        }

        /// <summary>
        /// Get entities by example
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="parameters">Dynamic object of parmeters, use JSON names (_key instead of Key)</param>
        /// <returns>List of entities</returns>
        public async Task<List<T>> GetByExampleAsync<T>(dynamic parameters)
        {
            return await GetByExampleAsync<T>(parameters, DynamicUtil.GetTypeName(typeof(T)));
        }

        /// <summary>
        /// Get entities by example
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="parameters">Dynamic object of parmeters, use JSON names (_key instead of Key)</param>
        /// <param name="collection">The collection to query</param>
        /// <returns>List of entities</returns>
        public async Task<List<T>> GetByExampleAsync<T>(dynamic parameters, string collection)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (string.IsNullOrWhiteSpace(collection))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(collection));

            Type type = typeof(T);
            ForeignKey fk = HasForeignKey(type);

            var q = BuildFKQuery(fk, collection, parameters);
            return await Query<T>(q).ToListAsync();
        }



        /// <summary>
        /// Get entity by key
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="key">Key of entity</param>
        /// <returns>Single entity</returns>
        public async Task<T> GetByKeyAsync<T>(string key) {
            Type type = typeof(T);
            var typeName = DynamicUtil.GetTypeName(type);

            return await GetByKeyAsync<T>(key, typeName);
        }

        /// <summary>
        /// Get entity by key
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="key">Key of entity</param>
        /// <param name="collection">The collection to query</param>
        /// <returns>Single entity</returns>
        public async Task<T> GetByKeyAsync<T>(string key, string collection) {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("message", nameof(key));
            if (string.IsNullOrWhiteSpace(collection))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(collection));

            Type type = typeof(T);
            var typeName = DynamicUtil.GetTypeName(type);

            ForeignKey fk = HasForeignKey(type);

            if (fk.IsForeignKey) {
                var q = BuildFKQuery(fk, collection, new { _key = key });

                var r = await Query<T>(q);
                return r.FirstOrDefault();
            }

            Payload payload = new Payload() {
                Content = string.Empty,
                Method = HttpMethod.Get,
                Path = string.Format("_api/document/{0}/{1}", typeName, key)
            };
            
            var result = await GetResultAsync(payload);

            if(result == null) {
                return default(T);
            }

            var json = JsonConvert.DeserializeObject<T>(result.Content);
            return json;
        }


        /// <summary>
        /// Get all entities of given type
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <returns>List of entities</returns>
        public async Task<List<T>> GetAllAsync<T>() {
            var typeName = DynamicUtil.GetTypeName(typeof(T));
            return await GetAllAsync<T>(typeName);
        }

        /// <summary>
        /// Get all entities of given type
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="collection">The collection to query</param>
        /// <returns>List of entities</returns>
        public async Task<List<T>> GetAllAsync<T>(string collection) {
            if (string.IsNullOrWhiteSpace(collection))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(collection));

            ForeignKey fk = HasForeignKey(typeof(T));

            if (fk.IsForeignKey) {
                var q = BuildFKQuery(fk, collection);

                return await Query<T>(q);
            }

            return await Query<T>(string.Format("FOR x IN {0} RETURN x", collection));
        }

        #endregion

        #region utility

        private AQLQuery BuildFKQuery(ForeignKey fk, string collection, dynamic parameters = null) {
            if (fk == null) throw new ArgumentNullException(nameof(fk));
            if (string.IsNullOrWhiteSpace(collection))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(collection));

            var q = new AQLQuery();
            var parms = new Dictionary<string, object>();
            var sb = new StringBuilder();
            Dictionary<string, object> dParams = null;
            if(parameters != null) {
                dParams = DynamicUtil.DynamicToDict(parameters);
            }
            

            sb.Append($"FOR x1 IN {collection}");

            if (fk.IsForeignKey) {
                for (var i = 0; i < fk.ForeignKeyTypes.Count; i++) {
                    sb.AppendFormat(" LET {0} = ( FOR x IN x1.{1} FOR {0} IN {2} FILTER x == {0}._key RETURN {0}) ",
                        "a" + i, // {0}
                        fk.ForeignKeyTypes[i].Key.ToLowerInvariant(),  // {1}
                        fk.ForeignKeyTypes[i].Value.Name);  // {2}
                }
            }

            // check for parameters
            if(dParams != null && dParams.Count > 0) {
                var dp = dParams.ToArray();
                for(var i = 0; i < dp.Length; i++) {
                    sb.AppendFormat(" FILTER x1.{0} == TO_STRING(@{1}) ", dp[i].Key, "pval" + i);
                    parms.Add("pval" + i, dp[i].Value);
                }
            }

            if (fk.IsForeignKey) {
                sb.Append(" RETURN MERGE (x1, {");

                for (var i = 0; i < fk.ForeignKeyTypes.Count; i++) {
                    if (i > 0) {
                        sb.Append(", ");
                    }

                    sb.AppendFormat("{1}: {0}", // Roles: a1
                        "a" + i, // {0}
                        fk.ForeignKeyTypes[i].Key.ToLowerInvariant());  // {2}
                }

                sb.Append(" }) ");
            } else {
                sb.Append(" RETURN x1 ");
            }

            q.Query = sb.ToString();

            if(parms.Count > 0) {
                q.Parameters = parms;
            }

            return q;
        }

        private ForeignKey HasForeignKey(Type t) {
            ForeignKey fk = new ForeignKey();
            // get custom CollectionAttribute
            var attribute = t.GetTypeInfo().GetCustomAttribute<CollectionAttribute>();
            // if CollectionAttribute exists and sets HasForeignKey to true
            if (attribute != null && attribute.HasForeignKey) {
                fk.ForeignKeyTypes = new List<KeyValuePair<string, Type>>();
                // iterate over properties and extract each one that has a foreign key
                foreach (var p in t.GetProperties()) {
                    var jca = p.GetCustomAttribute<JsonConverterAttribute>();
                    if (jca != null) {
                        if (jca.ConverterType == typeof(ForeignKeyConverter)) {
                            var ta = p.PropertyType.GenericTypeArguments.FirstOrDefault();
                            fk.ForeignKeyTypes.Add(new KeyValuePair<string,Type>(p.Name, ta));
                        }
                    }
                }
                fk.IsForeignKey = true;
            } else {
                fk.IsForeignKey = false;
            }

            return fk;
        }

        #endregion


        #region C*UD
        /// <summary>
        /// Update an entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="key">Key of entity</param>
        /// <param name="item">Entity with all or partial properties</param>
        /// <returns>UpdatedDocument with complete new Entity</returns>
        public async Task<UpdatedDocument<T>> UpdateAsync<T>(string key, T item) {
            var typeName = DynamicUtil.GetTypeName(typeof(T));

            return await UpdateAsync(key, item, typeName);
        }

        /// <summary>
        /// Update an entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="key">Key of entity</param>
        /// <param name="item">Entity with all or partial properties</param>
        /// <param name="collection">The collection in which the entity is stored</param>
        /// <returns>UpdatedDocument with complete new Entity</returns>
        public async Task<UpdatedDocument<T>> UpdateAsync<T>(string key, T item, string collection) {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(key));
            if (string.IsNullOrWhiteSpace(collection))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(collection));

            HttpMethod method = new HttpMethod("PATCH");

            Payload payload = new Payload()
            {
                Content = JsonConvert.SerializeObject(item, DatabaseSettings.JsonSettings),
                Method = method,
                Path = string.Format("_api/document/{0}/{1}?mergeObjects=false&returnNew=true", collection, key)
            };

            var result = await GetResultAsync(payload);
            
            var json = JsonConvert.DeserializeObject<UpdatedDocument<T>>(result.Content, DatabaseSettings.JsonSettings);
            return json;
        }

        /// <summary>
        /// Delete an entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="key">Entity key</param>
        /// <returns>True for success, false on error</returns>
        public async Task<bool> DeleteAsync<T>(string key) {
            var typeName = DynamicUtil.GetTypeName(typeof(T));

            return await DeleteAsync(key, typeName);
        }

        /// <summary>
        /// Delete an entity
        /// </summary>
        /// <param name="key">Entity key</param>
        /// <param name="collection">The collection to delete the entity from</param>
        /// <returns>True for success, false on error</returns>
        public async Task<bool> DeleteAsync(string key, string collection) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (string.IsNullOrWhiteSpace(collection))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(collection));

            Payload payload = new Payload()
            {
                Content = string.Empty,
                Method = HttpMethod.Delete,
                Path = string.Format("_api/document/{0}/{1}?silent=true", collection, key)
            };

            var result = await GetResultAsync(payload);

            return result.StatusCode == System.Net.HttpStatusCode.OK || 
                   result.StatusCode == System.Net.HttpStatusCode.Accepted;
        }

        /// <summary>
        /// Insert entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="item">Entity to insert</param>
        /// <returns>UpdatedDocument with new entity</returns>
        public async Task<UpdatedDocument<T>> InsertAsync<T>(T item)
        {
            var typeName = DynamicUtil.GetTypeName(typeof(T));
            return await InsertAsync(item, typeName);
        }

        /// <summary>
        /// Insert entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="item">Entity to insert</param>
        /// <param name="collection">The collection to insert the entity into</param>
        /// <returns></returns>
        public async Task<UpdatedDocument<T>> InsertAsync<T>(T item, string collection)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (string.IsNullOrWhiteSpace(collection))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(collection));

            try {
                Payload payload = new Payload()
                {
                    Content = JsonConvert.SerializeObject(item, DatabaseSettings.JsonSettings),
                    Method = HttpMethod.Post,
                    Path = string.Format("_api/document/{0}/?returnNew=true", collection)
                };

                var result = await GetResultAsync(payload);

                var json = JsonConvert.DeserializeObject<UpdatedDocument<T>>(result.Content, DatabaseSettings.JsonSettings);
                return json;
            }catch(System.Exception e) {
                System.Diagnostics.Debug.WriteLine(e.StackTrace);
                return null;
            }
        }


        #endregion

        #region internal

        private async Task<List<T>> ExecuteQuery<T>(ArangoQuery<T> query)
        {
            var result = await GetResultAsync(query.Payload);

            if (result == null) {
                return null;
            }

            var json = JsonConvert.DeserializeObject<AQLResult<T>>(result.Content);
            return json.Result;
        }

        private async Task<Result> GetResultAsync(Payload payload) {

            // Get connection just before we use it
            IConnection connection = connectionPool.GetConnection();

            Result result = await connection.GetAsync(payload);

            // Put connection back immediatly after use
            connectionPool.PutConnection(connection);
            return result;
        }

        #endregion

    }
}
