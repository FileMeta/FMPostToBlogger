using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Collections;

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
   -imgwidth               Width in web pixels to use on the image for JPEG
                           posts. Default width used with MarkDown posts.
                           Default is 800. (See details below.)           
   -draft                  Post in draft form. You can log into Blogger and
                           view or edit the post before publishing it.
   -dryrun                 Do a dry run. Connect with the blog, report
                           metadata, and check for conflicts with existing
                           posts but do not actually post to the blog.

Filenames:
   Each file will be converted into a blog post. Multiple file names may be
   included and wildcards are supported. Presently only JPEG photos (with a
   .jpg or .jpeg extension) are supported. A future relsase will add support
   for MarkDown format files.

Examples:
   FMPostToBlogger -authint -blog ""There and Back Again"" Bilbo.jpg Gandalf.jpg
           This will interactively log into Google (using your web browser) and
           then post two photos, Bilbo.jpg and Gandalf.jpg on the blog titled
           ""There and Back Again"".

   FMPostToBlogger -refresh_token 1/XirnVAK -blog ""Asgard"" Thor.jpg Sif.jpg
           This will use a refresh token (presumably from a previous run) to
           authenticate and then post Thor.jpg and Sif.jpg on the blog titled
           ""Asgard"".          

JPEG Details:
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

Image Widths:
   The -imgwidth tag indicates the width of the image in pixels as
   interpreted by the web browser. You should choose a value that works
   well with your blog layout. With the advent of high resolution
   displays, browser pixels may be more course than actual image pixels.
   Accordingly, FMPostToBlogger will request a link from the Google Web Album
   service that delivers an image approximately double the horizontal
   resolution specified by -imagewidth those offering high quality images
   on all devices.

Description:
   FMPostToBlogger is a command-line utility that makes Google Blogger posts
   out of photos. MarkDown files will be supported soon.

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

        const string c_googleClientId = "836437994394-2a38027hpt3ao7fdnvjffkkv2rbqr715.apps.googleusercontent.com";
        const string c_googleClientSecret = "wJUdAD-LM7wc5nNKNipVGkCy";
        const int c_defaultImgWidth = 800;

        static void Main(string[] args)
        {
            try
            {
                // Parse the command line
                bool showSyntax = false;
                bool authInteractive = false;
                string refreshToken = null;
                string blogName = null;
                int imgWidth = c_defaultImgWidth;
                List<string> filenames = new List<string>();
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

                        case "-imgwidth":
                            ++i;
                            if (!int.TryParse(args[i], out imgWidth))
                            {
                                throw new ArgumentException("Value for -imgwidth is not an integer: " + args[i]);
                            }
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
                        authorized = oauth.Authorize(Google.WebAlbum.OAuthScope, Google.Blog.OAuthScope);
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
                    blogPoster.ImgWidth = imgWidth;
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
                Console.WriteLine(err.Message());
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
        Google.WebAlbum m_album;

        public BlogPoster(string accessToken)
        {
            m_accessToken = accessToken;
            ImgWidth = 640;
        }

        public int ImgWidth { get; set; }
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

            // Find the corresponding album
            m_album = Google.WebAlbum.GetByTitle(m_accessToken, blogName);
            if (m_album == null)
            {
                Console.WriteLine("Google/Picasa WebAlbum for blog '{0}' not found.", blogName);
                return false;
            }

            return true;
        }

        // Property keys retrieved from https://msdn.microsoft.com/en-us/library/windows/desktop/dd561977(v=vs.85).aspx
        static WinShell.PROPERTYKEY s_pkTitle = new WinShell.PROPERTYKEY("F29F85E0-4FF9-1068-AB91-08002B27B3D9", 2); // System.Title
        static WinShell.PROPERTYKEY s_pkComment = new WinShell.PROPERTYKEY("F29F85E0-4FF9-1068-AB91-08002B27B3D9", 6); // System.Comment
        static WinShell.PROPERTYKEY s_pkKeywords = new WinShell.PROPERTYKEY("F29F85E0-4FF9-1068-AB91-08002B27B3D9", 5); // System.Keywords
        static WinShell.PROPERTYKEY s_pkDateTaken = new WinShell.PROPERTYKEY("14B81DA1-0135-4D31-96D9-6CBFC9671A99", 36867); // System.Photo.DateTaken
        static WinShell.PROPERTYKEY s_pkLatitude = new WinShell.PROPERTYKEY("8727CFFF-4868-4EC6-AD5B-81B98521D1AB", 100); // System.GPS.Latitude
        static WinShell.PROPERTYKEY s_pkLatitudeRef = new WinShell.PROPERTYKEY("029C0252-5B86-46C7-ACA0-2769FFC8E3D4", 100); // System.GPS.LatitudeRef
        static WinShell.PROPERTYKEY s_pkLongitude = new WinShell.PROPERTYKEY("C4C4DBB2-B593-466B-BBDA-D03D27D5E43A", 100); // System.GPS.Longitude
        static WinShell.PROPERTYKEY s_pkLongitudeRef = new WinShell.PROPERTYKEY("33DCF22B-28D5-464C-8035-1EE9EFD25278", 100); // System.GPS.LongitudeRef

        public bool PostFromPhoto(string filename)
        {
            {
                string extension = Path.GetExtension(filename);
                if (!extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Image to post must be a JPEG file. '{0}' is not.", Path.GetFileName(filename));
                    return false;
                }
            }

            // Use the Windows Property Store to read the title and comments from the image
            string photoTitle;
            string photoComment;
            var metadata = new Google.BlogPostMetadata();
            using (var propStore = WinShell.PropertyStore.Open(filename))
            {
                photoTitle = propStore.GetValue(s_pkTitle) as string;
                photoComment = propStore.GetValue(s_pkComment) as string;
                metadata.PublishedDate = propStore.GetValue(s_pkDateTaken) as DateTime? ?? new DateTime((long)0);
                metadata.UpdatedDate = DateTime.UtcNow;
                double photoLatitude;
                double photoLongitude;
                if (GetLatitudeLongitude(propStore, out photoLatitude, out photoLongitude))
                {
                    metadata.Latitude = photoLatitude;
                    metadata.Longitude = photoLongitude;
                }
                metadata.Labels = propStore.GetValue(s_pkKeywords) as string[];
            }

            if (string.IsNullOrEmpty(photoTitle))
            {
                Console.WriteLine("JPEG photo must have a title in the metadata to pe posted to the blog.\r\nOne method is to use the 'Properties-Details' function in Windows File Explorer.");
                return false;
            }
            if (photoComment == null)
            {
                photoComment = string.Empty;
            }

            // See if the post already exists
            var blogPost = m_blog.GetPostByTitle(photoTitle);
            if (blogPost != null)
            {
                Console.WriteLine("Post with title '{0}' already exists in blog '{1}'.", photoTitle, m_blog.Name);
                return false;
            }

            /* In Google/Picasa, the title is really a name or filename so we map things this way
            * | FileMeta  | Google  |
            * |-----------|---------|
            * | filenaeme | title   |
            * | title     | summary |
            * | comment   | n/a     |
            */

            Console.WriteLine("Adding photo to album: " + m_album.Title);

            // See if the photo already exists
            var photo = m_album.GetPhotoBySummary(photoTitle);
            if (photo != null)
            {
                Console.WriteLine("Photo with title '{0}' already exists in album '{1}'.", photo.Summary, m_album.Title);
                return false;
            }

            // Generate a unique name (since the album contents are client-cached, this doesn't require multiple web requests)
            string name;
            {
                string basename = Path.GetFileNameWithoutExtension(filename);
                name = string.Concat(basename, ".jpg");
                if (m_album.GetPhotoByTitle(name) != null)
                {
                    for (int i = 1; true; ++i)
                    {
                        name = string.Concat(basename, "_", i.ToString(), ".jpg");
                        if (m_album.GetPhotoByTitle(name) == null) break;
                    }
                }
            }

            if (DryRun)
            {
                Console.WriteLine("Dry run. Not posting.");
                Console.WriteLine("Filename: " + Path.GetFileName(filename));
                Console.WriteLine("Title: " + photoTitle);
                Console.WriteLine("Comment: " + photoComment);
                Console.WriteLine("Publish Date: " + metadata.PublishedDate.ToString("s"));
                if (metadata.Latitude != 0.0 && metadata.Longitude != 0.0)
                {
                    Console.WriteLine("Latitude;Longitude: {0:r};{1:r}", metadata.Latitude, metadata.Longitude);
                }
                if (metadata.Labels != null)
                {
                    Console.WriteLine("Labels: " + string.Join(";", metadata.Labels));
                }
                return true;
            }

            // Add the photo to the album.
            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                photo = m_album.AddPhoto(name, photoTitle, stream);
            }

            /*
            Console.WriteLine("Full res: " + photo.JpegUrl);
            Console.WriteLine("Alternate: " + photo.AlternateUrl);
            Console.WriteLine("Width={0} Height={1}", photo.Width, photo.Height);
            */

            photo.Refresh(ImgWidth * 2);
            /*
            Console.WriteLine("800 res: " + photo.JpegUrl);
            Console.WriteLine("Alternate: " + photo.AlternateUrl);
            Console.WriteLine("Width={0} Height={1}", photo.Width, photo.Height);
            */

            // Add the post to the blog
            Console.WriteLine("Adding new post to blog: " + m_blog.Name);

            // Calculate the height that preserves aspect ratio
            int propHeight = (ImgWidth * photo.Height) / photo.Width;

            // Compose the post using XML
            string html;
            {
                var doc = new XElement("div",
                    new XElement("a",
                        new XAttribute("href", photo.AlternateUrl),
                        new XElement("img",
                            new XAttribute("src", photo.JpegUrl),
                            new XAttribute("width", ImgWidth),
                            new XAttribute("height", propHeight))),
                    new XElement("br"),
                    photoComment);
                html = doc.ToString();
            }

            m_blog.AddPost(photoTitle, html, metadata, DraftMode);

            Console.WriteLine(DraftMode ? "Posted as draft!" : "Posted!");

            return true;
        }

        private static bool GetLatitudeLongitude(WinShell.PropertyStore store, out double latitude, out double longitude)
        {
            latitude = longitude = 0.0;

            double[] angle = (double[])store.GetValue(s_pkLatitude);
            string direction = (string)store.GetValue(s_pkLatitudeRef);
            if (angle == null || direction == null) return false;
            //Debug.WriteLine("Latitude: {0} {1},{2},{3}", direction, angle[0], angle[1], angle[2]);
            latitude = DegMinSecToDouble(direction, angle);

            angle = (double[])store.GetValue(s_pkLongitude);
            direction = (string)store.GetValue(s_pkLongitudeRef);
            if (angle == null || direction == null) return false;
            //Debug.WriteLine("Longitude: {0} {1},{2},{3}", direction, angle[0], angle[1], angle[2]);
            longitude = DegMinSecToDouble(direction, angle);

            return true;
        }

        private static double DegMinSecToDouble(string direction, double[] dms)
        {
            double result = dms[0];
            if (dms.Length > 1) result += dms[1] / 60.0;
            if (dms.Length > 2) result += dms[2] / 3600.0;

            if (string.Equals(direction, "W", StringComparison.OrdinalIgnoreCase) || string.Equals(direction, "S", StringComparison.OrdinalIgnoreCase))
            {
                result = -result;
            }

            return result;
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
