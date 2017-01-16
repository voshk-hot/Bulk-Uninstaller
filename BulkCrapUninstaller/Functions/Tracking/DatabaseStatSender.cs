﻿using System;
using MySql.Data.MySqlClient;

namespace BulkCrapUninstaller.Functions.Tracking
{
    public class DatabaseStatSender
    {
        public DatabaseStatSender(string connectionString, string commandName, long key)
        {
            ConnectionString = connectionString;
            CommandName = commandName;
            Key = key;
        }

        public string ConnectionString { get; set; }
        public string CommandName { get; set; }
        public long Key { get; set; }
        public static bool SuppressSqlExceptions { get; set; } = true;

        public bool SendData(byte[] value)
        {
            var connection = new MySqlConnection(ConnectionString);

            var command = connection.CreateCommand();
            command.CommandText = "CALL " + CommandName + "(@userParam, @dataParam)";

            if (Key == 0) Key = new Random().Next(-1000, -1);
            command.Parameters.Add(new MySqlParameter("@userParam", Key));
            command.Parameters.Add(new MySqlParameter("@dataParam", value));

            try
            {
                connection.Open();
                command.ExecuteNonQuery();
                connection.Close();
                connection.Dispose();
                return true;
            }
            catch (MySqlException)
            {
                if (!SuppressSqlExceptions)
                    throw;
            }
            return false;
        }
    }
}