using System;
using System.IO;
using System.Net;
using System.Text;

// Google Drive API Reference: https://developers.google.com/drive/api/v3/reference/

namespace Google
{
    class DriveFolder
    {
        #region Constants

        public const string OAuthScope = "https://www.googleapis.com/auth/drive.file";

        const string c_DriveEndpoint = "https://www.googleapis.com/drive/v3";
        const string c_DriveUploadEndpoint = "https://www.googleapis.com/upload/drive/v3/files";

        #endregion Constants

        #region Creation

        public static DriveFolder OpenPublic(string accessToken, string folderPath, bool allowCreate = true)
        {
            string[] parts = folderPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string id = "root";

            bool created = false;
            foreach (var part in parts)
            {
                string nextId = null;
                if (!created)
                {
                    nextId = GetId(accessToken, id, part, true);
                }

                if (nextId == null)
                {
                    nextId = CreateFolder(accessToken, id, part);
                    created = true;
                }

                id = nextId;
            }

            if (created)
            {
                GrantPublicReadAccess(accessToken, id);
            }

            return new DriveFolder(accessToken, id, folderPath);
        }

        string m_accessToken;
        string m_id;
        string m_path;

        private DriveFolder(string accessToken, string id, string path)
        {
            m_accessToken = accessToken;
            m_id = id;
            m_path = path;
        }

        #endregion

        #region Public Operations

        public string FolderPath => m_path;

        public DriveFile GetFile(string filename)
        {
            string id = GetId(m_accessToken, m_id, filename, false);
            if (id == null) return null;
            return new DriveFile(m_accessToken, id);
        }

        // Truly random number (from Random.org) likely to never occur again.
        const string c_multipartBoundary = "4b246b371e4e605324156d64";
        static readonly byte[] s_boundaryBytes = ApiUtility.Utf8NoBom.GetBytes(c_multipartBoundary);
        static readonly byte[] s_dashesBytes = new byte[] { (byte)'-', (byte)'-' };
        static readonly byte[] s_crlfBytes = new byte[] { 13, 10 };

        public DriveFile Upload(Stream uploadStream, string name)
        {
            string url = c_DriveUploadEndpoint + "?uploadtype=multipart";

            string postJsonPart = string.Concat(
                "Content-Type: application/json; charset=UTF-8\r\n\r\n",
                "{\"name\": \"", ApiUtility.JsonEncode(name), "\",",
                "\"parents\": [\"", ApiUtility.JsonEncode(m_id), "\"]}"
                );
            var postJsonBytes = ApiUtility.Utf8NoBom.GetBytes(postJsonPart);

            string contentType;
            string ext = Path.GetExtension(name).ToLower();
            if (string.IsNullOrEmpty(ext)) throw new ArgumentException("Image filename lacks extension.");
            ext = ext.Substring(1);
            switch (ext)
            {
                case "jpg":
                    contentType = "image/jpeg";
                    break;

                case "tif":
                    contentType = "image/tiff";
                    break;

                case "htm":
                case "html":
                    contentType = "text/html";
                    break;

                case "xml":
                    contentType = "text/xml";
                    break;

                case "json":
                    contentType = "application/json";
                    break;

                default:
                    contentType = "image/" + ext;   // Works for a majority of image types
                    break;
            }

            var contentTypeBytes = ApiUtility.Utf8NoBom.GetBytes(
                string.Concat("Content-Type: ", contentType, "\r\n\r\n"));

            // Calculate the content length
            long contentLength = 90 + postJsonBytes.Length + contentTypeBytes.Length + uploadStream.Length;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "multipart/related; boundary=" + c_multipartBoundary;
            request.Method = "POST";
            request.Headers.Add(string.Concat("Authorization: Bearer ", m_accessToken));
            request.ContentLength = contentLength;

            // Send the body
            using (var stream = request.GetRequestStream())
            {
                // Write the body (comments indicate the number of bytes - used for the content-length calculation
                stream.Write(s_dashesBytes, 0, 2);                      // 2
                stream.Write(s_boundaryBytes, 0, 24);                   // 24
                stream.Write(s_crlfBytes, 0, 2);                        // 2
                stream.Write(postJsonBytes, 0, postJsonBytes.Length);
                stream.Write(s_crlfBytes, 0, 2);                        // 2
                stream.Write(s_dashesBytes, 0, 2);                      // 2
                stream.Write(s_boundaryBytes, 0, 24);                   // 24
                stream.Write(s_crlfBytes, 0, 2);                        // 2
                stream.Write(contentTypeBytes, 0, contentTypeBytes.Length);
                uploadStream.CopyTo(stream);
                stream.Write(s_crlfBytes, 0, 2);                        // 2
                stream.Write(s_dashesBytes, 0, 2);                      // 2
                stream.Write(s_boundaryBytes, 0, 24);                   // 24
                stream.Write(s_dashesBytes, 0, 2);                      // 2
                stream.Write(s_crlfBytes, 0, 2);                        // 2
            }

            var doc = ApiUtility.HttpGetJson(request);
            ApiUtility.DumpXml(doc, Console.Out);

            return new DriveFile(m_accessToken, doc.Element("id").Value);
        }

        #endregion

        #region Internal Operations

        private static string GetId(string accessToken, string parentId, string name, bool isFolder)
        {
            string query = $"mimeType{(isFolder ? "=" : "!=")}'application/vnd.google-apps.folder' and '{parentId.Replace("'", "\\'")}' in parents and name='{name.Replace("'", "\\'")}'";

            string url = string.Concat(
                c_DriveEndpoint, "/files?",
                "corpora=user&q=",
                Uri.EscapeDataString(query));

            var doc = ApiUtility.HttpGetJson(url, accessToken);

            // ApiUtility.DumpXml(doc, Console.Out);

            return doc.Element("files")?.Element("item")?.Element("id")?.Value;
        }

        private static string CreateFolder(string accessToken, string parentId, string name)
        {
            string url = string.Concat(c_DriveEndpoint, "/files");

            string postJson = string.Concat(
                "{\"mimeType\": \"application/vnd.google-apps.folder\",",
                "\"name\": \"", ApiUtility.JsonEncode(name), "\",",
                "\"parents\": [\"", ApiUtility.JsonEncode(parentId), "\"]}"
                );

            var doc = ApiUtility.HttpPostJson(url, postJson, accessToken);

            // ApiUtility.DumpXml(doc, Console.Out);

            string id = doc.Element("id")?.Value;
            if (id == null) throw new ApplicationException($"Google.Drive: Failed to create folder '{name}'");

            return id;
        }

        private static void GrantPublicReadAccess(string accessToken, string id)
        {
            string url = string.Concat(c_DriveEndpoint, $"/files/{Uri.EscapeDataString(id)}/permissions");
            Console.WriteLine(url);

            string postJson = "{\"role\":\"reader\",\"type\":\"anyone\"}";
            Console.WriteLine(postJson);

            var doc = ApiUtility.HttpPostJson(url, postJson, accessToken);

            ApiUtility.DumpXml(doc, Console.Out);
        }

        #endregion Internal Operations

    }

    class DriveFile
    {
        string m_id;

        public DriveFile(string accessToken, string id)
        {

        }

        public string RawUrl => "https://drive.google.com/uc?export=view&id=" + m_id;
    }
}
