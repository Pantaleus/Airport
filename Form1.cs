using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Timer = System.Windows.Forms.Timer;

namespace Airport
{
    public partial class Form1 : Form
    {
        private DatabaseManager dbManager;
        private string apiUrl = "https://aviation-edge.com/v2/public/timetable?key=c95708-e9efa6&iataCode=PRG&type=departure";
        private Timer refreshTimer;
        private int refreshInterval = 10 * 60; // 10 minut v sekundách
        private int countdown;

        public Form1()
        {
            InitializeComponent();
            InitializeApp();
            StartRefreshTimer();
        }

        private void InitializeApp()
        {
            string databasePath = Path.Combine(Environment.CurrentDirectory, "departures.db");
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath); // Odstranit starou databázi, aby se vytvořila nová
            }

            dbManager = new DatabaseManager(databasePath);
            LoadDataIntoGridView();
        }

        private async void LoadDataIntoGridView()
        {
            await FetchAndStoreDepartures();
            var departures = dbManager.GetFilteredDepartures();
            dataGridView1.DataSource = departures;

            // Automatické přizpůsobení šířky sloupců
            dataGridView1.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private async Task FetchAndStoreDepartures()
        {
            try
            {
                var departures = await GetDeparturesFromApi();
                foreach (var departure in departures)
                {
                    string flightNumber = departure["flight"]["iataNumber"]?.ToString();
                    string destination = departure["arrival"]["iataCode"]?.ToString();
                    string city = DatabaseManager.IATAToCityMap.ContainsKey(destination) ? DatabaseManager.IATAToCityMap[destination] : destination;
                    string gate = departure["departure"]["gate"]?.ToString();
                    string boardingTime = departure["departure"]["estimatedTime"]?.ToString();
                    string scheduledTime = departure["departure"]["scheduledTime"]?.ToString();
                    string actualTime = departure["departure"]["actualTime"]?.ToString();
                    string status = departure["departure"]["status"]?.ToString();
                    string terminal = departure["departure"]["terminal"]?.ToString();
                    string airline = departure["airline"]["name"]?.ToString();

                    dbManager.InsertDeparture(flightNumber, destination, city, gate, boardingTime, scheduledTime, actualTime, status, terminal, airline);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching data: {ex.Message}");
            }
        }

        private async Task<JArray> GetDeparturesFromApi()
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                var response = await client.GetStringAsync(apiUrl);
                return JArray.Parse(response);
            }
        }

        private void ButtonRefresh_Click(object sender, EventArgs e)
        {
            LoadDataIntoGridView();
            countdown = refreshInterval; // Reset odpočtu po manuální obnově
        }

        private void StartRefreshTimer()
        {
            countdown = refreshInterval;
            refreshTimer = new Timer { Interval = 1000 }; // Interval 1 sekunda
            refreshTimer.Tick += OnTimerTick;
            refreshTimer.Start();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (countdown > 0)
            {
                countdown--;
                lblCountdown.Text = $"Obnova dat za: {countdown / 60:D2}:{countdown % 60:D2}";
            }
            else
            {
                LoadDataIntoGridView();
                countdown = refreshInterval;
            }
        }
    }
}
