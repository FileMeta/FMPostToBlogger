using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Collections;
using System.Diagnostics;
using System.Globalization;

/* TODO:
       * size (but with default)

    To set up
    * Get baseline css right
    * Prep blog, domain, etc.
*/


namespace FmPostToBlogger
{
    class Program
    {
// Column 78                                                                 |
        const string c_syntax =
@"Syntax: FMPostToBlogger: <options> <filenames>
Options:
   -h                      Print this help text.
   -authint                Launch a browser to log in to your Google account
                           interactively.
   -authtoken <token>      Specify a refresh token from a previous login to
                           with which to authenticate. When you log in
                           interactively the applicaiton will print a refresh
                           token that you can use for future postings.
   -blog <name>            Name of a blog on which you have posting
                           privileges.
   -maxwidth               Maximum width and height in web pixels to use on
   -maxheight              images. Default width used with MarkDown posts.
                           Defaults are 800 width and 720 height.
                           (See details below.)
   -linkfullres            Images link to full resolution copies.
   -draft                  Post in draft form. You can log into Blogger and
                           view or edit the post before publishing it.
   -dryrun                 Do a dry run. Connect with the blog, report
                           metadata, and check for conflicts with existing
                           posts but do not actually post to the blog.

Filenames:
   Each file will be converted into a blog post. Multiple file names may be
   included and wildcards are supported. Presently JPEG photos (with a
   .jpg or .jpeg extension) and MarkDown files (with a .md extension) are
   supported. Local .jpeg files referenced by MarkDown are automatically
   uploaded along with the blog post.

Examples:
   FMPostToBlogger -authint -blog ""There and Back Again"" Bilbo.jpg Gandalf.jpg
           This will interactively log into Google (using your web browser) and
           then post two photos, Bilbo.jpg and Gandalf.jpg on the blog titled
           ""There and Back Again"".

   FMPostToBlogger -refresh_token 1/XirnVAK -blog ""Asgard"" Thor.jpg Sif.jpg
           This will use a refresh token (presumably from a previous run) to
           authenticate and then post Thor.jpg and Sif.jpg on the blog titled
           ""Asgard"".

   FMPostToBlogger -refresh_token 1/XirnVAK -blog ""Coding"" GammaCompression.md
           This will use a refresh token to authenticate, upload all .jpeg
           images referenced by GammaCompression.md, the post to HTML and
           upload it to the ""Coding"" blog.

JPEG Post Details:
   Files must include a title in the JPEG metadata. This is is used as the
   title in the blog posting. Other metadata is optional. See below for
   metadata support. One way to view and edit metadata is to use the
   properties-details page in the Microsoft Windows file explorer. Many
   JPEG and photo album tools also support editing metadata.

JPEG Metadata Support:
   Title        Required - Becomes the title of the blog post.
   Comments     Optional - Included after the photo as text in the blog post.
   Date taken   Optional - Used as the posting date of the blog post. The
                           update date of the post will be the date the post
                           is uploaded.
   GPS Location            Often included in phone camera phones, the GPS
                           location will be included in the blog post.
                           Blogger usually includes this in the footer of the
                           post. Clicking on the location will bring up a
                           map of where the photo was taken.
   Tags/Keywords           Will be included as labels on the blog post.

Image Size:
   The -maxwidth and -maxheight parameters limit the image size. Values are
   in pixels as interpreted by the web browser. You should choose values that
   work well with your blog layout. With the advent of high resolution
   displays, browser pixels may be more coarse than actual image pixels.
   Accordingly, FMPostToBlogger will request a link from the Google Web Album
   service that delivers an image approximately double the resolution used.

Description:
   FMPostToBlogger is a command-line utility that makes Google Blogger posts
   out of photos and MarkDown files.

   To use, you must first set up a blog at https://blogger.com
   and make at least one post that includes a photo. Making the post will
   cause blogger to create a corresponding photo album in the google album
   archive (see https://get.google.com/albumarchive). You can delete the
   sample post afterward. FMPostToBlogger locates the blog and the
   corresponding album by name (not ID).

   For details, the latest release, and for open source code see
   http://github.com/FileMeta/FMPostToBlogger
";
// Column 78                                                                 |

        // Even though Google calls it a "Client Secret" it doesn't have to be kept secret.
        const string c_googleClientId = "836437994394-2a38027hpt3ao7fdnvjffkkv2rbqr715.apps.googleusercontent.com";
        const string c_googleClientSecret = "wJUdAD-LM7wc5nNKNipVGkCy";
        public const int c_defaultMaxWidth = 800;
        public const int c_defaultMaxHeight = 720;

        static void Main(string[] args)
        {
            try
            {
                // Parse the command line
                bool showSyntax = false;
                bool authInteractive = false;
                string refreshToken = null;
                string blogName = null;
                int maxWidth = c_defaultMaxWidth;
                int maxHeight = c_defaultMaxHeight;
                List<string> filenames = new List<string>();
                bool linkFullRes = false;
                bool draftMode = false;
                bool dryRun = false;
                if (args.Length == 0)
                {
                    showSyntax = true;
                }
                for (int i = 0; i < args.Length; ++i)
                {
                    switch (args[i].ToLowerInvariant())
                    {
                        case "-h":
                            showSyntax = true;
                            break;

                        case "-authint":
                            authInteractive = true;
                            break;

                        case "-authtoken":
                            ++i;
                            refreshToken = args[i];
                            break;

                        case "-blog":
                            ++i;
                            blogName = args[i];
                            break;

                        case "-maxwidth":
                            ++i;
                            if (!int.TryParse(args[i], out maxWidth))
                            {
                                throw new ArgumentException("Value for -maxwidth is not an integer: " + args[i]);
                            }
                            break;

                        case "-maxheight":
                            ++i;
                            if (!int.TryParse(args[i], out maxHeight))
                            {
                                throw new ArgumentException("Value for -maxheight is not an integer: " + args[i]);
                            }
                            break;

                        case "-linkfullres":
                            linkFullRes = true;
                            break;

                        case "-draft":
                            draftMode = true;
                            break;

                        case "-dryrun":
                            dryRun = true;
                            break;

                        default:
                            if (args[i][0] == '-')
                            {
                                throw new ArgumentException("Unexpected command-line option: " + args[i]);
                            }
                            else
                            {
                                filenames.Add(args[i]);
                            }
                            break;
                    }
                }

                // Not really a loop. Just for convenience of using 'break';
                do
                {

                    if (showSyntax)
                    {
                        Console.Write(c_syntax);
                        break;
                    }

                    // Check authentication method
                    if (authInteractive != (refreshToken == null))
                    {
                        throw new ArgumentException("Must specify one authentication method.\r\nEither '-authint' or '-authtoken' but not both.");
                    }

                    // Authenticate and authorize.
                    var oauth = new Google.OAuth(c_googleClientId, c_googleClientSecret);
                    bool authorized = false;
                    if (authInteractive)
                    {
                        Console.WriteLine("Getting authorization from Google. Please respond to the login prompt in your browser.");
                        authorized = oauth.Authorize(Google.Blog.OAuthScope, Google.DriveFolder.OAuthScope);
                        Win32Interop.ConsoleHelper.BringConsoleToFront();
                    }
                    else if (refreshToken != null)
                    {
                        authorized = oauth.Refresh(refreshToken);
                    }
                    if (!authorized)
                    {
                        throw new ArgumentException("Failed to authenticate with Google: " + oauth.Error);
                    }
                    Console.WriteLine("Authenticated with Google.");
                    Console.WriteLine("refresh_token (-authtoken) = " + oauth.Refresh_Token);
                    Console.WriteLine();

                    if (string.IsNullOrEmpty(blogName))
                    {
                        if (filenames.Count != 0)
                        {
                            throw new ArgumentException("No blog name specified.");
                        }
                        break;
                    }

                    // Open the blog poster and ensure the the blog exists
                    BlogPoster blogPoster = new BlogPoster(oauth.Access_Token);
                    if (!blogPoster.Open(blogName))
                    {
                        break;  // Error message was already reported.
                    }
                    blogPoster.MaxWidth = maxWidth;
                    blogPoster.MaxHeight = maxHeight;
                    blogPoster.LinkFullRes = linkFullRes;
                    blogPoster.DraftMode = draftMode;
                    blogPoster.DryRun = dryRun;

                    int postCount = 0;
                    int errorCount = 0;

                    // Post the files
                    foreach (string filename in new FileNameEnumerable(filenames))
                    {
                        Console.WriteLine(filename);
                        string ext = Path.GetExtension(filename);
                        if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                        {
                            if (blogPoster.PostFromPhoto(filename))
                            {
                                ++postCount; // PostFromPhoto reports success.
                            }
                            else
                            {
                                ++errorCount;
                            }
                        }
                        else if (ext.Equals(".md", StringComparison.OrdinalIgnoreCase))
                        {
                            if (blogPoster.PostFromMarkDown(filename))
                            {
                                ++postCount; // PostFromMarkDown reports success.
                            }
                            else
                            {
                                ++errorCount;
                            }
                        }
                        else
                        {
                            Console.WriteLine("File type '{0}' not supported for posting to blog.", ext);
                            ++errorCount;
                        }
                        Console.WriteLine();
                    }

                    if (postCount == 0 && errorCount == 0)
                    {
                        Console.WriteLine("No files specified to post.");
                    }
                    else
                    {
                        Console.WriteLine("{0} new blog posts.", postCount);
                        if (errorCount > 0)
                        {
                            Console.WriteLine("{0} post errors.", errorCount);
                        }
                    }
                }
                while (false); // Exit task options

            }
            catch (Exception err)
            {
                Console.WriteLine();
#if DEBUG
                Console.WriteLine(err.ToString());
#else
                Console.WriteLine(err.Message);
#endif
                Console.WriteLine("Enter 'FMPostToBlogger -h' for help.");
            }

            if (Win32Interop.ConsoleHelper.IsSoleConsoleOwner)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Press any key to exit.");
                Console.ReadKey(true);
            }
        }

    } // Class FmPostToBlogger

    /// <summary>
    /// Handles identifying the blog and photo album and posting to both.
    /// Associated with command processing, this reports progress and errors
    /// directly to the console.
    /// </summary>
    class BlogPoster
    {
        string m_accessToken;
        Google.Blog m_blog;
        Google.DriveFolder m_photoFolder;

        public BlogPoster(string accessToken)
        {
            m_accessToken = accessToken;
            MaxWidth = Program.c_defaultMaxWidth;
            MaxHeight = Program.c_defaultMaxHeight;
        }

        public int MaxWidth { get; set; }
        public int MaxHeight { get; set; }
        public bool LinkFullRes { get; set; }
        public bool DraftMode { get; set; }
        public bool DryRun { get; set; }

        public bool Open(string blogName)
        {
            // Find the blog
            m_blog = Google.Blog.GetByName(m_accessToken, blogName);
            if (m_blog == null)
            {
                Console.WriteLine("Blog '{0}' not found.", blogName);
                return false;
            }

            // Find or create the corresponding Google Drive folder
            m_photoFolder = Google.DriveFolder.OpenPublic(m_accessToken, "Blogger/" + blogName, true);

            return true;
        }

        public bool PostFromPhoto(string filename)
        {
            PhotoUploader photoUploader = new PhotoUploader(m_photoFolder);

            // Load the photo and metadata
            if (!photoUploader.Load(filename))
            {
                Console.WriteLine(photoUploader.ErrorMessage);
                return false;
            }

            if (string.IsNullOrEmpty(photoUploader.Title))
            {
                Console.WriteLine("JPEG photo must have a title in the metadata to pe posted to the blog.\r\nOne method is to use the 'Properties-Details' function in Windows File Explorer.");
                return false;
            }

            Console.WriteLine("Title: " + photoUploader.Title);

            // See if the post already exists
            var blogPost = m_blog.GetPostByTitle(photoUploader.Title);
            if (blogPost != null)
            {
                Console.WriteLine("Post with title '{0}' already exists in blog '{1}'.", photoUploader.Title, m_blog.Name);
                return false;
            }

            // Find or upload the photo
            if (!DryRun)
            {
                photoUploader.Upload();

                if (photoUploader.FoundExisting)
                {
                    Console.WriteLine("Using matching photo already in album: " + m_photoFolder.FolderPath);
                }
                else
                {
                    Console.WriteLine("Added photo to album: " + m_photoFolder.FolderPath);
                }
            }

            // Fill out the post metadata
            var metadata = new Google.BlogPostMetadata();
            metadata.UpdatedDate = DateTime.Now;
            metadata.PublishedDate = photoUploader.DateTaken ?? metadata.UpdatedDate;
            metadata.Latitude = photoUploader.Latitude;
            metadata.Longitude = photoUploader.Longitude;
            metadata.Labels = photoUploader.Labels;

            if (DryRun)
            {
                Console.WriteLine("Dry run. Not posting.");
                Console.WriteLine("Filename: " + Path.GetFileName(filename));
                Console.WriteLine("Title: " + photoUploader.Title);
                Console.WriteLine("Comment: " + photoUploader.Comment);
                Console.WriteLine("Publish Date: " + metadata.PublishedDate.ToString("s"));
                Console.WriteLine("Photo Dimensions: {0}x{1}", photoUploader.OriginalWidth, photoUploader.OriginalHeight);
                Console.WriteLine("Scaled Dimensions: {0}x{1}", photoUploader.OpWidth, photoUploader.OpHeight);
                Console.WriteLine("Web Pixel Dimensions: {0}x{1}", photoUploader.WebWidth, photoUploader.WebHeight);
                if (photoUploader.Latitude != 0.0)
                {
                    Console.WriteLine("Latitude;Longitude: {0:r};{1:r}", photoUploader.Latitude, photoUploader.Longitude);
                }
                if (photoUploader.Labels != null)
                {
                    Console.WriteLine("Labels: " + string.Join(";", photoUploader.Labels));
                }
                return true;
            }

            // Add the post to the blog
            Console.WriteLine("Adding new post to blog: " + m_blog.Name);

            // Compose the post using XML
            string html;
            {
                // Build the document
                var doc = new XElement("div",
                    BuildImageElement(photoUploader),   
                    new XElement("br"),
                    photoUploader.Comment);
                html = doc.ToString();
            }

            m_blog.AddPost(photoUploader.Title, html, metadata, DraftMode);

            Console.WriteLine(DraftMode ? "Posted as draft!" : "Posted!");

            return true;
        }

        public bool PostFromMarkDown(string filename)
        {
            // Read metadata and convert the markdown to HTML.
            Dictionary<string, string> yamlMetadata;
            string html;
            using (StreamReader reader = new StreamReader(filename, Encoding.UTF8, true))
            {
                // Read YAML metadata prefix if any
                yamlMetadata = MicroYaml.Parse(reader);

                // Read and convert the markdown
                using (var writer = new StringWriter())
                {
                    CommonMark.CommonMarkConverter.Convert(reader, writer);
                    writer.Flush();
                    html = writer.ToString();
                }
            }

            // Process the metadata
            Google.BlogPostMetadata metadata = new Google.BlogPostMetadata();
            string sval;
            string postTitle;
            if (yamlMetadata.TryGetValue("title", out sval))
            {
                postTitle = sval.Trim();
            }
            else
            {
                Console.WriteLine("Markdown post must have title in YAML prefix metadata.");
                return false;
            }
            metadata.UpdatedDate = DateTime.Now;
            if (yamlMetadata.TryGetValue("date", out sval))
            {
                DateTime postDate;
                if (!DateTime.TryParse(sval, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal|DateTimeStyles.NoCurrentDateDefault|DateTimeStyles.AllowWhiteSpaces, out postDate))
                {
                    Console.WriteLine("Invalid date format: ", sval);
                    return false;
                }
                metadata.PublishedDate = postDate;
            }
            else
            {
                metadata.PublishedDate = metadata.UpdatedDate;
            }
            if (yamlMetadata.TryGetValue("labels", out sval))
            {
                var labelList = new List<string>();
                foreach(string label in sval.Split(';'))
                {
                    string l = label.Trim();
                    if (l.Length > 0)
                    {
                        labelList.Add(l);
                    }
                }
                metadata.Labels = labelList.ToArray();
            }
            
            Console.WriteLine("=== Metadata ===");
            Console.WriteLine("title: " + postTitle);
            Console.WriteLine("date: " + metadata.PublishedDate.ToString("s"));
            if (metadata.Labels != null)
            {
                Console.WriteLine("labels: " + String.Join(";", metadata.Labels));
            }

            // See if the post already exists
            var blogPost = m_blog.GetPostByTitle(postTitle);
            if (blogPost != null)
            {
                Console.WriteLine("Post with title '{0}' already exists in blog '{1}'.", postTitle, m_blog.Name);
                return false;
            }

            // Parse the HTML into a DOM
            XDocument doc;
            var settings = new Html.HtmlReaderSettings();
            settings.IgnoreProcessingInstructions = true;
            using (var reader = new Html.HtmlReader(new StringReader(html), settings))
            {
                doc = System.Xml.Linq.XDocument.Load(reader);
            }
            html = null; // free the memory

            // Process all image elements - uploading photos and updating links
            foreach (var imgElement in doc.Descendants("img"))
            {
                UpdateImageNode(imgElement, filename);
            }

            // Convert to HTML string
            using (var stringWriter = new StringWriter())
            {
                XmlWriterSettings writerSettings = new XmlWriterSettings();
                writerSettings.OmitXmlDeclaration = true;
                writerSettings.Indent = false;
                writerSettings.ConformanceLevel = ConformanceLevel.Fragment;
                writerSettings.CloseOutput = true;
                using (XmlWriter writer = XmlWriter.Create(stringWriter, writerSettings))
                {
                    var body = doc.Root.Element("body");
                    if (body != null)
                    {
                        foreach(var ele in body.Elements())
                        {
                            ele.WriteTo(writer);
                        }
                    }

                }
                html = stringWriter.ToString();
            }

            // If dry run, just write the post to the console
            if (DryRun)
            {
                Console.WriteLine("=== DryRun Post ===");
                Console.WriteLine(html);
                return true;
            }

            // Add the post to the blog
            Console.WriteLine("=== Adding new post to blog: {0} ===", m_blog.Name);
            m_blog.AddPost(postTitle, html, metadata, DraftMode);
            Console.WriteLine(DraftMode ? "Posted as draft!" : "Posted!");

            return true;
        }

        private bool UpdateImageNode(XElement imageNode, string docFilename)
        {
            string imgSrc = imageNode.Attribute("src").Value;
            if (string.IsNullOrEmpty(imgSrc))
            {
                Console.WriteLine("Image has no source URL.");
                return false;
            }

            // Get the source and resolve the filename
            Uri docUri = new Uri(docFilename);
            Uri imageUri = new Uri(docUri, imgSrc);

            // If scheme is not file (not a local file) then leave it alone.
            if (imageUri.Scheme != "file") return true;
            string localPath = imageUri.LocalPath;

            Console.WriteLine("=== Image: {0} ===", localPath);

            // Load the image
            PhotoUploader photoUploader = new PhotoUploader(m_photoFolder);
            if (!photoUploader.Load(localPath))
            {
                Console.WriteLine(photoUploader.ErrorMessage);
                return false;
            }

            // Manage height and width. Maximums are the values specified in the image tag
            // if present. Otherwise they are the values passed into the class. Regardless
            // the original aspect ratio is always preserved and image are not blown up
            // beyond their original size.
            photoUploader.TargetWidth = GetIntegerAttribute(imageNode, "width", MaxWidth);
            photoUploader.TargetHeight = GetIntegerAttribute(imageNode, "height", MaxHeight);

            photoUploader.IncludeOriginalResolution = LinkFullRes;

            string opUrl;
            string originalUrl;
            if (DryRun)
            {
                Console.WriteLine("Dry run. Not uploading photo.");
                Console.WriteLine("Filename: " + localPath);
                if (!string.IsNullOrEmpty(photoUploader.Title))
                {
                    Console.WriteLine("Title: " + photoUploader.Title);
                }
                if (!string.IsNullOrEmpty(photoUploader.Comment))
                {
                    Console.WriteLine("Comment: " + photoUploader.Comment);
                }
                Console.WriteLine("Photo Dimensions: {0}x{1}", photoUploader.OriginalWidth, photoUploader.OriginalHeight);
                Console.WriteLine("Scaled Dimensions: {0}x{1}", photoUploader.OpWidth, photoUploader.OpHeight);
                Console.WriteLine("Web Pixel Dimensions: {0}x{1}", photoUploader.WebWidth, photoUploader.WebHeight);
                string name = Path.GetFileName(localPath);
                opUrl = "http://ScaledPhotoUrl.sample/" + name;
                originalUrl = "http://OriginalPhotoUrl.sample/" + name;
            }
            else
            {
                photoUploader.Upload();
                if (photoUploader.FoundExisting)
                {
                    Console.WriteLine("Using matching photo '{0}' already in album '{1}'.", photoUploader.Title, m_photoFolder.FolderPath);
                }
                else
                {
                    Console.WriteLine("Added photo '{0}' to album '{1}'.", Path.GetFileName(localPath), m_photoFolder.FolderPath);
                }

                opUrl = photoUploader.OpUrl;
                originalUrl = photoUploader.OriginalUrl; // Will be null if IncludeOriginalResolution is false;
            }

            // Create a new image element and insert it in place of the imageNode
            imageNode.AddAfterSelf(BuildImageElement(photoUploader));
            imageNode.Remove();

            return true;
        }

        private XElement BuildImageElement(PhotoUploader photoUploader)
        {
            XElement imgEle = new XElement("img",
                new XAttribute("src", photoUploader.OpUrl),
                new XAttribute("width", photoUploader.WebWidth),
                new XAttribute("height", photoUploader.WebHeight));

            // Wrap with a link to the full res version
            if (LinkFullRes)
            {
                imgEle = new XElement("a",
                    new XAttribute("href", photoUploader.OriginalUrl),
                    imgEle);
            }

            return imgEle;
        }

        static int GetIntegerAttribute(XElement imageNode, string attribute, int defaultValue)
        {
            var attrib = imageNode.Attribute(attribute);
            if (attrib == null)
            {
                return defaultValue;
            }
            int value;
            if (int.TryParse(attrib.Value, out value))
            {
                return value;
            }
            throw new ArgumentException($"<{imageNode.Name}> {attribute} attribute '{attrib.Value}' is not an integer. (Do not include units.)");
        }

    }

    /// <summary>
    /// Class to manage uploading a file from the local drive to a Google WebAlbum.
    /// Facilitates matching duplicates and transferring metadata.
    /// This is a console class and writes error messages directly to the
    /// console before returning.
    /// </summary>
    class PhotoUploader
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

        // References to uploaded files
        Google.DriveFile m_opFile;
        Google.DriveFile m_originalFile;


        // Error from load
        string m_errMsg;

        public PhotoUploader(Google.DriveFolder folder)
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

        public string OpUrl => throw new NotImplementedException();
        public string OriginalUrl => throw new NotImplementedException();

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

            int windowWidth = (m_targetWidth > 0) ? m_targetWidth : int.MaxValue;
            int windowHeight = (m_targetHeight > 0) ? m_targetHeight : int.MaxValue;

            // If both are maxed out - reduce to the defaults
            if (windowWidth == int.MaxValue && windowHeight == int.MaxValue)
            {
                windowWidth = Program.c_defaultMaxWidth;
                windowHeight = Program.c_defaultMaxHeight;
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
                    Stream srcImage = new FileStream(m_localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    // If it needs to be resized, do so
                    if (m_opWidth != m_originalWidth || m_opHeight != m_originalHeight)
                    {
                        using (srcImage)
                        {
                            uploadImage = new MemoryStream();
                            ImageFile.ResizeImage(srcImage, uploadImage, m_opWidth, m_opHeight);
                            uploadImage.Position = 0L;
                        }
                    }
                    else
                    {
                        uploadImage = srcImage;
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
                using (Stream srcImage = new FileStream(m_localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    m_originalFile = m_folder.Upload(srcImage, originalFilename);
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

        private static string FilenameEncode(string title)
        {
            var sb = new StringBuilder();
            foreach (char c in title)
            {
                if (char.IsControl(c) || char.IsPunctuation(c) || char.IsSymbol(c))
                {
                    // Do nothing
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }

    class FileNameEnumerable : IEnumerable<string>
    {
        IEnumerable<string> m_filenamePatterns;
        string m_defaultDirectory;

        public FileNameEnumerable(IEnumerable<string> filenamePatterns, string defaultDirectory = null)
        {
            m_filenamePatterns = filenamePatterns;
            m_defaultDirectory = defaultDirectory ?? Environment.CurrentDirectory;
        }

        public FileNameEnumerable(string filenamePattern, string defaultDirectory = null)
            : this(new string[] { filenamePattern }, defaultDirectory)
        {
        }

        public IEnumerator<string> GetEnumerator()
        {
            return new FileNameEnumerator(m_filenamePatterns.GetEnumerator(), m_defaultDirectory);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private class FileNameEnumerator : IEnumerator<string>
        {
            IEnumerator<string> m_patternEnum;
            IEnumerator<string> m_filenameEnum;
            string m_defaultDirectory;

            public FileNameEnumerator(IEnumerator<string> patternEnum, string defaultDirectory)
            {
                m_patternEnum = patternEnum;
                m_defaultDirectory = defaultDirectory;
            }

            public string Current
            {
                get
                {
                    return m_filenameEnum.Current;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return m_filenameEnum.Current;
                }
            }

            public void Dispose()
            {
                m_patternEnum.Dispose();
            }

            public bool MoveNext()
            {
                if (m_filenameEnum != null)
                {
                    if (m_filenameEnum.MoveNext())
                    {
                        return true;
                    }
                    m_filenameEnum = null;
                }

                while (m_patternEnum.MoveNext())
                {
                    string pattern = m_patternEnum.Current;

                    string directoryPath = Path.GetDirectoryName(pattern);
                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        directoryPath = m_defaultDirectory;
                    }
                    else
                    {
                        directoryPath = Path.GetFullPath(Path.Combine(m_defaultDirectory, directoryPath));
                    }
                    string[] files = Directory.GetFiles(directoryPath, Path.GetFileName(pattern), SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        m_filenameEnum = files.AsEnumerable().GetEnumerator();
                        if (m_filenameEnum.MoveNext()) return true;
                    }
                    else
                    {
                        // TODO: Make this a delegate for use in non-command-line contexts
                        Console.WriteLine("No match found for '{0}'", pattern);
                        Console.WriteLine();
                    }
                }
                return false;
            }

            public void Reset()
            {
                m_filenameEnum = null;
                m_patternEnum.Reset();
            }
        }
    }

}
