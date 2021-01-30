using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using MaFi.WebShareCz.ApiClient.Entities.Internals;

namespace MaFi.WebShareCz.ApiClient.Http
{
    internal sealed class WsHttpClient : IDisposable
    {
        private const string XML_ACCEPT = "text/xml; charset=UTF-8";
        private readonly Uri _baseUri;
        private readonly HttpClient _httpClient;

        public WsHttpClient(Uri baseUri)
        {
            _baseUri = baseUri;
            HttpClientHandler handler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        public Task<TResult> PostFormData<TResult>(Uri url, HttpContent content, CancellationToken cancellationToken = new CancellationToken(), bool acceptXml = true, TimeSpan? timeout = null) where TResult : Result
        {
            return PostFormData<TResult>(url, content, acceptXml ? XML_ACCEPT : "application/json", cancellationToken, timeout);
        }

        public Task<HttpResponseMessage> PostFormData(Uri url, HttpContent content, CancellationToken cancellationToken = new CancellationToken())
        {
            return PostFormData(url, content, XML_ACCEPT, cancellationToken);
        }

        public async Task<HttpResponseMessage> GetData(Uri url, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();
            return (await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)).EnsureSuccessStatusCode();
        }

        private async Task<TResult> PostFormData<TResult>(Uri url, HttpContent content, string accept, CancellationToken cancellationToken, TimeSpan? timeout) where TResult : Result
        {
            using (HttpResponseMessage response = await PostFormData(url, content, accept, cancellationToken, timeout))
            using (HttpContent responseContent = response.Content)
            using (Stream responseStream = await responseContent.ReadAsStreamAsync())
            {
                if (accept == XML_ACCEPT)
                    return DeserializeXml<TResult>(responseStream);
                return DeserializeJson<TResult>(responseStream);
            }
        }

        private async Task<HttpResponseMessage> PostFormData(Uri url, HttpContent content, string accept, CancellationToken cancellationToken, TimeSpan? timeout = null)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Accept", accept);
            request.Headers.Referrer = _baseUri;
            request.Content = content;
            using (CancellationTokenSource ctsForTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                ctsForTimeout.CancelAfter(timeout ?? TimeSpan.FromSeconds(30));
                return (await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctsForTimeout.Token)).EnsureSuccessStatusCode();
            }
        }

        private static TResult DeserializeXml<TResult>(Stream stream)
        {
            DataContractSerializer serializer = new DataContractSerializer(typeof(TResult));
            return (TResult)serializer.ReadObject(stream);
        }

        private static TResult DeserializeJson<TResult>(Stream stream)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TResult));
            return (TResult)serializer.ReadObject(stream);
        }

        void IDisposable.Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
