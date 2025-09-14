using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace WeatherStationServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var weatherService = new WeatherService();

            weatherService.OnTransferStarted += (sender, sessionId) => Console.WriteLine($"EVENT: Transfer started: {sessionId}");

            weatherService.OnSampleReceived += (sender, sample) => Console.WriteLine($"EVENT: Sample received at {sample.Date}");

            weatherService.OnTransferCompleted += (sender, sessionId) => Console.WriteLine($"EVENT: Transfer completed: {sessionId}");

            weatherService.OnWarningRaised += (sender, warning) => Console.WriteLine($"EVENT: Warning: {warning}");

            using(ServiceHost host = new ServiceHost(typeof(WeatherService)))
            {
                host.Opened += (s, e) =>
                {
                    Console.WriteLine("Weather Service started");
                    Console.WriteLine("Listening on: net.tcp://localhost:8000/WeatherService");
                    Console.WriteLine("Press Enter to stop the service...");
                };

                try
                {
                    host.Open();
                    Console.ReadLine();
                    host.Close();
                }catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    host.Abort();
                }
            }
        }
    }
}
