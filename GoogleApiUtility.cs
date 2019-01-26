using System;
using System.Text;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using System.IO;

namespace Google
{

    internal static class ApiUtility
    {
        /// <summary>
        /// UTF8 Encoding with no byte-order mark prefix.
        /// </summary>
        public static readonly Encoding Utf8NoBom = new UTF8Encoding(false, false);

        public static XElement HttpGetJson(string url, string accessToken)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers.Add(string.Concat("Authorization: Bearer ", accessToken));
            return HttpGetJson(request);
        }

        public static XElement HttpPostJson(string url, string jsonPost, string accessToken)
        {
            var postBytes = Utf8NoBom.GetBytes(jsonPost);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "application/json";
            request.Method = "POST";
            request.Headers.Add(string.Concat("Authorization: Bearer ", accessToken));
            request.ContentLength = postBytes.Length;

            // Send the body
            using (var stream = request.GetRequestStream())
            {
                stream.Write(postBytes, 0, postBytes.Length);
            }

            return HttpGetJson(request);
        }

        public static XElement HttpGetJson(HttpWebRequest request)
        {
            XElement doc = null;
            try
            {
                WebResponse response = request.GetResponse();
                using (var stream = response.GetResponseStream())
                {
                    using (var jsonReader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(stream, new System.Xml.XmlDictionaryReaderQuotas()))
                    {
                        doc = XElement.Load(jsonReader);
                    }
                }
            }
            catch (WebException ex)
            {
                string error = "HTTP ERROR";
                var response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    using (var reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        error = string.Concat(error, "\r\n", reader.ReadToEnd());
                    }
                }
                throw new ApplicationException(error);
            }

            return doc;
        }

        public static void JsonEncode(StringBuilder sb, string value)
        {
            foreach (char c in value)
            {
                switch (c)
                {
                    case '/':
                        sb.Append("\\/");
                        break;

                    case '\\':
                        sb.Append("\\\\");
                        break;

                    case '"':
                        sb.Append("\\\"");
                        break;

                    case '\b':
                        sb.Append("\\b");
                        break;

                    case '\t':
                        sb.Append("\\t");
                        break;

                    case '\r':
                        sb.Append("\\r");
                        break;

                    case '\n':
                        sb.Append("\\n");
                        break;

                    case '\f':
                        sb.Append("\\f");
                        break;

                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u" + ((uint)c).ToString("X4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
        }

        public static string JsonEncode(string value)
        {
            var sb = new StringBuilder();
            JsonEncode(sb, value);
            return sb.ToString();
        }

        public static void DumpXml(XElement xml, TextWriter writer)
        {
            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.CloseOutput = false;
            using (var xmlwriter = XmlWriter.Create(writer, settings))
            {
                xml.WriteTo(xmlwriter);
            }
            writer.WriteLine();
            writer.Flush();
        }
    }

}