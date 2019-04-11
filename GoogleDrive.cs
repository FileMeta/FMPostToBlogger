﻿using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Google
{
    class DriveFolder
    {
        #region Constants

        public const string OAuthScope = "https://www.googleapis.com/auth/drive.file";

        const string c_DriveEndpoint = "https://www.googleapis.com/drive/v3";

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

        public string Path => m_path;

        public DriveFile GetFile(string filename)
        {
            string id = GetId(m_accessToken, m_id, filename, false);
            if (id == null) return null;
            return new DriveFile(m_accessToken, id);
        }

        public DriveFile Upload(string filename, string localFilename)
        {
            throw new NotImplementedException();
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

        public DriveFile(string accessToken, string id)
        {

        }

    }
}