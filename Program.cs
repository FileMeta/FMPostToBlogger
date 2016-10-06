using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FmPostToBlogger
{
    class Program
    {
        const string c_googleClientId = "836437994394-2a38027hpt3ao7fdnvjffkkv2rbqr715.apps.googleusercontent.com";
        const string c_googleClientSecret = "wJUdAD-LM7wc5nNKNipVGkCy";

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Getting authorization from Google. Please respond to the login prompt in your browser.");
                var oauth = new Google.OAuth(c_googleClientId, c_googleClientSecret);
                bool authorized = oauth.Authorize("profile");

                // Bring the Console to Focus.
                Win32Interop.ConsoleHelper.BringConsoleToFront();

                if (authorized)
                {
                    Console.WriteLine("Access_Token: " + oauth.Access_Token ?? "(Null)");
                    Console.WriteLine("Id_Token: " + oauth.Id_Token ?? "(Null)");
                    Console.WriteLine("Refresh_Token: " + oauth.Refresh_Token ?? "(Null)");
                    Console.WriteLine("Expires_In: " + oauth.Expires_In.ToString());
                    Console.WriteLine("Token_Type: " + oauth.Token_Type ?? "(Null)");
                }
                else
                {
                    Console.WriteLine(oauth.Error);
                }
            }
            catch (Exception err)
            {
                Console.WriteLine();
                Console.WriteLine(err.ToString());
            }

            if (Win32Interop.ConsoleHelper.IsSoleConsoleOwner)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Press any key to exit.");
                Console.ReadKey(true);
            }
        }
    }
}
