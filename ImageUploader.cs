using System;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace FmPostToBlogger
{
    /// <summary>
    /// Class to manage uploading an image from local storage to Google Drive
    /// Facilitates matching duplicates, transferring metadata, and resizing
    /// This is a console class and writes error messages directly to the
    /// console before returning.
    /// </summary>
    class ImageUploader
    {
        // Property keys retrieved from https://msdn.microsoft.com/en-us/library/windows/desktop/dd561977(v=vs.85).aspx
        static Interop.PropertyKey s_pkTitle = new Interop.PropertyKey("F29F85E0-4FF9-1068-AB91-08002B27B3D9", 2); // System.Title
        static Interop.PropertyKey s_pkComment = new Interop.PropertyKey("F29F85E0-4FF9-1068-AB91-08002B27B3D9", 6); // System.Comment
        static Interop.PropertyKey s_pkKeywords = new Interop.PropertyKey("F29F85E0-4FF9-1068-AB91-08002B27B3D9", 5); // System.Keywords
        static Interop.PropertyKey s_pkWidth = new Interop.PropertyKey("6444048F-4C8B-11D1-8B70-080036B11A03", 3);
        static Interop.PropertyKey s_pkHeight = new Interop.PropertyKey("6444048F-4C8B-11D1-8B70-080036B11A03", 4);
        static Interop.PropertyKey s_pkDateTaken = new Interop.PropertyKey("14B81DA1-0135-4D31-96D9-6CBFC9671A99", 36867); // System.Photo.DateTaken
        static Interop.PropertyKey s_pkLatitude = new Interop.PropertyKey("8727CFFF-4868-4EC6-AD5B-81B98521D1AB", 100); // System.GPS.Latitude
        static Interop.PropertyKey s_pkLatitudeRef = new Interop.PropertyKey("029C0252-5B86-46C7-ACA0-2769FFC8E3D4", 100); // System.GPS.LatitudeRef
        static Interop.PropertyKey s_pkLongitude = new Interop.PropertyKey("C4C4DBB2-B593-466B-BBDA-D03D27D5E43A", 100); // System.GPS.Longitude
        static Interop.PropertyKey s_pkLongitudeRef = new Interop.PropertyKey("33DCF22B-28D5-464C-8035-1EE9EFD25278", 100); // System.GPS.LongitudeRef
        static Interop.PropertyKey s_pkOrientation = new Interop.PropertyKey("14B81DA1-0135-4D31-96D9-6CBFC9671A99", 274);

        Google.DriveFolder m_folder; // The folder to which the photo will be loaded

        // Width and height in web browser pixels of the space in which the image will be rendered.
        // With high-resolution displays, the image will be converted to approximately double this
        // resolution. If either value is zero then the other value will be governing. If both
        // values are zero then the image will be rendered at original resolution.
        int m_targetWidth = 0;
        int m_targetHeight = 0;

        // The width and height of the original image. If IncludeOriginalResolution
        // is set then an image of this resolution will also be uploaded.
        int m_originalWidth;
        int m_originalHeight;

        // The calculated width and height of the image in web pixels. These maintain
        // the aspect ratio of the original image. For high-resolution images they are
        // 1/2 the resolution of the operational image.
        int m_webWidth;
        int m_webHeight;

        // The width and height of the operational uploaded image in pixels. These maintain
        // the aspect ratio of the original image. If the original image is high resolution
        // these dimensions are reduced in order to save bandwidth. Under optimal circumstances
        // they are double the values of webWidth and webHeight.
        int m_opWidth;
        int m_opHeight;

        bool m_includeOriginalResolution = true;
        bool m_foundExisting = false;

        string m_localFilePath; // The local path of the photo to be uploaded

        // Metadata from the original image file
        string m_title;
        string m_comment;
        DateTime? m_dateTaken; // Null if no dateTaken
        double m_latitude; // Zero if not present
        double m_longitude; // Zero if not present
        string[] m_labels;
        int m_orientation;

        // References to uploaded files
        Google.DriveFile m_opFile;
        Google.DriveFile m_originalFile;


        // Error from load
        string m_errMsg;

        public ImageUploader(Google.DriveFolder folder)
        {
            m_folder = folder;
        }

        #region Metadata and other Properties

        /// <summary>
        /// TargetWidth is the width in web pixels of the space into which the
        /// image will be rendered.
        /// </summary>
        /// <remarks>
        /// <para>If the value is zero, there is no limit on the width.
        /// </para>
        /// <para>The actual image will be approximately double this resolution
        /// to perform well on high-resolution displays.
        /// </para>
        /// </remarks>
        public int TargetWidth { get => m_targetWidth; set => m_targetWidth = value; }

        /// <summary>
        /// TargetHeight is the height in web pixels of the space into which the
        /// image will be rendered.
        /// </summary>
        /// <remarks>
        /// <para>If the value is zero, there is no limit on the width.
        /// </para>
        /// <para>The actual image will be approximately double this resolution
        /// to perform well on high-resolution displays.
        /// </para>
        /// </remarks>
        public int TargetHeight { get => m_targetHeight; set => m_targetHeight = value; }

        /// <summary>
        /// If true, the image will be uploaded both at original and at target resolutions. If
        /// false, the image is only uploaded at target resolution.
        /// </summary>
        public bool IncludeOriginalResolution { get => m_includeOriginalResolution; set => m_includeOriginalResolution = value; }

        /// <summary>
        /// If true, existing file or files were found and no upload was necessary.
        /// </summary>
        public bool FoundExisting => m_foundExisting;

        /// <summary>
        /// The width and height of the original image.
        /// </summary>
        public int OriginalWidth { get { return m_originalWidth; } }
        public int OriginalHeight { get { return m_originalHeight; } }

        /// <summary>
        /// The width and height of the image in Web Browser pixels.
        /// </summary>
        public int WebWidth { get => m_webWidth; }
        public int WebHeight { get => m_webHeight; }

        /// <summary>
        /// The width and height of the operational, uploaded image. For images
        /// of sufficient resolution these will be approximately double <see cref="WebWidth"/>
        /// and <see cref="WebHeight"/>.
        /// </summary>
        public int OpWidth { get => m_opWidth; }
        public int OpHeight { get => m_opHeight; }

        public string Title { get { return m_title; } }
        public string Comment { get { return m_comment; } }
        public DateTime? DateTaken { get { return m_dateTaken; } }
        public double Latitude { get { return m_latitude; } }
        public double Longitude { get { return m_longitude; } }
        public string[] Labels { get { return m_labels; } }

        public string OpUrl => m_opFile.RawUrl;
        public string OriginalUrl => m_originalFile?.RawUrl;

        public string ErrorMessage { get { return m_errMsg; } }

        #endregion

        /// <summary>
        /// Load the photo and retrieve metadata
        /// </summary>
        /// <param name="localFilePath">The file path on the local machine or network.</param>
        /// <returns>True if the file was found, and is JPEG.</returns>
        /// <remarks>Upon a false return, error details are in the <see cref="ErrorMessage"/> property. </remarks>
        public bool Load(string localFilePath)
        {
            // Init everything
            m_title = null;
            m_comment = null;
            m_dateTaken = null;
            m_latitude = 0.0;
            m_longitude = 0.0;
            m_orientation = 1;
            m_labels = null;
            m_errMsg = null;
            m_originalWidth = 0;
            m_originalHeight = 0;
            m_webWidth = 0;
            m_webHeight = 0;
            m_opWidth = 0;
            m_opHeight = 0;

            {
                string extension = Path.GetExtension(localFilePath);
                if (!extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    m_errMsg = string.Format("Image to post must be a JPEG file. '{0}' is not.", Path.GetFileName(localFilePath));
                    return false;
                }
            }

            if (!File.Exists(localFilePath))
            {
                m_errMsg = string.Format("Photo file does not exist: " + localFilePath);
                return false;
            }

            // Use the Windows Property Store to read the title and comments from the image
            try
            {
                using (var propStore = WinShell.PropertyStore.Open(localFilePath))
                {
                    m_title = propStore.GetValue(s_pkTitle) as string;
                    m_comment = propStore.GetValue(s_pkComment) as string;
                    m_dateTaken = propStore.GetValue(s_pkDateTaken) as DateTime?;
                    m_originalWidth = (int)(UInt32)propStore.GetValue(s_pkWidth);
                    m_originalHeight = (int)(UInt32)propStore.GetValue(s_pkHeight);
                    m_latitude = GetLatOrLong(propStore, true);
                    m_longitude = GetLatOrLong(propStore, false);
                    m_labels = propStore.GetValue(s_pkKeywords) as string[];
                    m_orientation = (int)(ushort)(propStore.GetValue(s_pkOrientation) ?? ((ushort)1));
                }
            }
            catch (Exception err)
            {
                m_errMsg = string.Format("Failed to read metadata from photo: " + err.Message);
                return false;
            }

            m_localFilePath = localFilePath;
            if (m_title == null) m_title = string.Empty;
            if (m_comment == null) m_comment = string.Empty;

            // =========================
            // Calculate the height and width of the space into which the image must fit on the page
            // Note that when orientation is 90 degrees or 270 degrees, the Windows Property System
            // returns the dimensions of the rotated image. So width and height DO NOT have to be swapped.

            int windowWidth = (m_targetWidth > 0) ? m_targetWidth : int.MaxValue;
            int windowHeight = (m_targetHeight > 0) ? m_targetHeight : int.MaxValue;

            // If both are maxed out this is an error
            if (windowWidth == int.MaxValue && windowHeight == int.MaxValue)
            {
                throw new InvalidOperationException("TargetWidth and/or TargetHeight must be specified.");
            }

            // =========================
            // Calculate the height and width of the image on the page that matches
            // the original aspect ratio and fits within the maximum parameters.

            // First, assume that width will be the governing factor
            if (m_originalWidth <= windowWidth)
            {
                m_webWidth = m_originalWidth;
                m_webHeight = m_originalHeight;
            }
            else
            {
                m_webWidth = windowWidth;
                m_webHeight = (windowWidth * m_originalHeight) / m_originalWidth; // Scale height to match width
            }

            // If using width-dominance is too tall then use height dominance
            if (m_webHeight > windowHeight)
            {
                Debug.Assert(m_originalHeight > windowHeight);
                m_webHeight = windowHeight;
                m_webWidth = (windowHeight * m_originalWidth) / m_originalHeight; // Scale width to match height
            }

            // =========================
            // Calculate the height and width of the resized image to be uploaded
            // Ideally, it will be double the web height and width.
            if (m_originalWidth > m_webWidth * 2)
            {
                m_opWidth = m_webWidth * 2;
                m_opHeight = m_webHeight * 2;
            }
            else
            {
                m_opWidth = m_originalWidth;
                m_opHeight = m_originalHeight;
            }

            return true;
        }

        public void Upload()
        {
            m_foundExisting = true;

            // Look for existing operational file
            string opFilename = DeriveFilename(m_opWidth, m_opHeight);
            m_opFile = m_folder.GetFile(opFilename);

            // File not found, so upload
            if (m_opFile == null)
            {
                m_foundExisting = false;

                Stream uploadImage = null;
                {
                    // Open the file
                    var srcImage = new FileStream(m_localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    // If it needs to be resized, do so
                    if (m_opWidth != m_originalWidth || m_opHeight != m_originalHeight)
                    {
                        using (srcImage)
                        {
                            uploadImage = new MemoryStream();
                            ImageFile.ResizeAndRightImage(srcImage, uploadImage, m_opWidth, m_opHeight);
                            uploadImage.Position = 0L;
                        }
                    }
                    // If it needs to be righted, do so
                    else if (m_orientation != 1)
                    {
                        using (srcImage)
                        {
                            uploadImage = new MemoryStream();
                            ImageFile.RightImage(srcImage, uploadImage);
                            uploadImage.Position = 0L;
                        }
                    }
                    else
                    {
                        uploadImage = srcImage;
                        srcImage = null;
                    }
                }

                using (uploadImage)
                {
                    m_opFile = m_folder.Upload(uploadImage, opFilename);
                }
            }

            if (!m_includeOriginalResolution) return;

            // Look for existing original file
            string originalFilename = DeriveFilename(m_originalWidth, m_originalHeight);
            m_originalFile = m_folder.GetFile(originalFilename);

            if (m_originalFile == null)
            {
                m_foundExisting = false;

                Stream uploadImage = null;
                {
                    // Open the file
                    var srcImage = new FileStream(m_localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    // If it needs to be righted, do so
                    if (m_orientation != 1)
                    {
                        using (srcImage)
                        {
                            uploadImage = new MemoryStream();
                            ImageFile.RightImage(srcImage, uploadImage);
                            uploadImage.Position = 0L;
                        }
                    }
                    else
                    {
                        uploadImage = srcImage;
                        srcImage = null;
                    }
                }

                using (uploadImage)
                {
                    m_originalFile = m_folder.Upload(uploadImage, originalFilename);
                }
            }
        }

        private string DeriveFilename(int width, int height)
        {
            var sb = new StringBuilder();

            sb.Append(Path.GetFileNameWithoutExtension(m_localFilePath));

            // Append DateTaken if present
            if (m_dateTaken.HasValue)
            {
                sb.Append('_');
                sb.Append(m_dateTaken.Value.ToString("yyyy-MM-dd"));
            }

            // Append dimensions
            sb.Append($"_{width}x{height}");

            // Append extension
            sb.Append(Path.GetExtension(m_localFilePath));

            return sb.ToString();
        }

        private static double GetLatOrLong(WinShell.PropertyStore store, bool getLatitude)
        {
            // Get the property keys
            Interop.PropertyKey pkValue;
            Interop.PropertyKey pkDirection;
            if (getLatitude)
            {
                pkValue = s_pkLatitude;
                pkDirection = s_pkLatitudeRef;
            }
            else
            {
                pkValue = s_pkLongitude;
                pkDirection = s_pkLongitudeRef;
            }

            // Retrieve the values
            double[] angle = (double[])store.GetValue(pkValue);
            string direction = (string)store.GetValue(pkDirection);
            if (angle == null || angle.Length == 0 || direction == null) return 0.0;

            // Convert to double
            double value = angle[0];
            if (angle.Length > 1) value += angle[1] / 60.0;
            if (angle.Length > 2) value += angle[2] / 3600.0;

            if (direction.Equals("W", StringComparison.OrdinalIgnoreCase) || direction.Equals("S", StringComparison.OrdinalIgnoreCase))
            {
                value = -value;
            }

            return value;
        }
    }

}
