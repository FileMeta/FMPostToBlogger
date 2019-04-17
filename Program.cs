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
   -linkunscaled           Images link to full resolution copies.
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
                bool linkUnscaled = false;
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

                        case "-linkunscaled":
                            linkUnscaled = true;
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
                    blogPoster.LinkUnscaled = linkUnscaled;
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
        public bool LinkUnscaled { get; set; }
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
            ImageUploader photoUploader = new ImageUploader(m_photoFolder);
            photoUploader.IncludeOriginalResolution = LinkUnscaled;
            photoUploader.TargetWidth = MaxWidth;
            photoUploader.TargetHeight = MaxHeight;

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
                // Build the image element
                XElement imageEle = new XElement("img",
                    new XAttribute("src", photoUploader.OpUrl),
                    new XAttribute("width", photoUploader.WebWidth),
                    new XAttribute("height", photoUploader.WebHeight));

                // Wrap with a link to the full res version
                if (LinkUnscaled)
                {
                    imageEle = new XElement("a",
                        new XAttribute("href", photoUploader.OriginalUrl),
                        imageEle);
                }

                // Build the document
                var doc = new XElement("div",
                    imageEle,   
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

            // Manage height and width. Maximums are the values specified in the image tag
            // if present. Otherwise they are the values passed into the class. Regardless
            // the original aspect ratio is always preserved and image are not blown up
            // beyond their original size.
            ImageUploader photoUploader = new ImageUploader(m_photoFolder);
            photoUploader.TargetWidth = GetIntegerAttribute(imageNode, "width");
            photoUploader.TargetHeight = GetIntegerAttribute(imageNode, "height");
            if (photoUploader.TargetWidth == 0 && photoUploader.TargetHeight == 0)
            {
                photoUploader.TargetWidth = MaxWidth;
                photoUploader.TargetHeight = MaxHeight;
            }

            // Load the image
            if (!photoUploader.Load(localPath))
            {
                Console.WriteLine(photoUploader.ErrorMessage);
                return false;
            }

            // Allow the img tag to override default LinkUnscaled value
            bool linkUnscaled = LinkUnscaled;
            {
                string att = imageNode.Attribute("linkunscaled")?.Value;
                if (att != null)
                {
                    linkUnscaled = !string.Equals(att, "false", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(att, "0", StringComparison.OrdinalIgnoreCase);
                }
            }

            photoUploader.IncludeOriginalResolution = linkUnscaled;

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

            // Update the image
            imageNode.SetAttributeValue("src", opUrl);
            imageNode.SetAttributeValue("width", photoUploader.WebWidth);
            imageNode.SetAttributeValue("height", photoUploader.WebHeight);
            imageNode.SetAttributeValue("linkunscaled", null);
            
            // If linkUnscaled, wrap the image in a link
            if (linkUnscaled)
            {
                System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(originalUrl));
                var a = new XElement("a", new XAttribute("href", originalUrl));
                imageNode.AddAfterSelf(a);
                imageNode.Remove();
                a.Add(imageNode);
            }

            return true;
        }

        // Retrieves an integer attribute. Returns zero if not present.
        static int GetIntegerAttribute(XElement imageNode, string attribute)
        {
            var attrib = imageNode.Attribute(attribute);
            if (attrib == null)
            {
                return 0;
            }
            int value;
            if (int.TryParse(attrib.Value, out value))
            {
                return value;
            }
            throw new ArgumentException($"<{imageNode.Name}> {attribute} attribute '{attrib.Value}' is not an integer. (Do not include units.)");
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
