using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WeatherStationCommon;
using System.IO;
using System.Configuration;
using System.Globalization;
using System.ServiceModel;


namespace WeatherStationClient
{
    class Program
    {
        static void Main(string[] args)
        {
            string csvPath = ConfigurationManager.AppSettings["CsvFilePath"];
            string logPath = ConfigurationManager.AppSettings["LogFilePath"];

            Directory.CreateDirectory(Path.GetDirectoryName(logPath));

            using (StreamWriter logWriter = new StreamWriter(logPath, true))
            {
                logWriter.WriteLine($"{DateTime.Now}: Starting CSV processing");

                try
                { 
                    var samples = LoadSamplesFromCsv(csvPath, logWriter);

                    if(samples.Length == 0)
                    {
                        Console.WriteLine("No valid samples found!");
                        return;
                    }

                    Console.WriteLine($"Loaded {samples.Length} samples from CSV");

                    logWriter.WriteLine($"{DateTime.Now}: Loaded {samples.Length} samples");

                    //zadatak 7

                    var binding = new NetTcpBinding(SecurityMode.None);
                    binding.TransferMode = TransferMode.Streamed;
                    binding.MaxReceivedMessageSize = 10000000;

                    var endpointAddress = new EndpointAddress("net.tcp://localhost:8000/WeatherService");

                    using (var channelFactory = new ChannelFactory<IServiceContract>(binding, endpointAddress))
                    {
                        var client = channelFactory.CreateChannel();

                        var metadata = new SessionMetadata
                        {
                            StationId = "Station_1",
                            StartTime = DateTime.Now,
                            ExpectedSamples = samples.Length
                        };

                        string sessionId = client.StartSession(metadata);
                        Console.WriteLine($"Session started: {sessionId}");
                        logWriter.WriteLine($"{DateTime.Now}: Session started: {sessionId}");

                        int successCount = 0;
                        int errrorCount = 0;

                        for(int i = 0; i < samples.Length; i++)
                        {
                            try
                            {
                                Console.WriteLine($"Sending sample {i+1}/{samples.Length}...");
                                bool success = client.PushSample(samples[i]);

                                if(success)
                                {
                                    successCount++;
                                    Console.WriteLine("Sample sent successfully");
                                }
                                else
                                {
                                    errrorCount++;
                                    Console.WriteLine("Failed to send sample");
                                }
                                System.Threading.Thread.Sleep(100);
                            }catch(FaultException<ValidationFault> ex)
                            {
                                errrorCount++;
                                logWriter.WriteLine($"{DateTime.Now}: Validation error: {ex.Detail.Message}");
                                Console.WriteLine($"Validation error: {ex.Detail.Message}");
                            }catch(FaultException<DataFormatFault> ex)
                            {
                                errrorCount++;
                                logWriter.WriteLine($"{DateTime.Now}: Data format error: {ex.Detail.Message}");
                                Console.WriteLine($"Data format error: {ex.Detail.Message}");
                            }catch (Exception ex)
                            {
                                errrorCount++;
                                logWriter.WriteLine($"{DateTime.Now}: Error sending sample: {ex.Message}");
                                Console.WriteLine($"Error sending sample: {ex.Message}");
                            }
                        }

                        bool ended = client.EndSession(sessionId);
                        Console.WriteLine(ended ? "Session ended successfully" : "Failed to end session");
                        logWriter.WriteLine($"{DateTime.Now}: Session ended: {ended}");
                        Console.WriteLine($"\nSummary: {successCount} successfull, {errrorCount} failed");
                    }
                }
                catch (EndpointNotFoundException)
                {
                    Console.WriteLine("Error: Weather service is not running. Please start the server first.");
                    logWriter.WriteLine($"{DateTime.Now}: Service not available");
                }catch(Exception ex)
                {
                    logWriter.WriteLine($"{DateTime.Now}: {ex.Message}");
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static WeatherSample[] LoadSamplesFromCsv(string csvPath, StreamWriter logWriter)
        {
            var samples = new System.Collections.Generic.List<WeatherSample>();

            if (!File.Exists(csvPath))
            {
                string error = $"CSV file not found: {csvPath}";
                Console.WriteLine(error);
                throw new FileNotFoundException(error);
            }

            int lineNumber = 0;
            int maxSamples = 100;

            try
            {
                using (var reader = new StreamReader(csvPath))
                {
                    reader.ReadLine();
                    lineNumber++;

                    while(!reader.EndOfStream && samples.Count < maxSamples)
                    {
                        lineNumber++;
                        string line = reader.ReadLine();
                        if (string.IsNullOrEmpty(line)) continue;

                        string[] values = line.Split(',');

                        try
                        {
                            if(values.Length >= 6)
                            {
                                var sample = new WeatherSample
                                {
                                    T = double.Parse(values[0], CultureInfo.InvariantCulture),
                                    Tpot = double.Parse(values[1], CultureInfo.InvariantCulture),
                                    Tdew = double.Parse(values[2], CultureInfo.InvariantCulture),
                                    Sh = double.Parse(values[3], CultureInfo.InvariantCulture),
                                    Rh = double.Parse(values[4], CultureInfo.InvariantCulture),
                                    Date = DateTime.Parse(values[5], CultureInfo.InvariantCulture),
                                };

                                samples.Add(sample);
                                Console.WriteLine($"Loaded sample: {sample.Date}");
                            }
                            else
                            {
                                logWriter.WriteLine($"Line {lineNumber}: Invalid number of columns {values.Length}");
                            }
                        }catch(FormatException ex)
                        {
                            logWriter.WriteLine($"Line {lineNumber}: Format error: {ex.Message}");
                        }catch(Exception ex)
                        {
                            logWriter.WriteLine($"Line {lineNumber}: Error: {ex.Message}");
                        }
                    }
                }
                Console.WriteLine($"Finished loading {samples.Count} samples");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading CSV file: {ex.Message}");
                logWriter.WriteLine($"{DateTime.Now}: Error reading CSV: {ex.Message}");
            }
            return samples.ToArray();
        }
    }
}
