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

                }
                catch (Exception ex)
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
