using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Xml.Linq;

/* TODO:
   * Embed photo in link to album
   * Transfer geotags if present.
   * Transfer keywords to blog tags.
   * Clean up data dumps.
   * Command-line
       * photo(s) - multiple filenames plus wildcards
       * blog name
       * interactve login or persistent token
       * size (but with default)

    To set up
    * Get baseline css right
    * Prep blog, domain, etc.
*/


namespace FmPostToBlogger
{
    class Program
    {
        const string c_googleClientId = "836437994394-2a38027hpt3ao7fdnvjffkkv2rbqr715.apps.googleusercontent.com";
        const string c_googleClientSecret = "wJUdAD-LM7wc5nNKNipVGkCy";

        const string c_googleRefreshToken = "1/XirnVAK_bKCqm7HNfD_Adw-1ilIRwTSa0-oM6rp-9PM";

        static void Main(string[] args)
        {
            try
            {
                var oauth = new Google.OAuth(c_googleClientId, c_googleClientSecret);

                //Console.WriteLine("Getting authorization from Google. Please respond to the login prompt in your browser.");
                //bool authorized = oauth.Authorize(Google.WebAlbum.OAuthScope, Google.Blog.OAuthScope);
                //Win32Interop.ConsoleHelper.BringConsoleToFront();

                bool authorized = oauth.Refresh(c_googleRefreshToken);
                if (!authorized)
                {
                    throw new ApplicationException(oauth.Error);
                }

                Console.WriteLine("Authenticated with Google.");
                Console.WriteLine("refresh_token=" + oauth.Refresh_Token);
                Console.WriteLine();

                bool success = PostFromPhoto(oauth, "adventures with julie and brandt",
                    @"C:\Users\brand\Pictures\BlogSamples\WP_20161005_020.jpg");

                Environment.ExitCode =  success ? 0 : 1;
                if (success)
                {
                    Console.WriteLine("Success!");
                }
            }
            catch (Exception err)
            {
                Console.WriteLine();
#if DEBUG
                Console.WriteLine(err.ToString());
#else
                Console.WriteLine(err.Message());
#endif
            }

            if (Win32Interop.ConsoleHelper.IsSoleConsoleOwner)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Press any key to exit.");
                Console.ReadKey(true);
            }
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

        private static bool PostFromPhoto(Google.OAuth oauth, string blogName, string filename)
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

            // Find the blog
            var blog = Google.Blog.GetByName(oauth.Access_Token, blogName);
            if (blog == null)
            {
                Console.WriteLine("Blog '{0}' not found.", blogName);
                return false;
            }

            // See if the post already exists
            var blogPost = blog.GetPostByTitle(photoTitle);
            if (blogPost != null)
            {
                Console.WriteLine("Post with title '{0}' already exists in blog '{1}'.", photoTitle, blogName);
                return false;
            }

            var album = Google.WebAlbum.GetByTitle(oauth.Access_Token, blogName);
            if (album == null)
            {
                Console.WriteLine("Google/Picasa WebAlbum for blog '{0}' not found.", blogName);
                return false;
            }
            Console.WriteLine("Adding photo to album: " + album.Title);

            /* In Google/Picasa, the title is really a name or filename so we map things this way
            * | FileMeta  | Google  |
            * |-----------|---------|
            * | filenaeme | title   |
            * | title     | summary |
            * | comment   | n/a     |
            */

            // See if the photo already exists
            var photo = album.GetPhotoBySummary(photoTitle);
            if (photo != null)
            {
                Console.WriteLine("Photo with title '{0}' already exists in album '{1}'.", photo.Summary, blogName);
                return false;
            }

            // Generate a unique name (since the album contents are client-cached, this doesn't require multiple web requests)
            string name;
            {
                string basename = Path.GetFileNameWithoutExtension(filename);
                name = string.Concat(basename, ".jpg");
                if (album.GetPhotoByTitle(name) != null)
                {
                    for (int i=1; true; ++i)
                    {
                        name = string.Concat(basename, "_", i.ToString(), ".jpg");
                        if (album.GetPhotoByTitle(name) == null) break;
                    }
                }
            }

            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
               photo = album.AddPhoto(name, photoTitle, stream);
            }

            /*
            Console.WriteLine("Full res: " + photo.JpegUrl);
            Console.WriteLine("Alternate: " + photo.AlternateUrl);
            Console.WriteLine("Width={0} Height={1}", photo.Width, photo.Height);
            */

            photo.Refresh("800u");
            /*
            Console.WriteLine("800 res: " + photo.JpegUrl);
            Console.WriteLine("Alternate: " + photo.AlternateUrl);
            Console.WriteLine("Width={0} Height={1}", photo.Width, photo.Height);
            */

            Console.WriteLine("Adding new post to blog: " + blog.Name);

            // Compose the post using XML
            string html;
            {
                var doc = new XElement("div",
                    new XElement("a",
                        new XAttribute("href", photo.AlternateUrl),
                        new XElement("img",
                            new XAttribute("src", photo.JpegUrl),
                            new XAttribute("width", photo.Width),
                            new XAttribute("height", photo.Height))),
                    new XElement("br"),
                    photoComment);
                html = doc.ToString();
            }


            blog.AddPost(photoTitle, html, metadata);

            return true;
        }

        public static bool GetLatitudeLongitude(WinShell.PropertyStore store, out double latitude, out double longitude)
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

        static double DegMinSecToDouble(string direction, double[] dms)
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


    } // Class FmPostToBlogger

}
