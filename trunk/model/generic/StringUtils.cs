﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LogJoint
{
	public class StringUtils
	{
		public static string TrimInsignificantSpace(string str)
		{
			return str.Trim(InsignificantSpaces);
		}

		public static bool IsValidCSharpIdentifier(string str)
		{
			return identifierRe.Match(str).Success;
		}

		static readonly char[] InsignificantSpaces = new char[] { '\t', '\n', '\r', ' ' };
		static readonly Regex identifierRe = new Regex(@"^\w+$");

		public static bool IsLetterOrDigit(char c)
		{
			return char.IsLetterOrDigit(c) || c == '_';
		}

		static readonly string[] bytesUnits = new string[] { "B", "KB", "MB", "GB", "TB" };

		public static void FormatBytesUserFriendly(long bytes, StringBuilder outBuffer)
		{
			long divisor = 1;
			int unitIdx = 0;
			int maxUnitIdx = bytesUnits.Length - 1;
			for (; ; )
			{
				if (bytes / divisor < 1024 || unitIdx == maxUnitIdx)
				{
					if (divisor == 1)
						outBuffer.Append(bytes);
					else
						outBuffer.AppendFormat("{0:0.0}", (double)bytes / (double)divisor);
					outBuffer.AppendFormat(" {0}", bytesUnits[unitIdx]);
					break;
				}
				else
				{
					divisor *= 1024;
					++unitIdx;
				}
			}
		}

		public static string FormatBytesUserFriendly(long bytes)
		{
			var buf = new StringBuilder();
			FormatBytesUserFriendly(bytes, buf);
			return buf.ToString();
		}
	}
}
