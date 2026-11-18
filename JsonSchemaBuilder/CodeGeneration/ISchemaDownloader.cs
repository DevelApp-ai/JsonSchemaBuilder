using System;
using System.Net.Http;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    /// <summary>
    /// Downloads a remote schema document. Injectable so tests and hosts can
    /// supply deterministic fake downloads instead of doing network calls
    /// </summary>
    public interface ISchemaDownloader
    {
        /// <summary>
        /// Downloads the schema at the uri and returns it as a string
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        string Download(Uri uri);
    }

    /// <summary>
    /// Default downloader using HttpClient. Replaces the obsolete WebClient
    /// download this library historically used
    /// </summary>
    public class HttpSchemaDownloader : ISchemaDownloader
    {
        /// <summary>
        /// Downloads the schema at the uri and returns it as a string
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public string Download(Uri uri)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                return httpClient.GetStringAsync(uri).GetAwaiter().GetResult();
            }
        }
    }
}
