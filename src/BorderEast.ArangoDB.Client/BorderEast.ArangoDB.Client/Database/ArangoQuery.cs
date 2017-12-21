using BorderEast.ArangoDB.Client.Connection;
using BorderEast.ArangoDB.Client.Database.AQLCursor;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace BorderEast.ArangoDB.Client.Database
{
    /// <summary>
    /// Strongy typed query builder
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ArangoQuery<T>
    {
        private AQLQuery query;
        private readonly JsonSerializerSettings jsonSettings;
        private readonly bool isDebug;
        public Payload Payload { get; private set; }

        internal ArangoQuery(string queryStr, JsonSerializerSettings jsonSettings, bool isDebug) {
            this.jsonSettings = jsonSettings;
            this.isDebug = isDebug;
            query = new AQLQuery()
            {
                Query = queryStr
            };
            CreatePayload();
        }

        internal ArangoQuery(string queryStr, Dictionary<string, object> parameters, JsonSerializerSettings jsonSettings, bool isDebug) 
        {
            this.jsonSettings = jsonSettings;
            this.isDebug = isDebug;
            query = new AQLQuery()
            {
                Query = queryStr,
                Parameters = parameters
            };
            CreatePayload();
        }

        internal ArangoQuery(AQLQuery aqlQuery, JsonSerializerSettings jsonSettings, bool isDebug) {
            this.jsonSettings = jsonSettings;
            this.isDebug = isDebug;
            query = aqlQuery;
            CreatePayload();
        }

        /*
        /// <summary>
        /// Add parameters to the query
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public ArangoQuery<T> WithParameters(Dictionary<string, object> parameters) {
            this.query.Parameters = parameters;
            return this;
        }
        */

        private void CreatePayload()
        {
            Payload = new Payload()
            {
                Content = JsonConvert.SerializeObject(query, jsonSettings),
                Method = HttpMethod.Post,
                Path = "_api/cursor"
            };
            
            if (isDebug) {
                Payload.Path += "?query=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(query.Query));
            }
        }

    }  
}
