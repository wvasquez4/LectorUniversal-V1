﻿namespace LectorUniversal.Client.Helpers
{
    public class HttpClientWithoutToken
    {
        public HttpClientWithoutToken(HttpClient httpClient)
        {
            HttpClient = httpClient;
        }

        public HttpClient HttpClient { get; }
    }
}
