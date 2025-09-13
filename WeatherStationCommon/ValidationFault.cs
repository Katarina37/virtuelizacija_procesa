using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace WeatherStationCommon
{
    [DataContract]
    public class ValidationFault
    {
        [DataMember]
        public string Message { get; set; }
    }
}
