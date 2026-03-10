using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Http
{

    // HttpResponse is in one of 3 states:
    // - ResponseMessageInfo is object && ResponseMessageInfo.IsSuccessStatusCode -> success, inspect ResponseMessageInfo for StatusCode etc
    // - ResponseMessageInfo is object && !ResponseMessageInfo.IsSuccessStatusCode -> failure, inspect ResponseMessageInfo for StatusCode, ReasonPhrase etc
    // - ResponseMessageInfo is null -> exception, inspect ExceptionInfo fields
    public record HttpResponse
    {

        // copies of HttpRequestMessage and HttpResponseMessage which do not have the content and do not need to be disposed
        public record HttpRequestMessageInfo(HttpRequestHeaders Headers, HttpMethod Method, HttpRequestOptions Options, Uri? RequestUri, Version Version, HttpVersionPolicy VersionPolicy);
        public record HttpResponseMessageInfo(HttpResponseHeaders Headers, bool IsSuccessStatusCode, string? ReasonPhrase, HttpRequestMessageInfo RequestMessage, HttpStatusCode StatusCode, HttpResponseHeaders TrailingHeaders, Version Version);

        // holds Http exception information
        public record HttpExceptionInfo(HttpRequestMessageInfo HttpRequestMessage, string ErrorMessage, WebExceptionStatus? WebExceptionStatus);

        // if ResponseMessageInfo is null ExceptionInfo is not and vice versa
        public HttpResponseMessageInfo? ResponseMessageInfo { get; init; }
        public HttpExceptionInfo? ExceptionInfo { get; init; }

        public HttpResponse(HttpRequestMessage requestMessage, HttpResponseMessage responseMessage)
        {
            var requestMessageInfo = new HttpRequestMessageInfo(requestMessage.Headers, requestMessage.Method, requestMessage.Options, requestMessage.RequestUri, requestMessage.Version, requestMessage.VersionPolicy);
            ResponseMessageInfo = new(responseMessage.Headers, responseMessage.IsSuccessStatusCode, responseMessage.ReasonPhrase, requestMessageInfo, responseMessage.StatusCode, responseMessage.TrailingHeaders, responseMessage.Version);
            ExceptionInfo = null;
        }

        public HttpResponse(HttpRequestMessage requestMessage, Exception exception)
        {
            ResponseMessageInfo = null;
            var requestMessageInfo = new HttpRequestMessageInfo(requestMessage.Headers, requestMessage.Method, requestMessage.Options, requestMessage.RequestUri, requestMessage.Version, requestMessage.VersionPolicy);

            if (exception is WebException ex1 && ex1.Status == WebExceptionStatus.ProtocolError)
            {
                using HttpWebResponse? httpResponse = (HttpWebResponse?)ex1.Response;
                ExceptionInfo = new(requestMessageInfo, httpResponse?.StatusDescription ?? "", ex1.Status);
            }
            else if (exception is WebException ex2) ExceptionInfo = new(requestMessageInfo, ex2.FullMessage(), ex2.Status);
            else if (exception is TaskCanceledException ex3 && ex3.InnerException is TimeoutException) ExceptionInfo = new(requestMessageInfo, ex3.InnerException.FullMessage(), WebExceptionStatus.Timeout);
            else if (exception is TaskCanceledException ex4) ExceptionInfo = new(requestMessageInfo, ex4.FullMessage(), WebExceptionStatus.RequestCanceled);
            else ExceptionInfo = new(requestMessageInfo, exception.FullMessage(), null);
        }

        public override string ToString()
        {
            if (ResponseMessageInfo is object)
            {
                var msg = ResponseMessageInfo.IsSuccessStatusCode ? "Success" : "Failure";
                msg += $" {Enum.GetName(typeof(HttpStatusCode), ResponseMessageInfo.StatusCode)}";
                if (ResponseMessageInfo.ReasonPhrase is object) msg += $" {ResponseMessageInfo.ReasonPhrase}";
                return msg;

            }
            else if (ExceptionInfo is object)
            {
                var msg = "Failure";
                msg += $" {ExceptionInfo.ErrorMessage}";
                if (ExceptionInfo.WebExceptionStatus is object) msg += $" {Enum.GetName(typeof(WebExceptionStatus), ExceptionInfo.WebExceptionStatus)}";
                return msg;
            }
            return "NA"; // never reach here
        }
    }


    public static class ExtensionMethods
    {

        // progressCallback recieves (bytesRecieved, percent, speedKbSec) and can return false to cancell download
        public static async Task<(bool success, HttpResponse httpResponse)> DownloadFileAsync(this HttpClient httpClient, Uri requestUri, string fileToWriteTo, CancellationTokenSource? cts = null, Func<long, int, float, bool>? progressCallback = null)
        {
            var httpRequestMessage = new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = requestUri };
            var created = false;

            try
            {
                var cancellationToken = cts?.Token ?? default;

                using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!httpResponseMessage.IsSuccessStatusCode) return (false, new(httpRequestMessage, httpResponseMessage));
                var contentLength = httpResponseMessage.Content.Headers.ContentLength;

                using Stream streamToReadFrom = await httpResponseMessage.Content.ReadAsStreamAsync();
                using Stream streamToWriteTo = File.Open(fileToWriteTo, FileMode.Create);
                created = true;

                var buffer = new byte[81920];
                var bytesRecieved = (long)0;
                var stopwatch = Stopwatch.StartNew();
                int bytesInBuffer;
                while ((bytesInBuffer = await streamToReadFrom.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    await streamToWriteTo.WriteAsync(buffer.AsMemory(0, bytesInBuffer), cancellationToken);
                    bytesRecieved += bytesInBuffer;
                    if (progressCallback is object)
                    {
                        var percent = contentLength is object && contentLength != 0 ? (int)Math.Floor(bytesRecieved / (float)contentLength * 100.0) : 0;
                        var speedKbSec = (float)((bytesRecieved / 1024.0) / (stopwatch.ElapsedMilliseconds / 1000.0));
                        var proceed = progressCallback(bytesRecieved, percent, speedKbSec);
                        if (!proceed)
                        {
                            httpResponseMessage.ReasonPhrase = "Callback cancelled download";
                            httpResponseMessage.StatusCode = HttpStatusCode.PartialContent;
                            return (false, new(httpRequestMessage, httpResponseMessage));
                        }
                    }
                }

                return (true, new(httpRequestMessage, httpResponseMessage));
            }
            catch (Exception ex)
            {
                if (created) try { File.Delete(fileToWriteTo); } catch { };
                return (false, new(httpRequestMessage, ex));
            }
        }

        public static async Task<(string? ResponseAsString, HttpResponse httpResponse)> GetToStringAsync(this HttpClient httpClient, Uri requestUri, CancellationTokenSource? cts = null)
        {
            var httpRequestMessage = new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = requestUri };
            try
            {
                var cancellationToken = cts?.Token ?? default;
                using var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, cancellationToken);
                if (!httpResponseMessage.IsSuccessStatusCode) return (null, new(httpRequestMessage, httpResponseMessage));

                var responseAsString = await httpResponseMessage.Content.ReadAsStringAsync();
                return (responseAsString, new(httpRequestMessage, httpResponseMessage));
            }
            catch (Exception ex)
            {
                return (null, new(httpRequestMessage, ex)); ;
            }
        }

        public static async Task<(string? ResponseAsString, HttpResponse httpResponse)> PostToStringAsync(this HttpClient httpClient, Uri requestUri, HttpContent postBuffer, CancellationTokenSource? cts = null)
        {
            var httpRequestMessage = new HttpRequestMessage { Method = HttpMethod.Post, RequestUri = requestUri, Content = postBuffer };
            try
            {
                var cancellationToken = cts?.Token ?? default;
                using var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, cancellationToken);
                if (!httpResponseMessage.IsSuccessStatusCode) return (null, new(httpRequestMessage, httpResponseMessage));

                var responseAsString = await httpResponseMessage.Content.ReadAsStringAsync();
                return (responseAsString, new(httpRequestMessage, httpResponseMessage));
            }
            catch (Exception ex)
            {
                return (null, new(httpRequestMessage, ex));
            }
        }

    }
}

namespace System
{
    public static class ExtensionMethods
    {
        public static string FullMessage(this Exception ex)
        {
            if (ex is AggregateException aex) return aex.InnerExceptions.Aggregate("[ ", (total, next) => $"{total}[{next.FullMessage()}] ") + "]";
            var msg = ex.Message.Replace(", see inner exception.", "").Trim();
            var innerMsg = ex.InnerException?.FullMessage();
            if (innerMsg is object && innerMsg != msg) msg = $"{msg} [ {innerMsg} ]";
            return msg;
        }
    }
}
