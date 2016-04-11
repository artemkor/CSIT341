﻿using DatabaseBackup.ContractsDAL;
using DatabaseBackup.Entities;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;

namespace DatabaseBackup.DAL
{
    public class DBDao : IDao
    {
        public void Backup(string conString, string databaseName)
        {
            Database database;
            using (var connection = new SqlConnection(conString))
            {
                connection.Open();

                database = this.GetDatabase(connection, databaseName);

                database.Tables = this.GetTables(connection, database);

                database.Procedures = this.GetStoredProcedures(connection, database);

                database.Synonyms = this.GetSynonyms(connection, database);

                database.Views = this.GetViews(connection, database);

                database.Functions = this.GetFunctions(connection, database);

                database.Sequences = this.GetSequences(connection, database);
            }

            this.CreateBackupFile(database);
        }

        public void Restore(DateTime date)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> ShowDatabases(string conString)
        {
            var databases = new List<string>();
            var sqlCommandStr = "SELECT name FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')";
            using (var connection = new SqlConnection(conString))
            {
                connection.Open();
                using (var command = new SqlCommand(sqlCommandStr, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        databases.Add(reader.GetString(0));
                    }
                }
            }

            return databases;
        }

        private void CreateBackupFile(Database database)
        {
            var curDate = DateTime.Now;
            using (var sqlFile = new StreamWriter($"backup_{curDate: dd-MM-yyyy_HH-mm}.sql"))
            {
                sqlFile.WriteLine(database);
                sqlFile.WriteLine();
                sqlFile.WriteLine($"USE {database.Name}");
                sqlFile.WriteLine("GO");
                sqlFile.WriteLine();

                this.WriteTablesCreation(database.Tables, sqlFile);
                sqlFile.WriteLine();

                this.WriteViews(database.Views, sqlFile);
                sqlFile.WriteLine();

                this.WriteSynonyms(database.Synonyms, sqlFile);
                sqlFile.WriteLine();

                this.WriteProcedures(database.Procedures, sqlFile);
                sqlFile.WriteLine();

                this.WriteFunctions(database.Functions, sqlFile);
                sqlFile.WriteLine();

                this.WriteSequences(database.Sequences, sqlFile);
                sqlFile.WriteLine();

                this.WriteTableData(database.Tables, sqlFile);
                sqlFile.WriteLine();

                this.WriteConstraints(database.Tables, sqlFile);
                sqlFile.WriteLine();

                this.WriteTriggers(database.Tables, sqlFile);
                sqlFile.WriteLine();
            }
        }

        private IEnumerable<Column> GetColumns(SqlConnection connection, Table table)
        {
            string sqlCommandStr = @"SELECT COLUMN_NAME, COLUMN_DEFAULT, IS_NULLABLE, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, COLLATION_NAME
                                            FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @tableSchema AND TABLE_NAME = @tableName";
            var columns = new List<Column>();

            using (SqlCommand command = new SqlCommand(sqlCommandStr, connection))
            {
                command.Parameters.AddWithValue("@tableSchema", table.Schema);
                command.Parameters.AddWithValue("@tableName", table.Name);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add(new Column
                        {
                            Name = reader.GetString(0),
                            Default = (reader.IsDBNull(1)) ? null : reader.GetString(1),
                            IsNullable = reader.GetString(2) == "YES" ? true : false,
                            DataType = reader.GetString(3),
                            CharactersMaxLength = (reader.IsDBNull(4)) ? -1 : reader.GetInt32(4),
                            CollationName = (reader.IsDBNull(5)) ? null : reader.GetString(5),
                        });
                    }
                }
            }

            return columns;
        }

        private IEnumerable<Data> GetData(SqlConnection connection, Table table)
        {
            var sqlCommandStr = $"SELECT {string.Join(", ", table.Columns.Select(x => x.Name))} FROM {table.Name}";
            var data = new List<Data>();
            using (SqlCommand command = new SqlCommand(sqlCommandStr, connection))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var tempData = new Data();
                        tempData.NameValue = new Dictionary<string, string>();
                        tempData.TableName = table.Name;
                        tempData.TableSchema = table.Schema;
                        int counter = 0;
                        foreach (var column in table.Columns)
                        {
                            if (reader[counter] is DBNull)
                            {
                                tempData.NameValue.Add(column.Name, "NULL");
                                continue;
                            }

                            switch (column.DataType)
                            {
                                case "binary":
                                case "varbinary":
                                case "image":
                                case "rowversion":
                                case "timestamp":
                                    tempData.NameValue.Add(column.Name, $"'0x{BitConverter.ToString((byte[])reader[counter]).Replace("-", string.Empty)}'");
                                    break;

                                case "bigint":
                                case "bit":
                                case "decimal":
                                case "float":
                                case "int":
                                case "money":
                                case "numeric":
                                case "real":
                                case "smallint":
                                case "smallmoney":
                                case "tinyint":
                                    tempData.NameValue.Add(column.Name, reader[counter].ToString());
                                    break;

                                case "nchar":
                                case "ntext":
                                case "nvarchar":
                                    tempData.NameValue.Add(column.Name, $"'n{reader[counter].ToString()}'");
                                    break;

                                case "char":
                                case "text":
                                case "varchar":
                                    tempData.NameValue.Add(column.Name, $"'{reader[counter].ToString()}'");
                                    break;

                                case "date":
                                    tempData.NameValue.Add(column.Name, $"'{reader.GetDateTime(counter).ToShortDateString()}'");
                                    break;

                                case "datetime":
                                    tempData.NameValue.Add(column.Name, $"'{reader.GetDateTime(counter).ToString("dd-MM-YYYY HH:mm:ss.fffffff")}'");
                                    break;

                                case "datetime2":
                                    tempData.NameValue.Add(column.Name, $"'{reader.GetDateTime(counter).ToString("dd-MM-YYYY HH:mm:ss.fffffff")}'");
                                    break;

                                case "datetimeoffset":
                                    tempData.NameValue.Add(column.Name, $"'{reader.GetDateTime(counter).ToString("dd-MM-YYYY HH:mm:ss.fffffff zzz")}'");
                                    break;

                                case "time":
                                    tempData.NameValue.Add(column.Name, $"'{reader.GetTimeSpan(counter)}'");
                                    break;

                                case "uniqueidentifier":
                                    tempData.NameValue.Add(column.Name, $"{reader.GetGuid(counter).ToString()}'");
                                    break;

                                default:
                                    break;
                            }
                            counter++;
                        }

                        data.Add(tempData);
                    }
                }
            }

            return data;
        }

        private Database GetDatabase(SqlConnection connection, string databaseName)
        {
            var sqlStrCommand = @"SELECT
                                name,
                                compatibility_level,
                                collation_name,
                                user_access_desc,
                                is_read_only,
                                is_auto_close_on,
                                is_auto_shrink_on,
                                is_read_committed_snapshot_on,
                                recovery_model_desc,
                                page_verify_option_desc,
                                is_auto_create_stats_on,
                                is_auto_update_stats_on,
                                is_ansi_null_default_on,
                                is_ansi_nulls_on,
                                is_ansi_padding_on,
                                is_ansi_warnings_on,
                                is_arithabort_on,
                                is_concat_null_yields_null_on,
                                is_quoted_identifier_on,
                                is_numeric_roundabort_on,
                                is_recursive_triggers_on,
                                is_cursor_close_on_commit_on,
                                is_date_correlation_on,
                                is_db_chaining_on,
                                is_trustworthy_on,
                                is_parameterization_forced,
                                is_broker_enabled
                                FROM sys.databases WHERE name = @dbName";
            using (SqlCommand command = new SqlCommand(sqlStrCommand, connection))
            {
                command.Parameters.AddWithValue("@dbName", databaseName);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var Name = reader.GetString(0);
                        var CompatibilityLevel = reader.GetByte(1);
                        var CollationName = reader.GetString(2);
                        var UserAccessDescription = reader.GetString(3);
                        var IsReadOnly = reader.GetBoolean(4);
                        var IsAutoCloseOn = reader.GetBoolean(5);
                        var IsAutoShrinkOn = reader.GetBoolean(6);
                        var IsReadCommittedSnapshotOn = reader.GetBoolean(7);
                        var RecoveryModelDescription = reader.GetString(8);
                        var PageVerifyOptionDescription = reader.GetString(9);
                        var IsAutoCreateStatsOn = reader.GetBoolean(10);
                        var IsAutoUpdateStatsOn = reader.GetBoolean(11);
                        var IsAnsiNullDefaultOn = reader.GetBoolean(12);
                        var IsAnsiNullsOn = reader.GetBoolean(13);
                        var IsAnsiPaddingOn = reader.GetBoolean(14);
                        var IsAnsiWarningsOn = reader.GetBoolean(15);
                        var IsArithabortOn = reader.GetBoolean(16);
                        var IsConcatNullYieldsNullOn = reader.GetBoolean(17);
                        var IsQuotedIdentifierOn = reader.GetBoolean(18);
                        var IsNumericRoundAbortOn = reader.GetBoolean(19);
                        var IsRecursiveTriggersOn = reader.GetBoolean(20);
                        var IsCursorCloseOnCommitOn = reader.GetBoolean(21);
                        var IsDateCorrelationOn = reader.GetBoolean(22);
                        var IsDbChainingOn = reader.GetBoolean(23);
                        var IsTrustworthyOn = reader.GetBoolean(24);
                        var IsParameterizationForced = reader.GetBoolean(25);
                        var IsBrokerEnabled = reader.GetBoolean(26);
                        return new Database
                        {
                            Name = reader.GetString(0),
                            CompatibilityLevel = reader.GetByte(1),
                            CollationName = reader.GetString(2),
                            UserAccessDescription = reader.GetString(3),
                            IsReadOnly = reader.GetBoolean(4),
                            IsAutoCloseOn = reader.GetBoolean(5),
                            IsAutoShrinkOn = reader.GetBoolean(6),
                            IsReadCommittedSnapshotOn = reader.GetBoolean(7),
                            RecoveryModelDescription = reader.GetString(8),
                            PageVerifyOptionDescription = reader.GetString(9),
                            IsAutoCreateStatsOn = reader.GetBoolean(10),
                            IsAutoUpdateStatsOn = reader.GetBoolean(11),
                            IsAnsiNullDefaultOn = reader.GetBoolean(12),
                            IsAnsiNullsOn = reader.GetBoolean(13),
                            IsAnsiPaddingOn = reader.GetBoolean(14),
                            IsAnsiWarningsOn = reader.GetBoolean(15),
                            IsArithabortOn = reader.GetBoolean(16),
                            IsConcatNullYieldsNullOn = reader.GetBoolean(17),
                            IsQuotedIdentifierOn = reader.GetBoolean(18),
                            IsNumericRoundAbortOn = reader.GetBoolean(19),
                            IsRecursiveTriggersOn = reader.GetBoolean(20),
                            IsCursorCloseOnCommitOn = reader.GetBoolean(21),
                            IsDateCorrelationOn = reader.GetBoolean(22),
                            IsDbChainingOn = reader.GetBoolean(23),
                            IsTrustworthyOn = reader.GetBoolean(24),
                            IsParameterizationForced = reader.GetBoolean(25),
                            IsBrokerEnabled = reader.GetBoolean(26),
                        };
                    }
                }
            }

            return null;
        }

        #region getters

        private IEnumerable<Constraint> GetForeignKeyConstraints(SqlConnection connection, Table table)
        {
            var foreignKeyConstraints = new List<ForeignKeyConstraint>();
            string sqlCommandStr = @"SELECT
	                                    FK_Schema = FK.TABLE_SCHEMA,
                                        FK_Table = FK.TABLE_NAME,
                                        FK_Column = CU.COLUMN_NAME,
                                        PK_Schema = PK.TABLE_SCHEMA,
	                                    PK_Table = PK.TABLE_NAME,
                                        PK_Column = PT.COLUMN_NAME,
                                        Constraint_Name = C.CONSTRAINT_NAME,
		                                On_Delete = C.DELETE_RULE,
		                                On_Update = C.UPDATE_RULE
                                    FROM
                                        INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS C
                                    INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS FK
                                        ON C.CONSTRAINT_NAME = FK.CONSTRAINT_NAME
                                    INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS PK
                                        ON C.UNIQUE_CONSTRAINT_NAME = PK.CONSTRAINT_NAME
                                    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE CU
                                        ON C.CONSTRAINT_NAME = CU.CONSTRAINT_NAME
                                    INNER JOIN (
                                                SELECT
                                                    i1.TABLE_NAME,
                                                    i2.COLUMN_NAME
                                                FROM
                                                    INFORMATION_SCHEMA.TABLE_CONSTRAINTS i1
                                                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE i2
                                                    ON i1.CONSTRAINT_NAME = i2.CONSTRAINT_NAME
                                                WHERE
                                                    i1.CONSTRAINT_TYPE = 'PRIMARY KEY'
                                                ) PT
                                        ON PT.TABLE_NAME = PK.TABLE_NAME
WHERE FK.TABLE_SCHEMA = @tableSchema AND FK.TABLE_NAME = @tableName";

            using (SqlCommand command = new SqlCommand(sqlCommandStr, connection))
            {
                command.Parameters.AddWithValue("@tableName", table.Name);
                command.Parameters.AddWithValue("@tableSchema", table.Schema);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var constraintName = reader["Constraint_Name"] as string;
                        var primaryTableColumnName = reader["PK_Column"] as string;
                        var foreignTableColumnName = reader["FK_Column"] as string;
                        var element = foreignKeyConstraints.FirstOrDefault(x => x.Name == constraintName);

                        if (element != null)
                        {
                            element.PrimaryTableColumns.Add(primaryTableColumnName);
                            element.Columns.Add(foreignTableColumnName);
                            continue;
                        }

                        var foreignTableName = reader["FK_Table"] as string;
                        var foreignTableSchema = reader["FK_Schema"] as string;
                        var primaryTableSchema = reader["PK_Schema"] as string;
                        var primaryTableName = reader["PK_Table"] as string;
                        var onDeleteRule = reader["On_Delete"] as string;
                        var onUpdateRule = reader["On_Update"] as string;

                        foreignKeyConstraints.Add(new ForeignKeyConstraint
                        {
                            Name = constraintName,
                            PrimaryTableColumns = new List<string> { primaryTableColumnName },
                            PrimaryTableName = primaryTableName,
                            PrimaryTableSchema = primaryTableSchema,
                            TableName = foreignTableName,
                            TableSchema = foreignTableSchema,
                            Columns = new List<string> { foreignTableColumnName },
                            OnDeleteRule = onDeleteRule,
                            OnUpdateRule = onUpdateRule,
                        });
                    }
                }
            }

            return foreignKeyConstraints;
        }

        private IEnumerable<Function> GetFunctions(SqlConnection connection, Database database)
        {
            var functions = new List<Function>();
            string sqlCommandStr = @"SELECT ROUTINE_DEFINITION FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE = 'FUNCTION' AND SPECIFIC_CATALOG = @specificCatalog";

            using (SqlCommand command = new SqlCommand(sqlCommandStr, connection))
            {
                command.Parameters.AddWithValue("@specificCatalog", database.Name);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        functions.Add(new Function
                        {
                            Definition = reader.GetString(0),
                        });
                    }
                }
            }
            return functions;
        }

        private IEnumerable<Constraint> GetPrimaryKeyConstraints(SqlConnection connection, Table table)
        {
            var primaryKeyConstraints = new List<PrimaryKeyConstraint>();
            string sqlCommandStr = @"SELECT  tc.CONSTRAINT_NAME, tc.TABLE_SCHEMA, tc.TABLE_NAME, cu.COLUMN_NAME
	                                                                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc
		                                                                        INNER JOIN(SELECT * FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE) AS cu
		                                                                        ON tc.CONSTRAINT_NAME = cu.CONSTRAINT_NAME
                                                                        WHERE CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.TABLE_NAME = @tableName AND tc.TABLE_SCHEMA = @tableSchema";

            using (SqlCommand command = new SqlCommand(sqlCommandStr, connection))
            {
                command.Parameters.AddWithValue("@tableName", table.Name);
                command.Parameters.AddWithValue("@tableSchema", table.Schema);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var constraintName = reader["CONSTRAINT_NAME"] as string;
                        var primaryTableSchema = reader["TABLE_SCHEMA"] as string;
                        var primaryTableName = reader["TABLE_NAME"] as string;
                        var primaryTableColumnName = reader["COLUMN_NAME"] as string;

                        var element = primaryKeyConstraints.FirstOrDefault(x => x.Name == constraintName);

                        if (element != null)
                        {
                            element.Columns.Add(primaryTableColumnName);
                            continue;
                        }

                        primaryKeyConstraints.Add(new PrimaryKeyConstraint
                        {
                            Name = constraintName,
                            Columns = new List<string> { primaryTableColumnName },
                            TableName = primaryTableName,
                            TableSchema = primaryTableSchema,
                        });
                    }
                }
            }

            return primaryKeyConstraints;
        }

        private IEnumerable<Sequence> GetSequences(SqlConnection connection, Database database)
        {
            string sqlCommandStr = @"SELECT infS.SEQUENCE_SCHEMA, infS.SEQUENCE_NAME, infS.DATA_TYPE, infS.START_VALUE, infS.INCREMENT, infS.MINIMUM_VALUE, infS.MAXIMUM_VALUE, ss.is_cached FROM INFORMATION_SCHEMA.SEQUENCES as infS
INNER JOIN (SELECT name, is_cached FROM sys.sequences) as ss
ON ss.name = infS.SEQUENCE_NAME WHERE infS.SEQUENCE_CATALOG = @databaseName";

            var sequences = new List<Sequence>();

            using (SqlCommand command = new SqlCommand(sqlCommandStr, connection))
            {
                command.Parameters.AddWithValue("@databaseName", database.Name);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sequences.Add(new Sequence
                        {
                            Schema = reader.GetString(0),
                            Name = reader.GetString(1),
                            DataType = reader.GetString(2),
                            StartValue = reader.GetInt64(3),
                            Increment = reader.GetInt64(4),
                            MinValue = reader.GetInt64(5),
                            MaxValue = reader.GetInt64(6),
                            IsCached = reader.GetBoolean(7),
                        });
                    }
                }
            }

            return sequences;
        }

        private IEnumerable<Procedure> GetStoredProcedures(SqlConnection connection, Database database)
        {
            var procedures = new List<Procedure>();
            string sqlCommandStr = @"SELECT ROUTINE_DEFINITION FROM INFORMATION_SCHEMA.ROUTINES
                                        WHERE ROUTINE_TYPE = 'PROCEDURE' AND SPECIFIC_CATALOG = @specificCatalog";

            using (SqlCommand command = new SqlCommand(sqlCommandStr, connection))
            {
                command.Parameters.AddWithValue("@specificCatalog", database.Name);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        procedures.Add(new Procedure
                        {
                            Definition = reader.GetString(0),
                        });
                    }
                }
            }

            return procedures;
        }

        private IEnumerable<Synonym> GetSynonyms(SqlConnection connection, Database database)
        {
            var synonyms = new List<Synonym>();
            string sqlCommandStr = "SELECT name, base_object_name FROM sys.synonyms";
            char[] trimChars = new char[] { '[', ']' };
            using (SqlCommand command = new SqlCommand(sqlCommandStr, connection))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var catalogueSchemaObject = reader.GetString(1).Replace("[", string.Empty).Replace("]", string.Empty).Split('.');

                    if (catalogueSchemaObject[0] == database.Name)
                    {
                        synonyms.Add(new Synonym
                        {
                            Name = reader.GetString(0),
                            Catalogue = catalogueSchemaObject[0],
                            ObjectName = catalogueSchemaObject[2],
                            Schema = catalogueSchemaObject[1],
                        });
                    }
                }
            }

            return synonyms;
        }

        private IEnumerable<Table> GetTables(SqlConnection connection, Database database)
        {
            var tables = new List<Table>();
            var sqlStrCommand = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = @dbName";
            using (SqlCommand command = new SqlCommand(sqlStrCommand, connection))
            {
                command.Parameters.AddWithValue("@dbName", database.Name);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tables.Add(new Table
                        {
                            Schema = reader.GetString(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }
            foreach (var table in tables)
            {
                var constraints = new List<Constraint>(this.GetForeignKeyConstraints(connection, table));
                constraints.AddRange(this.GetPrimaryKeyConstraints(connection, table));
                constraints.AddRange(this.GetUniqueConstraints(connection, table));

                table.Columns = this.GetColumns(connection, table);
                table.Data = this.GetData(connection, table);
                table.Triggers = this.GetTableTriggers(connection, table);
                table.Constraints = constraints;
            }

            return tables;
        }

        private IEnumerable<Trigger> GetTableTriggers(SqlConnection connection, Table table)
        {
            var triggers = new List<Trigger>();
            var names = new List<string>();
            StringBuilder definition = new StringBuilder();
            string sqlCommandStr = "SELECT name FROM sys.triggers AS st WHERE st.parent_id = (SELECT object_id FROM sys.tables WHERE name = @tableName AND schema_id = (SELECT schema_id FROM sys.schemas WHERE name = @tableSchema))";
            string sqlCommandStr2 = "exec sp_helptext @name";

            using (SqlCommand command = new SqlCommand(sqlCommandStr, connection))
            {
                command.Parameters.AddWithValue("@tableName", table.Name);
                command.Parameters.AddWithValue("@tableSchema", table.Schema);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        names.Add(reader.GetString(0));
                    }
                }
            }
            foreach (var name in names)
            {
                using (SqlCommand command = new SqlCommand(sqlCommandStr2, connection))
                {
                    command.Parameters.AddWithValue("@name", $"{name}");
                    using (SqlDataReader reader2 = command.ExecuteReader())
                    {
                        while (reader2.Read())
                        {
                            definition.Append(reader2.GetString(0));
                        }
                    }

                    triggers.Add(new Trigger
                    {
                        Name = name,
                        Definition = definition.ToString(),
                    });

                    definition.Clear();
                }
            }

            return triggers;
        }

        private IEnumerable<Constraint> GetUniqueConstraints(SqlConnection connection, Table table)
        {
            var uniqueConstraints = new List<UniqueConstraint>();
            string sqlCommandStr = @"SELECT tc.CONSTRAINT_SCHEMA, tc.CONSTRAINT_NAME,  tc.TABLE_SCHEMA, tc.TABLE_NAME, cu.COLUMN_NAME
	        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc
	        INNER JOIN(SELECT * FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE) AS cu
	        ON tc.CONSTRAINT_NAME = cu.CONSTRAINT_NAME
	        WHERE tc.CONSTRAINT_TYPE = 'UNIQUE' AND tc.TABLE_NAME = @tableName AND tc.TABLE_SCHEMA = @tableSchema";

            using (SqlCommand command = new SqlCommand(sqlCommandStr, connection))
            {
                command.Parameters.AddWithValue("@tableName", table.Name);
                command.Parameters.AddWithValue("@tableSchema", table.Schema);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var constraintName = reader["CONSTRAINT_NAME"] as string;
                        var primaryTableSchema = reader["TABLE_SCHEMA"] as string;
                        var primaryTableName = reader["TABLE_NAME"] as string;
                        var primaryTableColumnName = reader["COLUMN_NAME"] as string;

                        var element = uniqueConstraints.FirstOrDefault(x => x.Name == constraintName);

                        if (element != null)
                        {
                            element.Columns.Add(primaryTableColumnName);
                            continue;
                        }

                        uniqueConstraints.Add(new UniqueConstraint
                        {
                            Name = constraintName,
                            Columns = new List<string> { primaryTableColumnName },
                            TableName = primaryTableName,
                            TableSchema = primaryTableSchema,
                        });
                    }
                }
            }

            return uniqueConstraints;
        }

        private IEnumerable<View> GetViews(SqlConnection connection, Database database)
        {
            var views = new List<View>();
            string sqlCommandStr = @"SELECT TABLE_SCHEMA, TABLE_NAME, VIEW_DEFINITION FROM INFORMATION_SCHEMA.Views WHERE TABLE_CATALOG = @databaseName";

            using (SqlCommand command = new SqlCommand(sqlCommandStr, connection))
            {
                command.Parameters.AddWithValue("@databaseName", database.Name);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        views.Add(new View
                        {
                            Schema = reader.GetString(0),
                            Name = reader.GetString(1),
                            Definition = reader.GetString(2),
                        });
                    }
                }
            }

            foreach (var view in views)
            {
                view.Triggers = this.GetViewTriggers(connection, view);
            }

            return views;
        }

        private IEnumerable<Trigger> GetViewTriggers(SqlConnection connection, View view)
        {
            var triggers = new List<Trigger>();
            var names = new List<string>();
            StringBuilder definition = new StringBuilder();
            string sqlCommandStr = "SELECT name FROM sys.triggers AS st WHERE st.parent_id = (SELECT object_id FROM sys.tables WHERE name = @viewName AND schema_id = (SELECT schema_id FROM sys.schemas WHERE name = @viewSchema))";
            string sqlCommandStr2 = "exec sp_helptext @name";

            using (SqlCommand command = new SqlCommand(sqlCommandStr, connection))
            {
                command.Parameters.AddWithValue("@viewName", view.Name);
                command.Parameters.AddWithValue("@viewSchema", view.Schema);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        names.Add(reader.GetString(0));
                    }
                }
            }

            foreach (var name in names)
            {
                using (SqlCommand command = new SqlCommand(sqlCommandStr2, connection))
                {
                    command.Parameters.AddWithValue("@name", $"{name}");
                    using (SqlDataReader reader2 = command.ExecuteReader())
                    {
                        while (reader2.Read())
                        {
                            definition.Append(reader2.GetString(0));
                        }
                    }

                    triggers.Add(new Trigger
                    {
                        Name = name,
                        Definition = definition.ToString(),
                    });

                    definition.Clear();
                }
            }

            return triggers;
        }

        #endregion getters

        #region WriteMethods

        private void WriteConstraints(IEnumerable<Table> tables, StreamWriter sqlFile)
        {
            foreach (var table in tables)
            {
                foreach (var constraint in table.Constraints)
                {
                    sqlFile.WriteLine(constraint);
                    sqlFile.WriteLine("GO");
                }
            }
        }

        private void WriteFunctions(IEnumerable<Function> functions, StreamWriter sqlFile)
        {
            sqlFile.WriteLine("/* Functions */");
            foreach (var function in functions)
            {
                sqlFile.WriteLine(function);
                sqlFile.WriteLine("GO");
            }
        }

        private void WriteProcedures(IEnumerable<Procedure> procedures, StreamWriter sqlFile)
        {
            sqlFile.WriteLine("/* Stored procedures */");

            foreach (var procedure in procedures)
            {
                sqlFile.WriteLine(procedure);
                sqlFile.WriteLine("GO;");
            }
        }

        private void WriteSequences(IEnumerable<Sequence> sequences, StreamWriter sqlFile)
        {
            foreach (var sequence in sequences)
            {
                sqlFile.WriteLine(sequence);
                sqlFile.WriteLine("GO");
            }
        }

        private void WriteSynonyms(IEnumerable<Synonym> synonyms, StreamWriter sqlFile)
        {
            sqlFile.WriteLine("/* Synonyms */");
            foreach (var synonym in synonyms)
            {
                sqlFile.WriteLine(synonym);
                sqlFile.WriteLine("GO");
            }
        }

        private void WriteTableData(IEnumerable<Table> tables, StreamWriter sqlFile)
        {
            sqlFile.WriteLine("/* Data */");

            foreach (var table in tables)
            {
                foreach (var dataPiece in table.Data)
                {
                    sqlFile.WriteLine(dataPiece);
                    sqlFile.WriteLine("GO");
                }
            }
        }

        private void WriteTablesCreation(IEnumerable<Table> tables, StreamWriter sqlFile)
        {
            sqlFile.WriteLine("/* Tables */");

            foreach (var table in tables)
            {
                sqlFile.WriteLine($"CREATE TABLE [{table.Schema}].[{table.Name}] (");

                foreach (var column in table.Columns)
                {
                    string allowNull = column.IsNullable ? "NULL" : "NOT NULL";
                    string defaultValue = column.Default == null ? String.Empty : "DEFAULT" + column.Default;
                    sqlFile.WriteLine($"\t[{column.Name}] {column.DataType} {allowNull} {defaultValue},");
                }

                sqlFile.WriteLine(");");
                sqlFile.WriteLine("GO;");
            }
        }

        private void WriteTriggers(IEnumerable<Table> tables, StreamWriter sqlFile)
        {
            foreach (var table in tables)
            {
                foreach (var trigger in table.Triggers)
                {
                    sqlFile.WriteLine(trigger);
                    sqlFile.WriteLine("GO");
                    sqlFile.WriteLine();
                }
            }
        }

        private void WriteViews(IEnumerable<View> views, StreamWriter sqlFile)
        {
            foreach (var view in views)
            {
                sqlFile.WriteLine(view);
                sqlFile.WriteLine("GO");
            }
        }

        #endregion WriteMethods
    }
}