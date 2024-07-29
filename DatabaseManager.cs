using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

public class DatabaseManager
{
    private string connectionString;
    public static Dictionary<string, string> IATAToCityMap = new Dictionary<string, string>();

    public DatabaseManager(string databasePath)
    {
        connectionString = $"Data Source={databasePath};Version=3;";
        InitializeDatabase();
        LoadCitiesFromCsv();
    }

    private void InitializeDatabase()
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            connection.Open();
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Departures (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FlightNumber TEXT,
                    Destination TEXT,
                    City TEXT,
                    Gate TEXT,
                    BoardingTime TEXT,
                    ScheduledTime TEXT,
                    ActualTime TEXT,
                    Status TEXT,
                    Terminal TEXT,
                    Airline TEXT
                )";
            using (var command = new SQLiteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

    private void LoadCitiesFromCsv()
    {
        try
        {
            var lines = File.ReadAllLines("cities.csv");
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length == 2)
                {
                    IATAToCityMap[parts[0].Trim()] = parts[1].Trim();
                }
            }

            // Debugovací výpis pro kontrolu načtení dat
            foreach (var kvp in IATAToCityMap)
            {
                Console.WriteLine($"IATA: {kvp.Key}, City: {kvp.Value}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading cities from CSV: {ex.Message}");
        }
    }

    public void InsertDeparture(string flightNumber, string destination, string city, string gate, string boardingTime, string scheduledTime, string actualTime, string status, string terminal, string airline)
    {
        // Přeložíme název města podle IATA kódu
        city = IATAToCityMap.ContainsKey(destination) ? IATAToCityMap[destination] : city;

        // Kontrola pro zpoždění
        if (!string.IsNullOrEmpty(actualTime) && DateTime.TryParse(actualTime, out DateTime actual) && DateTime.TryParse(scheduledTime, out DateTime scheduled) && actual > scheduled)
        {
            status += $" Opožděn ({(actual - scheduled).TotalMinutes} minut)";
        }

        // Kontrola pro zrušený let
        if (status != null && status.ToLower().Contains("cancelled"))
        {
            status = "Zrušen";
        }

        // Kontrola pro boarding
        if (status != null && status.ToLower().Contains("boarding"))
        {
            status = "Boarding";
        }

        using (var connection = new SQLiteConnection(connectionString))
        {
            connection.Open();
            string insertQuery = @"
                INSERT INTO Departures (FlightNumber, Destination, City, Gate, BoardingTime, ScheduledTime, ActualTime, Status, Terminal, Airline)
                VALUES (@FlightNumber, @Destination, @City, @Gate, @BoardingTime, @ScheduledTime, @ActualTime, @Status, @Terminal, @Airline)";
            using (var command = new SQLiteCommand(insertQuery, connection))
            {
                command.Parameters.AddWithValue("@FlightNumber", flightNumber);
                command.Parameters.AddWithValue("@Destination", destination);
                command.Parameters.AddWithValue("@City", city);
                command.Parameters.AddWithValue("@Gate", gate);
                command.Parameters.AddWithValue("@BoardingTime", boardingTime);
                command.Parameters.AddWithValue("@ScheduledTime", scheduledTime);
                command.Parameters.AddWithValue("@ActualTime", actualTime);
                command.Parameters.AddWithValue("@Status", status);
                command.Parameters.AddWithValue("@Terminal", terminal);
                command.Parameters.AddWithValue("@Airline", airline);
                command.ExecuteNonQuery();
            }
        }
    }

    public List<Departure> GetFilteredDepartures()
    {
        var departures = new List<Departure>();
        using (var connection = new SQLiteConnection(connectionString))
        {
            connection.Open();
            string selectQuery = "SELECT * FROM Departures";
            using (var command = new SQLiteCommand(selectQuery, connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var departure = new Departure
                        {
                            FlightNumber = reader["FlightNumber"].ToString(),
                            Destination = reader["Destination"].ToString(),
                            City = reader["City"].ToString(),
                            Gate = reader["Gate"].ToString(),
                            BoardingTime = reader["BoardingTime"].ToString(),
                            ScheduledTime = reader["ScheduledTime"].ToString(),
                            ActualTime = reader["ActualTime"].ToString(),
                            Status = reader["Status"].ToString(),
                            Terminal = reader["Terminal"].ToString(),
                            Airline = reader["Airline"].ToString()
                        };

                        DateTime now = DateTime.Now;
                        if (DateTime.TryParse(departure.ScheduledTime, out DateTime scheduledTime))
                        {
                            if (scheduledTime >= now.AddHours(-2) && scheduledTime <= now.AddHours(24))
                            {
                                departures.Add(departure);
                            }
                        }
                    }
                }
            }
        }
        return departures;
    }
}

public class Departure
{
    public string FlightNumber { get; set; }
    public string Destination { get; set; }
    public string City { get; set; }
    public string Gate { get; set; }
    public string BoardingTime { get; set; }
    public string ScheduledTime { get; set; }
    public string ActualTime { get; set; }
    public string Status { get; set; }
    public string Terminal { get; set; }
    public string Airline { get; set; }
}
