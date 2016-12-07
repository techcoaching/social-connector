﻿namespace App.Common.Connector.Twitter
{
    using App.Common.Configurations;
    using App.Common.DI;
    using App.Common.Http;
    using System.Net.Http;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    internal class TwitterConnector : BaseConnector
    {
        protected const string AppConsumerKey = "wZc0bZ4IhQ0IFBfhdPnnT5cJi";
        protected const string AppConsumerSecret = "og5tNjm7cWqZSDaUhus0QJldRBZt0KYhd0bnStLXjcNpodageH";
        protected const string AccessToken = "806037750881226752-ngNvUw6NlEy5tpxBwFEe79PQo4NUAXf";
        protected const string AccessTokenSecret = "2p4SswJXfJ61joywV4P89LMe5bT0r0wh7aT7zz1HwMCOb";


        //public const string AppConsumerKey = "xvz1evFS4wEEPTGEFPHBog";
        //public const string AppConsumerSecret = "kAcSOqF21Fu85e7zjz7ZN2U4ZRhfV3WpwPAoE3Z7kBw";
        //public const string AccessToken = "370773112-GmHxMAgYyLbNEtIKZeRNFsMKPR9EyMZeS9weJAEb";
        //public const string AccessTokenSecret = "LswwdoUaIvS8ltyTt5jkRh4J50vUPVVHtR2YPi5kE";

        private readonly DateTime EpochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly HMACSHA1 sigHasher;

        public TwitterConnector()
        {
            this.sigHasher = new HMACSHA1(new ASCIIEncoding().GetBytes(string.Format("{0}&{1}", TwitterConnector.AppConsumerSecret, TwitterConnector.AccessTokenSecret)));
        }

        public override IResponseData<TResponse> Post<TRequest, TResponse>(string uri, TRequest data)
        {
            IConnectorBuilderFactory builderFactory = IoC.Container.Resolve<IConnectorBuilderFactory>();
            IRequestBuilder requestBuilder = builderFactory.Create(BuilderFactoryType.Twitter);
            OAuthRequest request = requestBuilder.GetOAuthRequest(data);

            using (HttpClient client = this.CreateHttpClient(Configuration.Current.Twitter.BaseApiUrl, request))
            {
                HttpContent content = new JsonContent<TRequest>(data);
                HttpResponseMessage responseMessage = client.PostAsync(request.Url, content).Result;
                IResponseData<TResponse> result = this.GetResponseAs<ResponseData<TResponse>>(responseMessage.Content);
                return result;
            }
        }

        protected HttpClient CreateHttpClient(string baseUrl, OAuthRequest request)
        {
            string authHeader = this.GetAuthHeader(request);
            HttpClient client = base.CreateHttpClient(baseUrl);
            client.DefaultRequestHeaders.Add("Authorization", authHeader);
            return client;
        }

        private string GetAuthHeader(OAuthRequest request)
        {
            var timestamp = (int)((DateTime.UtcNow - this.EpochUtc).TotalSeconds);
            request.Data.Add("oauth_consumer_key", TwitterConnector.AppConsumerKey);
            request.Data.Add("oauth_nonce", "kYjzVBB8Y0ZFabxSWbWovY3uYSQ2pTgmZeNu2VS4cg");
            request.Data.Add("oauth_signature_method", "HMAC-SHA1");
            request.Data.Add("oauth_timestamp", timestamp.ToString());
            request.Data.Add("oauth_token", TwitterConnector.AccessToken);
            request.Data.Add("oauth_version", "1.0");
            request.Data.Add("oauth_signature", this.GenerateSignature(request));
            return this.GenerateOAuthHeader(request.Data);
        }

        private string GenerateOAuthHeader(Dictionary<string, string> data)
        {
            return "OAuth " + string.Join(", ",
                data.Where(kvp => kvp.Key.StartsWith("oauth_"))
                .Select(kvp => string.Format("{0}=\"{1}\"", Uri.EscapeDataString(kvp.Key), Uri.EscapeDataString(kvp.Value)))
                .OrderBy(s => s)
            );
        }

        private string GenerateSignature(OAuthRequest request)
        {
            string fullUrl = request.Url;

            var sigString = string.Join("&", request.Data
                .Union(request.Data)
                .Select(kvp => string.Format("{0}={1}", Uri.EscapeDataString(kvp.Key), Uri.EscapeDataString(kvp.Value)))
                .OrderBy(s => s)
            );

            var fullSigData = string.Format(
                "{0}&{1}&{2}",
                "POST",
                Uri.EscapeDataString(fullUrl),
                Uri.EscapeDataString(sigString.ToString())
            );

            return Convert.ToBase64String(this.sigHasher.ComputeHash(new ASCIIEncoding().GetBytes(fullSigData.ToString())));
        }
    }
}