using System;
using Microsoft.Data.SqlClient;

var cs = "Server=(localdb)\\mssqllocaldb;Database=DocIntelligenceDb;Trusted_Connection=True;MultipleActiveResultSets=true";
using var conn = new SqlConnection(cs);
conn.Open();
using var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT Id, OriginalFileName, Status FROM Documents";
using var reader = cmd.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine($"ID: {reader["Id"]}, Name: {reader["OriginalFileName"]}");
}
