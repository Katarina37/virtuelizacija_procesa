using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WeatherStationCommon;
using System.IO;
using System.ServiceModel;


namespace WeatherStationServer
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
    public class WeatherService : IServiceContract, IDisposable
    {
        private string _sessionId;
        private StreamWriter _measurementsWriter;
        private StreamWriter _rejectsWriter;
        private string _dataDirectory;

        public WeatherService()
        {
            _dataDirectory = System.Configuration.ConfigurationManager.AppSettings["DataDirectory"];
            Directory.CreateDirectory(_dataDirectory);
        }

        public string StartSession(SessionMetadata meta)
        {
            try
            {
                if (string.IsNullOrEmpty(meta.StationId))
                    throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "StationId is required" });
                
                if(meta.ExpectedSamples <= 0)
                    throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "ExpectedSamples must be greater than 0" });

                _sessionId = $"{meta.StationId}_{meta.StartTime:yyyyMMdd_HHmmss}";

                string measurementsFile = Path.Combine(_dataDirectory, $"{_sessionId}_measurements.csv");
                string rejectsFile = Path.Combine(_dataDirectory, $"{_sessionId}_rejects.csv");

                _measurementsWriter = new StreamWriter(measurementsFile, true);
                _rejectsWriter = new StreamWriter(rejectsFile, true);

                _measurementsWriter.WriteLine("T,Tpot,Tdew,Sh,Rh,Date");
                _rejectsWriter.WriteLine("T,Tpot,Tdew,Sh,Rh,Date,Reason");

                return _sessionId;
            }
            catch (Exception ex)
            {
                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = ex.Message });
            }
        }

        public bool PushSample(WeatherSample sample)
        {
            try
            {
                if (sample.Date == DateTime.MinValue)
                    throw new FaultException<ValidationFault>(new ValidationFault { Message = "Invalid date"});

                if (sample.Sh <= 0)
                    throw new FaultException<ValidationFault>(new ValidationFault { Message = "Specific humidity must be positive"});

                if (sample.Sh < 0 || sample.Sh > 100)
                    throw new FaultException<ValidationFault>(new ValidationFault { Message = "Relative humidity must be between 0 and 100"});

                _measurementsWriter.WriteLine($"{sample.T},{sample.Tpot},{sample.Tdew},{sample.Sh},{sample.Rh},{sample.Date:o}");
                _measurementsWriter.Flush();

                return true;
            }
            catch (FaultException) 
            {
                throw;
            }
            catch(Exception ex)
            {
                _rejectsWriter.WriteLine($"{sample.T},{sample.Tpot},{sample.Tdew},{sample.Sh},{sample.Rh},{sample.Date:o},{ex.Message}");
                _rejectsWriter.Flush();

                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = ex.Message });
            }
        }

        public bool EndSession(string sessionId)
        {
            try
            {
                if (_sessionId != sessionId)
                    throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "Invalid session ID"});

                DisposeWriters();
                return true;
            }
            catch(Exception ex)
            {
                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = ex.Message});
            }
        }

        private void DisposeWriters()
        {
            _measurementsWriter?.Close();
            _measurementsWriter?.Dispose();

            _rejectsWriter?.Close();
            _rejectsWriter?.Dispose();
        }

        public void Dispose()
        {
            DisposeWriters();
        }
    }
}
