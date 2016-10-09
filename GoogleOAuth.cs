﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Diagnostics;

namespace Google
{
    /// <summary>
    /// Manages authentication and authorization with Google Data APIs.
    /// API Documentation here: https://developers.google.com/identity/protocols/OAuth2InstalledApp
    /// Requires reference to System.Runtime.Serializaton for JSON parsing.
    /// </summary>
    class OAuth
    {
        const string c_gooogleAuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        const string c_googleTokenExchangeEndpoint = "https://www.googleapis.com/oauth2/v4/token";

        string m_clientId;
        string m_clientSecret;

        public OAuth(string clientId, string clientSecret)
        {
            m_clientId = clientId;
            m_clientSecret = clientSecret;
        }

        public string LoginHint { get; set; }
        public string Error { get; private set; }

        public string Access_Token { get; private set; }
        public string Id_Token { get; private set; }
        public string Refresh_Token { get; private set; }
        public int Expires_In { get; private set; }
        public string Token_Type { get; private set; }

        public bool Authorize(params string[] scopes)
        {
            return Authorize(string.Join(" ", scopes));
        }

        public bool Authorize(string scope)
        {
            // Clear any previous authorization
            Access_Token = null;
            Id_Token = null;
            Refresh_Token = null;
            Expires_In = 0;
            Token_Type = null;

            // Create a redirect Url to which the browser will go once the user has been authenticated.
            string redirectUrl = string.Format("http://{0}:{1}/", IPAddress.Loopback, GetUnusedPortNumber());
            Debug.WriteLine("Redirect URL = " + redirectUrl);

            // Create an HttpListener to listen for requests on that redirect URL.
            var http = new HttpListener();
            http.Prefixes.Add(redirectUrl);
            Debug.WriteLine("HTTP Listening...");
            http.Start();

            // Create the OAuth 2.0 authorization request URL
            string authorizationRequestUrl = string.Format("{0}?response_type=code&client_id={1}&redirect_uri={2}&scope={3}",
                c_gooogleAuthorizationEndpoint,
                m_clientId,
                System.Uri.EscapeDataString(redirectUrl),
                System.Uri.EscapeDataString(scope));

            if (!string.IsNullOrEmpty(LoginHint))
            {
                authorizationRequestUrl = string.Concat(authorizationRequestUrl, "&login_hint=", System.Uri.EscapeUriString(LoginHint));
            }

            // Open request in the browser.
            System.Diagnostics.Process.Start(authorizationRequestUrl);

            // Wait for the OAuth authorization response.
            var context = http.GetContext();

            // Send an HTTP response to the browser.
            {
                var response = context.Response;
                string responseString = string.Format("<html><head></head><body>Application has been authorized. You may close this window.</body></html>");  //"<html><head><meta http-equiv='refresh' content='10;url=https://google.com'></head><body>Please return to the app.</body></html>"
                var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                var responseOutput = response.OutputStream;
                Task responseTask = responseOutput.WriteAsync(buffer, 0, buffer.Length).ContinueWith((task) =>
                {
                    responseOutput.Close();
                    http.Stop();
                    Debug.WriteLine("HTTP server stopped.");
                });
            }

            // Checks for errors.
            if (context.Request.QueryString.Get("error") != null)
            {
                Error = context.Request.QueryString.Get("error");
                return false;
            }

            var code = context.Request.QueryString.Get("code");
            if (string.IsNullOrEmpty(code))
            {
                Error = "Malformed authorization response.";
                return false;
            }

            // Exchange the code for tokens
            return ExchangeForTokens(string.Format("code={0}&client_id={1}&client_secret={2}&redirect_uri={3}&grant_type=authorization_code",
                code, m_clientId, m_clientSecret, redirectUrl));
        }

        public bool Refresh(string refreshToken)
        {
            Refresh_Token = refreshToken;
            return ExchangeForTokens(string.Format("refresh_token={0}&client_id={1}&client_secret={2}&grant_type=refresh_token",
                refreshToken, m_clientId, m_clientSecret));
        }

        private bool ExchangeForTokens(string requestString)
        {
            HttpWebRequest tokenRequest = (HttpWebRequest)WebRequest.Create(c_googleTokenExchangeEndpoint);
            tokenRequest.Method = "POST";
            tokenRequest.ContentType = "application/x-www-form-urlencoded";
            tokenRequest.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            byte[] bodyBytes = Encoding.ASCII.GetBytes(requestString);
            tokenRequest.ContentLength = bodyBytes.Length;
            using (var stream = tokenRequest.GetRequestStream())
            {
                stream.Write(bodyBytes, 0, bodyBytes.Length);
            }

            try
            {
                WebResponse response = tokenRequest.GetResponse();
                using (var reader = new JsonReader(response.GetResponseStream()))
                {
                    while (reader.Read())
                    {
                        switch (reader.Name)
                        {
                            case "access_token":
                                Access_Token = reader.Value;
                                break;

                            case "id_token":
                                Id_Token = reader.Value;
                                break;

                            case "refresh_token":
                                Refresh_Token = reader.Value;
                                break;

                            case "expires_in":
                                {
                                    int val;
                                    int.TryParse(reader.Value, out val);
                                    Expires_In = val;
                                }
                                break;

                            case "token_type":
                                Token_Type = reader.Value;
                                break;
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                string error = "Error converting OAuth Token";
                var response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    string errTxt = string.Empty;
                    using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        errTxt = reader.ReadToEnd();
                    }

                    error = string.Format("Error converting OAuth Token: HTTP {0}: {1}", response.StatusCode, errTxt);
                }
                Error = error;
                return false;
            }

            return true;
        }


        #region Private Helper Functions

        private static int GetUnusedPortNumber()
        {
            int port = 0;
            using (var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp))
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 0);
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                port = ((IPEndPoint)socket.LocalEndPoint).Port;
            }
            return port;
        }

        private class JsonReader : IDisposable
        {
            Stream m_stream;
            System.Xml.XmlReader m_reader;

            public JsonReader(Stream stream)
            {
                m_stream = stream;
                m_reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(stream, new System.Xml.XmlDictionaryReaderQuotas());
            }

            public string Name { get; private set; }
            public string Value { get; private set; }

            public bool Read()
            {
                while (m_reader.Read())
                {
                    if (m_reader.NodeType == System.Xml.XmlNodeType.Element && !m_reader.IsEmptyElement)
                    {
                        Name = m_reader.Name;
                    }
                    else if (m_reader.NodeType == System.Xml.XmlNodeType.EndElement)
                    {
                        Name = null;
                    }
                    else if (m_reader.NodeType == System.Xml.XmlNodeType.Text && Name != null)
                    {
                        Value = m_reader.Value;
                        return true;
                    }
                }
                return false;
            }

            public void Dispose()
            {
                if (m_reader != null)
                {
                    m_reader.Dispose();
                }
                if (m_stream != null)
                {
                    m_stream.Dispose();
                    m_stream = null;
                }
            }
        }

        private static Dictionary<string, string> JsonToDictionary(Stream stream)
        {
            var result = new Dictionary<string, string>();
            using (System.Xml.XmlReader reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(stream, new System.Xml.XmlDictionaryReaderQuotas()))
            {
                string elementName = null;
                while (reader.Read())
                {
                    if (reader.NodeType == System.Xml.XmlNodeType.Element && !reader.IsEmptyElement)
                    {
                        elementName = reader.Name;
                    }
                    else if (reader.NodeType == System.Xml.XmlNodeType.EndElement)
                    {
                        elementName = null;
                    }
                    else if (reader.NodeType == System.Xml.XmlNodeType.Text && elementName != null)
                    {
                        result[elementName] = reader.Value;
                    }
                }
            }
            return result;
        }

        #endregion

    }
}
