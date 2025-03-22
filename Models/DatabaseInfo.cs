using System;
using System.Collections.Generic;

namespace DataBaseMarkDown.Models
{
    /// <summary>
    /// 資料庫類型列舉
    /// </summary>
    public enum DatabaseType
    {
        SqlServer,
        MariaDB,
        SQLite
    }

    /// <summary>
    /// 資料庫連接資訊
    /// </summary>
    public class DatabaseInfo
    {
        public DatabaseType Type { get; set; }
        public string Server { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty; // 針對SQLite

        /// <summary>
        /// 根據資料庫類型建立連接字串
        /// </summary>
        public string BuildConnectionString()
        {
            switch (Type)
            {
                case DatabaseType.SqlServer:
                    return $"Server={Server};Database={Database};User Id={Username};Password={Password};TrustServerCertificate=True;";
                case DatabaseType.MariaDB:
                    return $"Server={Server};Database={Database};Uid={Username};Pwd={Password};";
                case DatabaseType.SQLite:
                    return $"Data Source={FilePath};Version=3;";
                default:
                    throw new NotSupportedException("不支援的資料庫類型");
            }
        }
    }

    /// <summary>
    /// 表格資訊
    /// </summary>
    public class TableInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public List<ColumnInfo> Columns { get; set; } = new List<ColumnInfo>();
        public bool IsSelected { get; set; } = false;
    }

    /// <summary>
    /// 欄位資訊
    /// </summary>
    public class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
        public string ForeignKeyTable { get; set; } = string.Empty;
        public string ForeignKeyColumn { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = false;
    }
} 