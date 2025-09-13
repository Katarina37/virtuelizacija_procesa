using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace WeatherStationCommon
{
    [ServiceContract]
    public interface IServiceContract
    {
        [OperationContract]
        [FaultContract(typeof(DataFormatFault))]
        string StartSession(SessionMetadata meta);

        [OperationContract]
        [FaultContract(typeof(DataFormatFault))]
        [FaultContract (typeof(ValidationFault))]
        bool PushSample(WeatherSample sample);

        [OperationContract]
        [FaultContract(typeof(DataFormatFault))]
        bool EndSession(string sessionId);
    }
}
