using MaFi.WebShareCz.ApiClient.Security;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;

namespace MaFi.WebShareCz.ApiClient.Configuration
{
    public sealed class WsConfigSerializer
    {
        private readonly Action _onChange;
        private readonly IDataProtector _protector;

        internal WsConfigSerializer(Action onChange, IDataProtector protector)
        {
            _onChange = onChange;
            _protector = protector;
        }
        
        public WsConfig Deserialize(Stream sourceStream)
        {
            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(WsSerializableConfig));
                WsSerializableConfig serializableConfig = (WsSerializableConfig)serializer.ReadObject(sourceStream);
                return new WsConfig(serializableConfig, _onChange, _protector);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return new WsConfig();
            }
        }

        public void Serialize(Stream targetStream, WsConfig config)
        {
            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(WsSerializableConfig));
                WsSerializableConfig serializableConfig = new WsSerializableConfig(config);
                serializer.WriteObject(targetStream, serializableConfig);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
        }
    }
}
