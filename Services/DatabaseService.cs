using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using DataBaseMarkDown.Models;

namespace DataBaseMarkDown.Services
{
    public class DatabaseService
    {
        private readonly DatabaseInfo _dbInfo;

        public DatabaseService(DatabaseInfo dbInfo)
        {
            _dbInfo = dbInfo;
        }

        // 測試數據庫連接
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                // 根據不同類型的資料庫，使用不同的方式開啟連接
                switch (_dbInfo.Type)
                {
                    case DatabaseType.SqlServer:
                        using (var connection = new SqlConnection(_dbInfo.ConnectionString))
                        {
                            await connection.OpenAsync();
                        }
                        break;
                    case DatabaseType.MariaDB:
                        using (var connection = new MySqlConnection(_dbInfo.ConnectionString))
                        {
                            await connection.OpenAsync();
                        }
                        break;
                    case DatabaseType.SQLite:
                        using (var connection = new SQLiteConnection(_dbInfo.ConnectionString))
                        {
                            connection.Open(); // SQLite 沒有 OpenAsync 方法，使用同步版本
                        }
                        break;
                    default:
                        throw new NotSupportedException("不支援的資料庫類型");
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 獲取所有表
        public async Task<List<TableInfo>> GetAllTablesAsync()
        {
            try
            {
                using IDbConnection connection = GetConnection();
                await OpenConnectionAsync(connection);  // 使用新的方法開啟連接
                List<TableInfo> tables = new List<TableInfo>();

                switch (_dbInfo.Type)
                {
                    case DatabaseType.SqlServer:
                        tables = await GetSqlServerTablesAsync(connection);
                        break;
                    case DatabaseType.MariaDB:
                        tables = await GetMariaDbTablesAsync(connection);
                        break;
                    case DatabaseType.SQLite:
                        tables = await GetSqliteTablesAsync(connection);
                        break;
                }

                // 獲取每個表的列信息
                foreach (var table in tables)
                {
                    table.Columns = await GetColumnsAsync(connection, table);
                }

                return tables;
            }
            catch (Exception ex)
            {
                throw new Exception($"獲取資料表時發生錯誤: {ex.Message}", ex);
            }
        }
        
        // 開啟數據庫連接
        private async Task OpenConnectionAsync(IDbConnection connection)
        {
            if (connection.State == ConnectionState.Closed)
            {
                // 根據不同類型的連接使用不同的方式開啟
                switch (connection)
                {
                    case SqlConnection sqlConnection:
                        await sqlConnection.OpenAsync();
                        break;
                    case MySqlConnection mySqlConnection:
                        await mySqlConnection.OpenAsync();
                        break;
                    default:
                        connection.Open(); // 對於其他類型，使用同步方法
                        break;
                }
            }
        }

        // 獲取SQL Server表
        private async Task<List<TableInfo>> GetSqlServerTablesAsync(IDbConnection connection)
        {
            const string sql = @"
                SELECT 
                    t.TABLE_SCHEMA AS SchemaName, 
                    t.TABLE_NAME AS Name
                FROM 
                    INFORMATION_SCHEMA.TABLES t
                WHERE 
                    t.TABLE_TYPE = 'BASE TABLE'
                ORDER BY 
                    t.TABLE_SCHEMA, t.TABLE_NAME";

            var tables = await connection.QueryAsync<dynamic>(sql);
            var result = new List<TableInfo>();
            
            foreach (var table in tables)
            {
                result.Add(new TableInfo
                {
                    Schema = table.SchemaName,
                    Name = table.Name
                });
            }
            
            return result;
        }

        // 獲取MariaDB表
        private async Task<List<TableInfo>> GetMariaDbTablesAsync(IDbConnection connection)
        {
            string sql = @"
                SELECT 
                    TABLE_SCHEMA AS SchemaName, 
                    TABLE_NAME AS Name
                FROM 
                    INFORMATION_SCHEMA.TABLES
                WHERE 
                    TABLE_SCHEMA = @Database
                    AND TABLE_TYPE = 'BASE TABLE'
                ORDER BY 
                    TABLE_NAME";

            var tables = await connection.QueryAsync<dynamic>(sql, new { Database = _dbInfo.Database });
            var result = new List<TableInfo>();
            
            foreach (var table in tables)
            {
                result.Add(new TableInfo
                {
                    Schema = table.SchemaName,
                    Name = table.Name
                });
            }
            
            return result;
        }

        // 獲取SQLite表
        private async Task<List<TableInfo>> GetSqliteTablesAsync(IDbConnection connection)
        {
            const string sql = @"
                SELECT 
                    '' AS SchemaName,
                    name AS Name
                FROM 
                    sqlite_master
                WHERE 
                    type = 'table' AND
                    name NOT LIKE 'sqlite_%'
                ORDER BY 
                    name";

            var tables = await connection.QueryAsync<dynamic>(sql);
            var result = new List<TableInfo>();
            
            foreach (var table in tables)
            {
                result.Add(new TableInfo
                {
                    Schema = table.SchemaName,
                    Name = table.Name
                });
            }
            
            return result;
        }

        // 獲取列信息
        private async Task<List<ColumnInfo>> GetColumnsAsync(IDbConnection connection, TableInfo table)
        {
            switch (_dbInfo.Type)
            {
                case DatabaseType.SqlServer:
                    return await GetSqlServerColumnsAsync(connection, table);
                case DatabaseType.MariaDB:
                    return await GetMariaDbColumnsAsync(connection, table);
                case DatabaseType.SQLite:
                    return await GetSqliteColumnsAsync(connection, table);
                default:
                    return new List<ColumnInfo>();
            }
        }

        // 獲取SQL Server列
        private async Task<List<ColumnInfo>> GetSqlServerColumnsAsync(IDbConnection connection, TableInfo table)
        {
            string sql = @"
                SELECT 
                    c.COLUMN_NAME AS Name,
                    c.DATA_TYPE AS DataType,
                    CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IsNullable,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,
                    CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsForeignKey,
                    ISNULL(fk.REFERENCED_TABLE_NAME, '') AS ForeignKeyTable,
                    ISNULL(fk.REFERENCED_COLUMN_NAME, '') AS ForeignKeyColumn
                FROM 
                    INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN (
                    SELECT 
                        ku.TABLE_CATALOG,
                        ku.TABLE_SCHEMA,
                        ku.TABLE_NAME,
                        ku.COLUMN_NAME
                    FROM 
                        INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    JOIN 
                        INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                        ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    WHERE 
                        tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ) pk ON c.TABLE_CATALOG = pk.TABLE_CATALOG 
                   AND c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
                   AND c.TABLE_NAME = pk.TABLE_NAME 
                   AND c.COLUMN_NAME = pk.COLUMN_NAME
                LEFT JOIN (
                    SELECT 
                        cu.TABLE_CATALOG,
                        cu.TABLE_SCHEMA,
                        cu.TABLE_NAME,
                        cu.COLUMN_NAME,
                        kcu.TABLE_SCHEMA AS REFERENCED_TABLE_SCHEMA,
                        kcu.TABLE_NAME AS REFERENCED_TABLE_NAME,
                        kcu.COLUMN_NAME AS REFERENCED_COLUMN_NAME
                    FROM 
                        INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                    JOIN 
                        INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE cu
                        ON rc.CONSTRAINT_NAME = cu.CONSTRAINT_NAME
                    JOIN 
                        INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                        ON rc.UNIQUE_CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                ) fk ON c.TABLE_CATALOG = fk.TABLE_CATALOG 
                   AND c.TABLE_SCHEMA = fk.TABLE_SCHEMA 
                   AND c.TABLE_NAME = fk.TABLE_NAME 
                   AND c.COLUMN_NAME = fk.COLUMN_NAME
                WHERE 
                    c.TABLE_SCHEMA = @SchemaName
                    AND c.TABLE_NAME = @TableName
                ORDER BY 
                    c.ORDINAL_POSITION";

            return (await connection.QueryAsync<ColumnInfo>(sql, new { SchemaName = table.Schema, TableName = table.Name })).AsList();
        }

        // 獲取MariaDB列
        private async Task<List<ColumnInfo>> GetMariaDbColumnsAsync(IDbConnection connection, TableInfo table)
        {
            string sql = @"
                SELECT 
                    c.COLUMN_NAME AS Name,
                    c.DATA_TYPE AS DataType,
                    CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IsNullable,
                    CASE WHEN c.COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END AS IsPrimaryKey,
                    CASE WHEN kcu.REFERENCED_TABLE_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsForeignKey,
                    IFNULL(kcu.REFERENCED_TABLE_NAME, '') AS ForeignKeyTable,
                    IFNULL(kcu.REFERENCED_COLUMN_NAME, '') AS ForeignKeyColumn
                FROM 
                    INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN 
                    INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                    ON c.TABLE_SCHEMA = kcu.TABLE_SCHEMA
                    AND c.TABLE_NAME = kcu.TABLE_NAME
                    AND c.COLUMN_NAME = kcu.COLUMN_NAME
                    AND kcu.REFERENCED_TABLE_NAME IS NOT NULL
                WHERE 
                    c.TABLE_SCHEMA = @Database
                    AND c.TABLE_NAME = @TableName
                ORDER BY 
                    c.ORDINAL_POSITION";

            return (await connection.QueryAsync<ColumnInfo>(sql, new { 
                Database = _dbInfo.Database, 
                TableName = table.Name 
            })).AsList();
        }

        // 獲取SQLite列
        private async Task<List<ColumnInfo>> GetSqliteColumnsAsync(IDbConnection connection, TableInfo table)
        {
            string sql = $"PRAGMA table_info('{table.Name}')";
            
            var columnInfos = await connection.QueryAsync<dynamic>(sql);
            var results = new List<ColumnInfo>();
            
            foreach (var column in columnInfos)
            {
                results.Add(new ColumnInfo
                {
                    Name = column.name,
                    DataType = column.type,
                    IsNullable = column.notnull == 0,
                    IsPrimaryKey = column.pk == 1,
                    // SQLite的外键需要另外查询
                    IsForeignKey = false
                });
            }
            
            // 获取外键信息
            sql = $"PRAGMA foreign_key_list('{table.Name}')";
            var foreignKeys = await connection.QueryAsync<dynamic>(sql);
            
            foreach (var fk in foreignKeys)
            {
                var column = results.Find(c => c.Name == fk.from);
                if (column != null)
                {
                    column.IsForeignKey = true;
                    column.ForeignKeyTable = fk.table;
                    column.ForeignKeyColumn = fk.to;
                }
            }
            
            return results;
        }

        // 獲取相應類型的數據庫連接
        private IDbConnection GetConnection()
        {
            return _dbInfo.Type switch
            {
                DatabaseType.SqlServer => new SqlConnection(_dbInfo.ConnectionString),
                DatabaseType.MariaDB => new MySqlConnection(_dbInfo.ConnectionString),
                DatabaseType.SQLite => new SQLiteConnection(_dbInfo.ConnectionString),
                _ => throw new NotSupportedException("不支援的資料庫類型")
            };
        }
    }
} 