/*
 *  Authors:  Benton Stark
 * 
 *  Copyright (c) 2007-2009 Starksoft, LLC (http://www.starksoft.com) 
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 * 
 */

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using nUpdate.Administration.TransferInterface;

namespace nUpdate.Administration.Core.Ftp
{
    /// <summary>
    ///     Generic ftp file and directory listing parser that supports most Unix, Dos, and Windows style FTP
    ///     directory listings.  A custom parser can be created using the IFtpItemParser interface in the event
    ///     this parser does not suit the needs of a specific FTP server directory format listing.
    /// </summary>
    public class FtpGenericParser : IFtpItemParser
    {
        // unix regex expressions
        private readonly Regex _dosDate = new Regex(@"(\d\d-\d\d-\d\d)", RegexOptions.Compiled);
        private readonly Regex _dosDir = new Regex(@"<DIR>|\sDIR\s", RegexOptions.Compiled);

        private readonly Regex _dosName =
            new Regex(@"((?<=<DIR>\s+).+)|((?<=\d\d:\d\d\s+).+)|((?<=(\d\d:\d\d(AM|PM|am|pm)\s+\d+\s+)).+)",
                RegexOptions.Compiled);

        private readonly Regex _dosSize =
            new Regex(@"((?<=(\d\d:\d\d\s*(AM|PM|am|pm)\s*))\d+)|(\d+(?=\s+\d\d-\d\d-\d\d\s+))", RegexOptions.Compiled);

        private readonly Regex _dosTime = new Regex(@"(\d\d:\d\d\s*(AM|PM|am|pm))|(\d\d:\d\d)", RegexOptions.Compiled);

        private readonly Regex _isUnix = new Regex(@"(d|l|-|b|c|p|s)(r|w|x|-|t|s){9}", RegexOptions.Compiled);
        private readonly Regex _unixAttribs = new Regex(@"(d|l|-|b|c|p|s)(r|w|x|-|t|s){9}", RegexOptions.Compiled);

        private readonly Regex _unixDay =
            new Regex(@"(?<=(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec|mrt|mei|okt)\s+)\d+",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly Regex _unixMonth = new Regex(@"(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec|mrt|mei|okt)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly Regex _unixName =
            new Regex(
                @"((?<=((Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec|mrt|mei|okt)\s+\d+\s+(19|20)\d\d\s+)|((Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec|mrt|mei|okt)\s+\d+\s+\d+:\d\d\s+)).+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly Regex _unixSize =
            new Regex(@"\d+(?=(\s+(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec|mrt|mei|okt)))",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly Regex _unixSymbLink = new Regex(@"(?<=\s+->\s+).+", RegexOptions.Compiled);

        private readonly Regex _unixTime =
            new Regex(@"(?<=(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec|mrt|mei|okt)\s+\d+\s+)\d+:\d\d",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly Regex _unixType = new Regex(@"(d|l|-|b|c|p|s)(?=(r|w|x|-|t|s){9})", RegexOptions.Compiled);

        private readonly Regex _unixYear =
            new Regex(@"(?<=(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec|mrt|mei|okt)\s+\d+\s+)(19|20)\d\d",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // dos and other expressions


        /// <summary>
        ///     Method to parse a line of file listing data from the FTP server.
        /// </summary>
        /// <param name="line">Line to parse.</param>
        /// <returns>Object representing data in parsed file listing line.</returns>
        public FtpItem ParseLine(string line)
        {
            if (_isUnix.IsMatch(line))
                return ParseUnixFormat(line);
            return ParseDosFormat(line);
        }

        private FtpItem ParseUnixFormat(string line)
        {
            //System.Diagnostics.Debug.Assert(! (line == "-r-xr-xr-x   1 root     root        21969 Apr  8  1986 rfc982.txt"));


            string attribs = _unixAttribs.Match(line).ToString();
            string month = _unixMonth.Match(line).ToString();
            string day = _unixDay.Match(line).ToString();
            string year = _unixYear.Match(line).ToString();
            string time = _unixTime.Match(line).ToString();
            string size = _unixSize.Match(line).ToString();
            string name = _unixName.Match(line).ToString().Trim();
            string symbLink = "";

            // ignore the microsoft 'etc' file that IIS uses for WWW users
            if (name == "~ftpsvc~.ckm")
                return null;

            //  if we find a symbolic link then extract the symbolic link first and then
            //  extract the file name portion
            if (_unixSymbLink.IsMatch(name))
            {
                symbLink = _unixSymbLink.Match(name).ToString();
                name = name.Substring(0, name.IndexOf("->", StringComparison.Ordinal)).Trim();
            }

            string itemType = _unixType.Match(line).ToString();


            //  if the current year is not given in unix then we need to figure it out.
            //  basically, if a date is within the past 6 months unix will show the 
            //  time instead of the year
            if (year.Length == 0)
            {
                int curMonth = DateTime.Today.Month;
                int curYear = DateTime.Today.Year;

                DateTime result;
                if (DateTime.TryParse(String.Format(CultureInfo.InvariantCulture, "1-{0}-2007", month), out result))
                {
                    if ((curMonth - result.Month) < 0)
                        year = Convert.ToString(curYear - 1, CultureInfo.InvariantCulture);
                    else
                        year = curYear.ToString(CultureInfo.InvariantCulture);
                }
            }

            DateTime dateObj;
            DateTime.TryParse(String.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2} {3}", day, month, year, time),
                out dateObj);

            long sizeLng;
            Int64.TryParse(size, out sizeLng);

            var itemTypeObj = FtpItemType.Unknown;
            switch (itemType.ToLower(CultureInfo.InvariantCulture))
            {
                case "l":
                    itemTypeObj = FtpItemType.SymbolicLink;
                    break;
                case "d":
                    itemTypeObj = FtpItemType.Directory;
                    break;
                case "-":
                    itemTypeObj = FtpItemType.File;
                    break;
                case "b":
                    itemTypeObj = FtpItemType.BlockSpecialFile;
                    break;
                case "c":
                    itemTypeObj = FtpItemType.CharacterSpecialFile;
                    break;
                case "p":
                    itemTypeObj = FtpItemType.NamedSocket;
                    break;
                case "s":
                    itemTypeObj = FtpItemType.DomainSocket;
                    break;
            }

            if (itemTypeObj == FtpItemType.Unknown || name.Trim().Length == 0)
                return null;
            return new FtpItem(name, dateObj, sizeLng, symbLink, attribs, itemTypeObj, line);
        }

        private FtpItem ParseDosFormat(string line)
        {
            string name = _dosName.Match(line).ToString().Trim();

            // if the name has no length the simply stop processing and return null.
            if (name.Trim().Length == 0)
                return null;

            string date = _dosDate.Match(line).ToString();
            string time = _dosTime.Match(line).ToString();
            string size = _dosSize.Match(line).ToString();
            string dir = _dosDir.Match(line).ToString().Trim();

            // put togther the date/time
            DateTime dateTime;
            DateTime.TryParse(String.Format(CultureInfo.InvariantCulture, "{0} {1}", date, time), out dateTime);

            // parse the file size
            long sizeLng;
            Int64.TryParse(size, out sizeLng);

            // determine the file item itemType
            FtpItemType itemTypeObj;
            if (dir.Length > 0)
                itemTypeObj = FtpItemType.Directory;
            else
                itemTypeObj = FtpItemType.File;

            return new FtpItem(name, dateTime, sizeLng, String.Empty, String.Empty, itemTypeObj, line);
        }
    }
}