using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using MaFi.WebShareCz.ApiClient.Http;

namespace MaFi.WebShareCz.ApiClient.Entities
{
    [DataContract(Name = "file", Namespace = "")]
    public class WsFilePreview : IDisposable
    {
        private CancellationTokenSource _cts = null;
        private bool _disposed = false;

        internal WsFilePreview()
        {
            JpgData = Task.FromResult<byte[]>(null);
        }

        internal WsFilePreview(string name) : this()
        {
            Name = name;
        }

        internal void StartDownload(WsHttpClient httpClient)
        {
            JpgData = GetJpgData(httpClient);
        }

        public Task<byte[]> JpgData { get; private set; }

        [DataMember(Name = "ident", Order = 1)]
        public string Ident { get; private set; }

        [DataMember(Name = "name", Order = 2)]
        public string Name { get; private set; }

        [DataMember(Name = "img", Order = 4)]
        private string Url { get; set; }

        private async Task<byte[]> GetJpgData(WsHttpClient httpClient)
        {
            if (_disposed || string.IsNullOrWhiteSpace(Url))
                return JpgData.Result;
            try
            {
                using (_cts = new CancellationTokenSource())
                using (HttpResponseMessage response = await httpClient.GetData(new Uri(Url), _cts.Token))
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
            finally
            {
                _cts = null;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _cts?.Cancel();
            _cts = null;
        }
    }
}
