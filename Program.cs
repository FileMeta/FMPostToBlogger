using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace FmPostToBlogger
{
    class Program
    {
        const string c_googleClientId = "836437994394-2a38027hpt3ao7fdnvjffkkv2rbqr715.apps.googleusercontent.com";
        const string c_googleClientSecret = "wJUdAD-LM7wc5nNKNipVGkCy";

        const string c_googleRefreshToken = "1/AowLkpKBCdL5Ch1wZ-AQ7zYMfLu4SWzNHZmrkbjEzDQ";

        static void Main(string[] args)
        {
            try
            {
                var oauth = new Google.OAuth(c_googleClientId, c_googleClientSecret);

                //Console.WriteLine("Getting authorization from Google. Please respond to the login prompt in your browser.");
                //bool authorized = oauth.Authorize(Google.WebAlbum.OAuthScope);
                //Win32Interop.ConsoleHelper.BringConsoleToFront();

                bool authorized = oauth.Refresh(c_googleRefreshToken);
                if (!authorized)
                {
                    throw new ApplicationException(oauth.Error);
                }

                Console.WriteLine("Authenticated with Google.");
                Console.WriteLine("refresh_token=" + oauth.Refresh_Token);
                Console.WriteLine();

                Environment.ExitCode = PostFromPhoto(oauth, "adventures with julie and brandt", @"C:\Users\brand\Pictures\BlogSamples\IMG_9329.jpg") ? 0 : 1;
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

        static WinShell.PROPERTYKEY s_pkTitle;
        static WinShell.PROPERTYKEY s_pkComment;

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

            // If necessary, get the appropriate property keys
            if (s_pkTitle.pid == 0)
            {
                using (var propSys = new WinShell.PropertySystem())
                {
                    s_pkTitle = propSys.GetPropertyKeyByName("System.Title");
                    s_pkComment = propSys.GetPropertyKeyByName("System.Comment");
                }
            }

            // Use the Windows Property Store to read the title and comments from the image
            string photoTitle;
            string photoComment;
            using (var propStore = WinShell.PropertyStore.Open(filename))
            {
                photoTitle = propStore.GetValue(s_pkTitle) as string;
                photoComment = propStore.GetValue(s_pkComment) as string;
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

            var album = Google.WebAlbum.GetByTitle(oauth.Access_Token, blogName);
            if (album == null)
            {
                Console.WriteLine("Google/Picasa WebAlbum for blog '{0}' not found.", blogName);
                return false;
            }
            Console.WriteLine("Posting photo in album: " + album.Title);

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

            Console.WriteLine("Full res: " + photo.JpegUrl);
            Console.WriteLine("Alternate: " + photo.AlternateUrl);
            Console.WriteLine("Width={0} Height={1}", photo.Width, photo.Height);

            photo.Refresh("800u");
            Console.WriteLine("800 res: " + photo.JpegUrl);
            Console.WriteLine("Alternate: " + photo.AlternateUrl);
            Console.WriteLine("Width={0} Height={1}", photo.Width, photo.Height);

            return true;
        }

    } // Class FmPostToBlogger

}
