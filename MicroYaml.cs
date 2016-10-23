using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace FmPostToBlogger
{
    /// <summary>
    /// <para>Parses a very small subset of YAML. This form is a set of name-value pairs
    /// where each pair is on a separate line and the name is delimited from the
    /// value with a colon. In YAML terms, this is "plain style." The parser is
    /// designed to be enhanced with Double-Quoted style and Single-Quoted style
    /// in the future.
    /// </para>
    /// <para>This is used primarily for YAML metadata on to MarkDown files in the style
    /// used by Jekyll.
    /// </para>
    /// <para>Presently there are no plans to add lists, nested objects, or other
    /// advanced YAML features. JSON is a beter choice when that complexity is needed.
    /// </para>
    /// </summary>
    static class MicroYaml
    {

        /// <summary>
        /// Parses MicroYaml metadata that begins and ends with lines containing just three dashes.
        /// Leaves the reader positioned at the beginning of the next line after the YAML metadata.
        /// </summary>
        /// <param name="reader">A TextReader containing a document potentially containing
        /// a YAML metadata prefix.</param>
        /// <returns>A dictionary - empty if there was not metadata.</returns>
        /// <remarks>
        /// The YAML prefix follows the style of Jekyll pages. Here's an example of a YAML header
        /// on a MarkDown document:
        /// <code>
        /// ---
        /// title: An Interesting Document
        /// date: 2016-04-19
        /// ---
        /// # Heading
        /// This is a really interesting document.
        /// </code>
        /// </remarks>
        public static Dictionary<string, string> Parse(TextReader reader)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            // Should start with a line containing three dashes and nothing else. If that
            // delimiter line is not found, assume that there is no YAML present.
            if (reader.Peek() != '-') return result;
            reader.Read();
            if (reader.Peek() != '-') return result;
            reader.Read();
            if (reader.Peek() != '-') return result;
            reader.Read();
            if (reader.Peek() == '\r') reader.Read();
            if (reader.Read() != '\n') return result;

            // Main parsing loop. Positioned at the beginning of a line
            for (;;)
            {
                SkipWhitespace(reader);

                int ch = 0;

                // Read the key
                var sb = new StringBuilder();
                for (;;)
                {
                    ch = reader.Read();
                    if (ch < 0 || ch == ':' || ch == '\r' || ch == '\n') break;
                    sb.Append((char)ch);
                }

                if (ch < 0) break; // Syntax error - end of file
                if (ch == '\r' && reader.Peek() == '\n') ch = reader.Read();

                string key = sb.ToString().Trim();

                if (ch != ':')
                {
                    // If end of YAML prefix
                    if (key == "---")
                    {
                        break;
                    }
                    continue; // Syntax error.
                }

                SkipWhitespace(reader);

                // Read the value
                // TODO: Add Double-Quoted Style and Single-Quoted Style Here
                sb = new StringBuilder();
                for (;;)
                {
                    ch = reader.Read();
                    if (ch < 0 || ch == '\r' || ch == '\n') break;
                    sb.Append((char)ch);
                }
                if (ch == '\r' && reader.Peek() == '\n') ch = reader.Read();

                string value = sb.ToString().Trim();

                result[key] = value;
            }

            return result;
        }

        static private int SkipWhitespace(TextReader reader)
        {
            int count = 0;
            for (;;)
            {
                int ch = reader.Peek();
                if (ch != ' ' && ch != '\t') break;
                reader.Read();
                ++count;
            }
            return count;
        }

    }
}
