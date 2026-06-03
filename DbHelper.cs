using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CanvasDrawing
{
    public class ActivityRecord
    {
        public int Id { get; set; }
        public string Time { get; set; } = "";
        public double Duration { get; set; }
    }

    public class DatabaseService : IDisposable
    {
        private readonly string _connectionString =
            "Host=localhost;Port=5432;Database=canvas_drawing;Username=postgres;Password=0000";

        public DatabaseService() => InitializeDatabase();

        private void InitializeDatabase()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS activity_log (
                    id SERIAL PRIMARY KEY,
                    activity_time TIMESTAMP NOT NULL,
                    duration_seconds DOUBLE PRECISION NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )";
            cmd.ExecuteNonQuery();
        }

        public async Task SaveActivityAsync(double durationSeconds)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO activity_log (activity_time, duration_seconds) VALUES (@t, @d)";
            cmd.Parameters.AddWithValue("@t", DateTime.Now);
            cmd.Parameters.AddWithValue("@d", Math.Round(durationSeconds, 2));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<ActivityRecord>> GetActivitiesAsync()
        {
            var list = new List<ActivityRecord>();
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, activity_time, duration_seconds FROM activity_log ORDER BY id DESC LIMIT 50";

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ActivityRecord
                {
                    Id = reader.GetInt32(0),
                    Time = reader.GetDateTime(1).ToString("HH:mm:ss dd.MM.yyyy"),
                    Duration = reader.GetDouble(2)
                });
            }
            return list;
        }

        // Получение сводной статистики
        public async Task<(int Count, double TotalTime)> GetSummaryAsync()
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(duration_seconds), 0) FROM activity_log";

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0), Math.Round(reader.GetDouble(1), 2));
            }
            return (0, 0);
        }

        // Полная очистка таблицы
        public async Task ClearAllActivitiesAsync()
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM activity_log";
            await cmd.ExecuteNonQueryAsync();
        }

        public void Dispose() { }
    }
}