using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Nerven.Self
{
    public static class Helpers
    {
        private static readonly JsonSerializer _JsonSerializer = JsonSerializer.Create();
        private static readonly Encoding _SerializationEncoding = Encoding.UTF8;

        public static void SerializeData<TData>(string cacheTo, TData data)
        {

            using (var _dataTextWriter = new StreamWriter(cacheTo, false, _SerializationEncoding))
            using (var _dataJsonReader = new JsonTextWriter(_dataTextWriter))
            {
                _JsonSerializer.Serialize(_dataJsonReader, data);
            }
        }

        public static TData DeserializeData<TData>(string cacheFrom)
        {
            using (var _dataTextReader = new StreamReader(cacheFrom, _SerializationEncoding))
            using (var _dataJsonReader = new JsonTextReader(_dataTextReader))
            {
                return _JsonSerializer.Deserialize<TData>(_dataJsonReader);
            }
        }
    }
}
