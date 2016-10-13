/* Based on Google Picasa Web Albums Data API
https://developers.google.com/picasa-web/docs/2.0/developers_guide_protocol
*/
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Net;
using System.IO;

namespace Google
{

    internal static class WebAlbumUtility
    {
        public const string c_PicasawebEndpoint = "https://picasaweb.google.com/data/feed/api";

        public const string c_VersionHeader = "GData-Version";
        public const string c_VersionValue = "2";

        public static XElement HttpGetXml(string url, string accessToken)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers.Add(c_VersionHeader, c_VersionValue);
            request.Headers.Add(string.Concat("Authorization: Bearer ", accessToken));
            return HttpGetXml(request);
        }

        public static XElement HttpGetXml(HttpWebRequest request)
        {
            XElement doc = null;
            try
            {
                WebResponse response = request.GetResponse();
                using (var stream = response.GetResponseStream())
                {
                    doc = XElement.Load(stream);
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

    class WebAlbum
    {
        #region Static Members

        public const string OAuthScope = "https://picasaweb.google.com/data/";

        static XNamespace nsAtom = "http://www.w3.org/2005/Atom";
        static XNamespace nsGphoto = "http://schemas.google.com/photos/2007";

        public static WebAlbum GetByTitle(string accessToken, string title)
        {
            var xml = WebAlbumUtility.HttpGetXml(string.Concat(WebAlbumUtility.c_PicasawebEndpoint, "/user/default?kind=album"), accessToken);
            IEnumerable<XElement> matches =
                from el in xml.Elements(nsAtom + "entry")
                where el.Element(nsAtom + "title").Value.Equals(title, StringComparison.OrdinalIgnoreCase)
                select el;

            if (matches.Count() == 0) return null;
            if (matches.Count() != 1) throw new ApplicationException("More than one album match: " + title);

            return new WebAlbum(accessToken, matches.First());
        }

        public static XElement GetAlbumList(string accessToken, string userId = "default")
        {
            return WebAlbumUtility.HttpGetXml(string.Concat(WebAlbumUtility.c_PicasawebEndpoint, "/user/", userId, "?kind=album"), accessToken);
        }

        #endregion

        #region Construction

        string m_accessToken;
        XElement m_album;
        string m_userId;
        string m_albumId;
        string m_title;


        private WebAlbum(string accessToken, XElement album)
        {
            m_accessToken = accessToken;
            m_title = album.Element(nsAtom + "title").Value;    // Replace with the actual title (which might have capitalization differences)
            m_userId = album.Element(nsGphoto + "user").Value;
            m_albumId = album.Element(nsGphoto + "id").Value;
            Refresh();
        }

        #endregion Construction

        #region Public Members

        public string Title
        {
            get { return m_title; }
        }

        // TODO: Add properties for accessToken, userId, albumId

        public void Refresh(string imgmax = "d")
        {
            m_album = WebAlbumUtility.HttpGetXml(string.Concat(WebAlbumUtility.c_PicasawebEndpoint, "/user/", m_userId, "/album/", m_albumId, "?kind=photo&imgmax=", imgmax), m_accessToken);
        }

        /// <summary>
        /// Gets the photo with a particular title.
        /// </summary>
        /// <param name="title">The title to match.</param>
        /// <returns>The photo if found or null if not found.</returns>
        /// <remarks>The title is actually the filename (and the trailing part of the URL). Therefore
        /// by metata standards the label should be 'name', not 'title'.</remarks>
        public WebPhoto GetPhotoByTitle(string title)
        {
            IEnumerable<XElement> matches =
                from el in m_album.Elements(nsAtom + "entry")
                where el.Element(nsAtom + "title").Value.Equals(title, StringComparison.OrdinalIgnoreCase)
                select el;

            if (matches.Count() == 0) return null;

            return new WebPhoto(m_accessToken, m_userId, m_albumId, matches.First());
        }

        /// <summary>
        /// Gets the photo with a particular summary.
        /// </summary>
        /// <param name="summary">The summary to match.</param>
        /// <returns>The photo if found or null if not found.</returns>
        /// <remarks>The summary is more like what metata standards would call 'title' whereas 'title'
        /// in Picasa/Google terms is more like 'name' or 'filename'.</remarks>
        public WebPhoto GetPhotoBySummary(string summary)
        {
            IEnumerable<XElement> matches =
                from el in m_album.Elements(nsAtom + "entry")
                where el.Element(nsAtom + "summary").Value.Equals(summary, StringComparison.OrdinalIgnoreCase)
                select el;

            if (matches.Count() == 0) return null;

            return new WebPhoto(m_accessToken, m_userId, m_albumId, matches.First());
        }

        public WebPhoto AddPhoto(string title, string summary, Stream photoStream)
        {
            string boundary = "BOUNDARY--" + DateTime.Now.Ticks.ToString("x");

            string metadata;
            {
                var xmlMetadata = new XElement(nsAtom + "entry",
                    new XElement(nsAtom + "title", title),
                    new XElement(nsAtom + "summary", summary),
                    new XElement(nsAtom + "category",
                        new XAttribute("scheme", "http://schemas.google.com/g/2005#kind"),
                        new XAttribute("term", "http://schemas.google.com/photos/2007#photo")
                    )
                );
                metadata = xmlMetadata.ToString();
            }

            byte[] metadataBytes = System.Text.Encoding.UTF8.GetBytes(
                String.Concat("\r\n--", boundary, "\r\nContent-Type: application/atom+xml\r\n\r\n", metadata));
            byte[] fileHeaderBytes = System.Text.Encoding.UTF8.GetBytes(
                String.Concat("\r\n--", boundary, "\r\nContent-Type: image/jpeg\r\n\r\n"));
            byte[] footerBytes = System.Text.Encoding.ASCII.GetBytes(string.Concat("\r\n--", boundary, "--\r\n"));

            string url = string.Concat(WebAlbumUtility.c_PicasawebEndpoint, "/user/", m_userId, "/album/", m_albumId, "?imgmax=d");
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "multipart/related; boundary=" + boundary;
            request.Method = "POST";
            request.Headers.Add(WebAlbumUtility.c_VersionHeader, WebAlbumUtility.c_VersionValue);
            request.Headers.Add(string.Concat("Authorization: Bearer ", m_accessToken));
            request.ContentLength = metadataBytes.Length + fileHeaderBytes.Length + photoStream.Length + footerBytes.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(metadataBytes, 0, metadataBytes.Length);
                stream.Write(fileHeaderBytes, 0, fileHeaderBytes.Length);
                photoStream.Position = 0;
                photoStream.CopyTo(stream);
                stream.Write(footerBytes, 0, footerBytes.Length);
            }

            var xml = WebAlbumUtility.HttpGetXml(request);
            //WebAlbumUtility.DumpXml(xml, Console.Out);

            return new WebPhoto(m_accessToken, m_userId, m_albumId, xml);
        }

        #endregion

    } // Class WebAlbum

    class WebPhoto
    {
        static XNamespace nsAtom = "http://www.w3.org/2005/Atom";
        static XNamespace nsGphoto = "http://schemas.google.com/photos/2007";
        static XNamespace nsMedia = "http://search.yahoo.com/mrss/";

        string m_accessToken;
        string m_title;
        string m_summary;
        string m_userId;
        string m_albumId;
        string m_photoId;
        string m_alternate;
        XElement m_xml;

        public WebPhoto(string accessToken, string userId, string albumId, XElement xml)
        {
            m_accessToken = accessToken;
            m_userId = userId;
            m_albumId = albumId;
            m_photoId = xml.Element(nsGphoto + "id").Value;
            m_title = xml.Element(nsAtom + "title").Value;
            m_summary = xml.Element(nsAtom + "summary").Value;
            m_xml = xml;

            m_alternate =
                (from el in m_xml.Elements(nsAtom + "link")
                 where el.Attribute("rel").Value == "alternate"
                 select el).First().Attribute("href").Value;

        }

        static int[] s_imgmaxValues = new int[] { 32, 48, 64, 72, 94, 110, 104, 128, 144, 150, 160, 200, 220, 288, 320, 400, 512, 576, 640, 720, 800, 912, 1024, 1152, 1280, 1440, 1600 };

        /// <summary>
        /// Refreshes the photo by re-retrieving the data from the API
        /// </summary>
        /// <param name="imgmax">The maximum image size for the Image URL or 0 for native size (see remarks).</param>
        /// <remarks>When retrieving photo information you can specify the maximum size for the image that the URL references.
        /// Google supports the following sizes. The value will be rounded up to the next size.
        /// Values are in terms of horizontal pixels.
        /// 32, 48, 64, 72, 94, 110, 104, 128, 144, 150, 160, 200, 220, 288, 320, 400, 512, 576, 640, 720, 800, 912, 1024, 1152, 1280, 1440, 1600
        /// </remarks>
        public void Refresh(int imgmax = 0)
        {
            // Convert imgmax into a value acceptable to Google
            string imgmaxStr = null;
            if (imgmax == 0)
            {
                imgmaxStr = "d";
            }
            else
            {
                foreach (int val in s_imgmaxValues)
                {
                    if (val >= imgmax)
                    {
                        imgmaxStr = val.ToString() + "u";   // Always uncropped
                    }
                    if (imgmaxStr == null)
                    {
                        imgmaxStr = "d";
                    }
                }
            }

            m_xml = WebAlbumUtility.HttpGetXml(string.Concat(WebAlbumUtility.c_PicasawebEndpoint, "/user/", m_userId, "/album/", m_albumId, "/photoid/", m_photoId, "?imgmax=", imgmaxStr), m_accessToken);
            //WebAlbumUtility.DumpXml(m_xml, Console.Out);
        }

        public string Title
        {
            get { return m_title; }
        }

        public string Summary
        {
            get { return m_summary; }
        }

        public string JpegUrl
        {
            get
            {
                return m_xml.Element(nsMedia + "group").Element(nsMedia + "content").Attribute("url").Value;
            }
        }

        public int Width
        {
            get
            {
                return int.Parse(m_xml.Element(nsMedia + "group").Element(nsMedia + "content").Attribute("width").Value);

            }
        }

        public int Height
        {
            get
            {
                return int.Parse(m_xml.Element(nsMedia + "group").Element(nsMedia + "content").Attribute("height").Value);

            }
        }

        public string AlternateUrl
        {
            get
            {
                return m_alternate;
            }
        }

    }

} // Namespace Google
