﻿using BorderEast.ArangoDB.Client.Database;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace BorderEast.ArangoDB.Client.Connection
{
    public class ConnectionPool<Connection>
    {
        private ConcurrentBag<Connection> _connections;
        private Func<Connection> _objectGenerator;

        public ConnectionPool(ClientSettings databaseSettings) {
        }

        public ConnectionPool(Func<Connection> connectionsGenerator) {
            _connections = new ConcurrentBag<Connection>();
            _objectGenerator = connectionsGenerator ?? throw new ArgumentNullException(nameof(connectionsGenerator));
        }

        public Connection GetConnection() {
            if (_connections.TryTake(out Connection connection)) {
                return connection;
            }

            return _objectGenerator();
        }

        public void PutConnection(Connection connection) {
            _connections.Add(connection);
        }
    }
}
