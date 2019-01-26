using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.IO;
using System.Globalization;

namespace Google
{
    class Album
    {
        public const string OAuthScope = "https://www.googleapis.com/auth/photoslibrary https://www.googleapis.com/auth/photoslibrary.sharing";

        const string c_photosEndPoint = "https://photoslibrary.googleapis.com/v1";

        public static Album GetByTitle(string accessToken, string title)
        {
            var album = InternalGetByTitle(accessToken, title, false);
            if (album != null) return album;
            return InternalGetByTitle(accessToken, title, true);
        }

        static Album InternalGetByTitle(string accessToken, string title, bool shared)
        {
            string nextPageToken = null;
            do
            {
                string albumKey = shared ? "sharedAlbums" : "albums";
                var url = string.Concat(c_photosEndPoint, "/", albumKey, "?fields=nextPageToken,", albumKey, "(id,title)");
#if DEBUG && false
                url = string.Concat(url, "&pageSize=3");
#endif
                if (!string.IsNullOrEmpty(nextPageToken))
                {
                    url = string.Concat(url, "&pageToken=", nextPageToken);
                }

                var doc = ApiUtility.HttpGetJson(url, accessToken);

                var albums = doc.Element(albumKey);
                if (albums != null)
                {
                    var matches = 
                        from el in albums.Elements("item")
                        where string.Equals(title, el.Element("title")?.Value)
                        select el;

                    var match = matches.FirstOrDefault();
                    if (match != null)
                    {
                        return new Album(accessToken, match);
                    }
                }

                nextPageToken = doc.Element("nextPageToken")?.Value;
            } while (!string.IsNullOrEmpty(nextPageToken));

            return null;
        }

        public static Album Create(string accessToken, string title, bool shared)
        {
            var url = string.Concat(c_photosEndPoint, "/albums");
            var post = string.Concat("{\"album\":{\"title\":\"", ApiUtility.JsonEncode(title), "\"}}");
            var albumEle = ApiUtility.HttpPostJson(url, post, accessToken);

            var id = albumEle.Element("id")?.Value;
            if (id == null)
            {
                throw new ApplicationException("Unknown failure creating album.");
            }

            if (shared)
            {
                url = string.Concat(c_photosEndPoint, "/albums/", id, ":share");
                post = "{\"sharedAlbumOptions\":{\"isCollaborative\":\"false\",\"isCommentable\":\"false\"}}";
                var doc = ApiUtility.HttpPostJson(url, post, accessToken);
                ApiUtility.DumpXml(doc, Console.Out);
            }

            return new Album(accessToken, albumEle);
        }

        string m_accessToken;
        string m_id;
        XElement m_catalog;

        private Album(string accessToken, XElement albumElement)
        {
            m_accessToken = accessToken;
            Title = albumElement.Element("title")?.Value;
            if (string.IsNullOrEmpty(Title))
            {
                throw new ApplicationException("Google Photos - Album Missing Title.");
            }
            m_id = albumElement.Element("id")?.Value;
            if (string.IsNullOrEmpty("id"))
            {
                throw new ApplicationException("Google Photos - Album Missing ID.");
            }
        }

        public string Title { get; private set; }

        public Photo GetPhotoByFilename(string filename)
        {
            if (m_catalog == null) Refresh();

            var matches =
                from el in m_catalog.Elements("item")
                where matchFilename(filename, el.Element("filename"))
                select el;

            var match = matches.FirstOrDefault();
            return (match == null) ? null : new Photo(m_accessToken, match);
        }

        public Photo GetMatchingPhoto(string filename = null, string description = null, int width = 0, int height = 0, DateTime? dateTaken = null)
        {
            if (m_catalog == null) Refresh();
            var matches =
                from el in m_catalog.Elements("item")
                where (filename == null || matchFilename(filename, el.Element("filename")))
                    && (description == null || matchString(description, el.Element("description")))
                    && (width == 0 || matchInt(width, el.Element("mediaMetadata")?.Element("width")))
                    && (height == 0 || matchInt(height, el.Element("mediaMetadata")?.Element("height")))
                    && (!dateTaken.HasValue || matchDate(dateTaken.Value, el.Element("mediaMetadata")?.Element("creationTime")))
                select el;

            var match = matches.FirstOrDefault();
            return (match == null) ? null : new Photo(m_accessToken, match);
        }

        public Photo AddPhoto(string title, string summary, string photoStream)
        {
            throw new NotImplementedException();
        }

        private void Refresh()
        {
            XElement catalog = new XElement("mediaItems");

            string nextPageToken = null;
            do
            {
                var url = string.Concat(c_photosEndPoint, "/mediaItems:search");

                // Compose the post
                string post;
                {
                    var sb = new StringBuilder();
                    sb.Append("{\"albumId\":\"");
                    sb.Append(m_id);
                    sb.Append('"');
                    //sb.Append("\",fields\":\"*\"");
#if DEBUG && false
                    sb.Append(",\"pageSize\":\"3\"");
#else
                    sb.Append(",\"pageSize\":\"100\"");
#endif

                    if (nextPageToken != null)
                    {
                        sb.Append(",\"pageToken\":\"");
                        sb.Append(nextPageToken);
                        sb.Append('"');
                    }
                    sb.Append('}');
                    post = sb.ToString();
                }

                var doc = ApiUtility.HttpPostJson(url, post, m_accessToken);

                var mediaItems = doc.Element("mediaItems");
                if (mediaItems != null)
                {
                    // Copy into list first because Remove messes up iteration.
                    var list = new List<XElement>(mediaItems.Elements("item"));
                    foreach(var item in list)
                    {
                        item.Remove();
                        catalog.Add(item);
                    }
                }

                nextPageToken = doc.Element("nextPageToken")?.Value;
            } while (!string.IsNullOrEmpty(nextPageToken));

            ApiUtility.DumpXml(catalog, Console.Out);
            Console.WriteLine($"Found {catalog.Elements("item").Count()} photos in album.");

            m_catalog = catalog;
        }

        #region Matching Tools

        static bool matchString(string key, XElement ele)
        {
            string match = ele?.Value;
            return string.Equals(key, match, StringComparison.OrdinalIgnoreCase);
        }

        static bool matchFilename(string key, XElement ele)
        {
            string match = ele?.Value;
            if (string.IsNullOrEmpty(match)) return false;
            return string.Equals(Path.GetFileNameWithoutExtension(key), Path.GetFileNameWithoutExtension(match), StringComparison.OrdinalIgnoreCase);
        }

        static bool matchInt(int key, XElement ele)
        {
            string match = ele?.Value;
            if (string.IsNullOrEmpty(match)) return false;
            int intMatch;
            if (!int.TryParse(match, out intMatch)) return false;
            return key == intMatch;
        }

        const long c_tickSecond = 10000000L;
        const long c_tickMinute = c_tickSecond * 60L;
        const long c_tickHour = c_tickMinute * 60L;
        const long c_tickDay = c_tickHour * 24L;

        static bool matchDate(DateTime key, XElement ele)
        {
            // Due to timezone issues, we consider it a match if the values are within 24 hours and the minutes and seconds match.
            string match = ele?.Value;
            if (string.IsNullOrEmpty(match)) return false;
            DateTime dtMatch;
            if (!DateTime.TryParse(match,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.AllowWhiteSpaces,
                out dtMatch)) return false;

            var diff = Math.Abs(key.Ticks - dtMatch.Ticks);
            return diff < c_tickDay && (diff % c_tickHour) < c_tickSecond;
        }

        #endregion Matching Tools
    }

    class Photo
    {

        internal Photo(string accessToken, XElement photoElement)
        {

        }

    }
}
