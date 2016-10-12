using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Net;
using System.Globalization;

namespace Google
{
    internal static class BloggerUtility
    {
        public const string c_BloggerEndpoint = "https://www.googleapis.com/blogger/v3";

        public static XElement HttpGetJson(string url, string accessToken)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers.Add(string.Concat("Authorization: Bearer ", accessToken));
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
    }

    class Blog
    {
        #region Static Members

        public const string OAuthScope = "https://www.googleapis.com/auth/blogger";

        public static Blog GetByName(string accessToken, string name)
        {
            var doc = BloggerUtility.HttpGetJson(string.Concat(BloggerUtility.c_BloggerEndpoint, "/users/self/blogs"), accessToken);

            IEnumerable<XElement> matches =
                from el in doc.Element("items").Elements("item")
                where el.Element("name").Value.Equals(name, StringComparison.OrdinalIgnoreCase)
                select el;

            if (matches.Count() == 0) return null;
            if (matches.Count() != 1) throw new ApplicationException("More than one album match: " + name);

            return new Blog(accessToken, matches.First());
        }

        #endregion

        #region Construction

        string m_accessToken;
        XElement m_doc;
        string m_name;
        string m_blogId;

        private Blog(string accessToken, XElement doc)
        {
            m_accessToken = accessToken;
            m_doc = doc;
            m_name = doc.Element("name").Value;
            m_blogId = doc.Element("id").Value;
        }

        #endregion

        #region Public Members

        public string Name { get { return m_name; } }

        public BlogPost GetPostByTitle(string title)
        {
            var doc = BloggerUtility.HttpGetJson(string.Concat(BloggerUtility.c_BloggerEndpoint, "/blogs/", m_blogId,
                "/posts/search?q=", System.Uri.EscapeDataString(title), "&fields=items(id,title,published,updated,labels,location)"), m_accessToken);

            var items = doc.Element("items");
            if (items == null) return null;

            IEnumerable<XElement> matches =
                from el in items.Elements("item")
                where el.Element("title").Value.Equals(title, StringComparison.OrdinalIgnoreCase)
                select el;

            if (matches.Count() == 0) return null;

            return new BlogPost(m_accessToken, matches.First());
        }

        public BlogPost AddPost(string title, string bodyHtml, BlogPostMetadata metadata = null, bool isDraft = false)
        {
            byte[] postBytes = assemblePost(m_blogId, title, bodyHtml, metadata);

            // Compose the request
            string url = string.Concat(BloggerUtility.c_BloggerEndpoint, "/blogs/", m_blogId, "/posts/");
            if (isDraft) url = string.Concat(url, "?isDraft=true");
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "application/json";
            request.Method = "POST";
            request.Headers.Add(string.Concat("Authorization: Bearer ", m_accessToken));
            request.ContentLength = postBytes.Length;

            // Send the body
            using (var stream = request.GetRequestStream())
            {
                stream.Write(postBytes, 0, postBytes.Length);
            }

            var doc = BloggerUtility.HttpGetJson(request);

            return new BlogPost(m_accessToken, doc);
        }

        #endregion

        static byte[] assemblePost(string blogId, string title, string bodyHtml, BlogPostMetadata metadata)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"kind\":\"blogger#post\",\"blog\":{\"id\":\"");
            JsonEncode(sb, blogId);
            sb.Append("\"},\"title\":\"");
            JsonEncode(sb, title);
            sb.Append("\"");
            if (metadata != null)
            {
                if (metadata.PublishedDate.Ticks != 0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, ",\"published\":\"{0:s}\"", metadata.PublishedDate.ToUniversalTime());
                }
                if (metadata.UpdatedDate.Ticks != 0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, ",\"updated\":\"{0:s}\"", metadata.UpdatedDate.ToUniversalTime());
                }
                if (metadata.Latitude != 0.0 && metadata.Longitude != 0.0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, ",\"location\":{{\"name\":\"Click Here\",\"lat\":\"{0:F12}\",\"lng\":\"{1:F12}\"}}",
                        metadata.Latitude, metadata.Longitude);
                }
                if (metadata.Labels != null && metadata.Labels.Length != 0)
                {
                    sb.Append(",\"labels\":[");
                    foreach (string label in metadata.Labels)
                    {
                        sb.Append('"');
                        JsonEncode(sb, label);
                        sb.Append("\",");
                    }
                    sb.Remove(sb.Length - 1, 1);    // Remove trailing comma
                    sb.Append(']');
                }
            }
            sb.Append(",\"content\":\"");
            JsonEncode(sb, bodyHtml);
            sb.Append("\"}");

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        static void JsonEncode(StringBuilder sb, string value)
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
                        else {
                            sb.Append(c);
                        }
                        break;
                }
            }
        }

    } // Class Blog

    class BlogPostMetadata
    {
        public DateTime PublishedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string[] Labels { get; set; }
    }

    class BlogPost
    {
        string m_accessToken;
        string m_postId;

        public BlogPost(string accessToken, XElement postDoc)
        {
            m_accessToken = accessToken;
            m_postId = postDoc.Element("id").Value;
        }

        public string Id { get { return m_postId; } }
    }
}
