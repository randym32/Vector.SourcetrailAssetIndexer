/* Lexer
   Copyright 2008-2021, Randall Maas

    Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

    1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

    2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

    3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System;
using System.IO;

/// <summary>
/// This is used to save the state of parsing
/// </summary>
class LexState
{
   /// <summary>
   /// The column or index within the text
   /// </summary>
   internal int Idx ;

   /// <summary>
   /// The line number
   /// </summary>
   internal int Line;

   /// <summary>
   /// The index where the line starts
   /// </summary>
   internal int LineStartIdx;

   /// <summary>
   /// Creates a state of the lexer object
   /// </summary>
   /// <param name="A"></param>
   /// <param name="line"></param>
   internal LexState(int A, int line, int lineStartIdx)
   {
      Idx = A;
      this . Line= line;
      this . LineStartIdx= lineStartIdx;
   }

   /// <summary>
   /// Determines whether or not "obj" is equal to this object
   /// </summary>
   /// <param name="obj">An object to compare against</param>
   /// <returns>True if they are the same</returns>
   public override bool Equals(object obj)
   {
      if (this == obj)
         return true;
      if (!(obj is LexState))
         return false;
      var b = (LexState) obj;
      return Idx == b.Idx && Line == b.Line && LineStartIdx == b.LineStartIdx;
   }
   
   /// <summary>
   /// Gets the hash code for this object
   /// </summary>
   /// <returns>The hash code</returns>
   public override int GetHashCode()
   {
      int hash = 17;
      hash = hash * 23 + Idx;
      hash = hash * 23 + Line;
      hash = hash * 23 + LineStartIdx;
      return hash;
  }
}

/// <summary>
/// A class to take a string and convert it into tokens
/// </summary>
public partial class Lexer
{
   /// <summary>
   ///  The string being parsed
   /// </summary>
   internal readonly string Str;

   /// <summary>
   /// An array of characters
   /// </summary>
   internal char[] CA;

   /// <summary>
   /// The index of the next characer
   /// </summary>
   internal int    Idx = 0;

   /// <summary>
   /// The line within the file
   /// </summary>
   internal int    Line= 1;

   /// <summary>
   /// The index where the line starts
   /// </summary>
   internal int LineStartIdx;

   /// <summary>
   /// This makes it easy to pass a string as an argument to things,
   /// and automatically convert to their preferred form
   /// </summary>
   /// <param name="str">The string to parse into words</param>
   /// <returns>A lexer</returns>
   public static implicit operator Lexer(string str)
   {
      // Parse the string into words
      return new Lexer(str);
   }

   /// <summary>
   /// Create a lexer to tokenize a string
   /// </summary>
   /// <param name="S"></param>
   public Lexer(string S)
   {
      Str = S;
      CA  = Str.ToCharArray();
   }

   /// <summary>
   /// This creates a lexer that reads from the specified file
   /// </summary>
   /// <param name="Path"></param>
   /// <returns></returns>
   internal static Lexer FromFile(string Path)
   {
      StreamReader In = File.OpenText(Path);
      return new Lexer(In.ReadToEnd());
   }


   /// <summary>
   /// This is true we've reached the end of the file
   /// </summary>
   internal bool EOF { get {return Idx >= CA.Length; }}


   /// <summary>
   /// This is used to save the state of the lexer, in the event the parse
   /// doesn't work out
   /// </summary>
   /// <returns>Lexer state</returns>
   internal LexState Save()
   {
      return new LexState(Idx, Line, LineStartIdx);
   }


   /// <summary>
   /// This is used to restore to a different parse state if the parse didn't
   /// work
   /// </summary>
   /// <param name="State">Previously saved lexer state</param>
   internal void Restore(LexState State)
   {
      Idx = State.Idx ;
      Line= State.Line;
      LineStartIdx = State.LineStartIdx;
   }


   /// <summary>
   /// Returns the remaining unprocessed text
   /// </summary>
   /// <returns>The text to the end of the line</returns>
   internal string ToEndOfLine()
   {
      return Str.Substring(Idx);
   }


    /// <summary>
    /// Skips the text to the end of the line
    /// </summary>
    void SkipToEOL()
    {
        while (Idx < CA.Length && '\n' != CA[Idx])
            Idx++;
        if (Idx >= CA.Length || '\n' != CA[Idx])
            return;
    }


    /// <summary>
    /// Skips the text to the end of */ comment
    /// </summary>
    void SkipToEndOfComment()
    {
        while (Idx < CA.Length)
        {
            if ('*' != CA[Idx++])
                continue;

            if ('/' == CA[Idx])
            {
                Idx++;
                break;
            }
        }
    }

    /// <summary>
    /// Preprocess the string.  This skips white space
    /// </summary>
    internal void Preprocess()
    {
        while (Idx < CA.Length)
        {
            // Get the next character
            char Ch1 = CA[Idx++];
            // Try to handle the carriage return junk
            if ('\n' == Ch1)
            {
                Line++;
                LineStartIdx = Idx;
            }
            if ('\r' == Ch1)
            {
                LineStartIdx = Idx;
            }

            // Skip white space
            if (char.IsWhiteSpace(Ch1))
                continue;
            // Skip C comments
            if ('/' == Ch1)
            {
                char Ch2 = CA[Idx++];
                if ('/' == Ch2)
                {
                    SkipToEOL();
                    continue;
                }
                if ('*' == Ch2)
                {
                    SkipToEndOfComment();
                    continue;
                }
                Idx -= 2;
                break;
            }
            Idx--;
            break;
        }
    }

   /// <summary>
   /// Fetches a string of letters, numbers, and underscores
   /// </summary>
   /// <returns>The symbol</returns>
   internal string Symbol()
   {
      Preprocess();
      if (Idx >= CA.Length || !(char.IsLetter(CA[Idx]) || '_' == CA[Idx]))
        return null;

      int Start = Idx++;
      for (; Idx < CA.Length; Idx++)
       if (!(char.IsLetterOrDigit(CA[Idx]) || '_' == CA[Idx]))
         break;

      return new string(CA, Start, Idx-Start);
   }

   /// <summary>
   /// This is used to tokenize a number with a leading zero.
   /// The convention is that the leading zero may indicate another
   /// base
   /// </summary>
   /// <param name="Val"></param>
   /// <returns></returns>
   bool Number0(out int Val)
   {
      Val=0;
      var orig = Idx;
      Idx++;
      if (Idx >= CA.Length)
        return true;
      if ('x' == char.ToLower(CA[Idx]))
        {
           Idx++;
           while (Idx < CA.Length)
            if (char.IsDigit(CA[Idx]))
              Val = Val*16 + CA[Idx++] - '0';
             else if (CA[Idx] >= 'a' && CA[Idx] <= 'f')
              Val = Val*16 + 10 + CA[Idx++] - 'a';
             else if (CA[Idx] >= 'A' && CA[Idx] <= 'F')
              Val = Val*16 + 10 + CA[Idx++] - 'A';
             else
              return true;
        }

      while (Idx < CA.Length && CA[Idx] >= '0' && CA[Idx] <= '7')
       Val = Val*16 + CA[Idx++] - '0';

      // See if what was a standalone zero
      if (Idx-orig == 1 && (Idx >= CA.Length || Char . IsWhiteSpace(CA[Idx])))
         return true;

      if (Idx < CA.Length && !Char . IsDigit(CA[Idx]))
        return false;

      return true;
   }

    /// <summary>
    /// Parses a number; if the number starts with
    /// a 0x it will be treated as hex; if it starts with a zero it will be treated as a octal number
    /// </summary>
    /// <param name="Val">The resulting value</param>
    /// <returns>false if it is not a number, true if it is</returns>
    internal bool Integer(out double Val, bool useLeadingZero= true)
    {
        Preprocess();
        Val = 0;
        if (CA.Length <= Idx)
            return false;
        char Ch = CA[Idx];
        float sign = 1;
        if ('-'==Ch)
        {
            sign = -1;
            Idx++;
            Ch = CA[Idx];
        }
        if (!char.IsDigit(Ch))
            return false;

        if ('0' == Ch && useLeadingZero)
        {
            if (Number0(out int iVal))
            {
                Val = sign * iVal;
                return true;
            }
            Val = 0;
            return true;
        }

        while (Idx < CA.Length && char.IsDigit(CA[Idx]))
            Val = Val * 10 + CA[Idx++] - '0';

        // Skip any suffix
        if (Idx < CA.Length && ('u' == CA[Idx] || 'U' == CA[Idx]|| 'f' == CA[Idx] || 'F' == CA[Idx]||'l' == CA[Idx] || 'L' == CA[Idx]))
            Idx++;
        Val *= sign;
        return true;
    }

    /// <summary>
    /// Parses a number; if the number starts with
    /// a 0x it will be treated as hex; if it starts with a zero it will be treated as a octal number
    /// </summary>
    /// <param name="Val">The resulting value</param>
    /// <returns>false if it is not a number, true if it is</returns>
    internal bool Number(out double Val)
    {
        var start = Idx;
        if (!Integer(out Val))
        {
            Idx = start;
            return false;
        }
        if (CA.Length <= Idx)
            return true;
        char Ch = CA[Idx];
        // Check for a decimal number
        if ('.' == Ch)
        {
            var tmp = Idx;
            Idx++;
            if (!Integer(out double right, false))
            {
                Idx = tmp+1;
                return true;
            }
            // Count the digits so we can divide right
            right /= Math.Pow(10,Idx-tmp-1);
            Val += right;
        }
        if (CA.Length <= Idx)
            return true;
        Ch = CA[Idx];
        // Check for a power in scientific notation
        if ('e' == Ch)
        {
            var tmp = Idx;
            Idx++;
            if ('+' == CA[Idx])
            {
                Idx++;
            }

            if (!Integer(out double right))
            {
                Idx = tmp;
                return true;
            }
            // The exponent
            Val *= Math.Pow(10,right);
        }

        return true;
    }


    /// <summary>
    /// Attempts to have the next token match the given literal string
    /// </summary>
    /// <param name="Keyword"></param>
    /// <returns></returns>
    internal bool KeywordMatch(string Keyword)
    {
        Preprocess();
        char[] Key = Keyword.ToCharArray();
        int Jdx = 0, OrigIdx = Idx;

        while (Idx < CA.Length)
        {
            if (Jdx >= Key.Length)
            {
                if (char.IsLetterOrDigit(Keyword[Jdx - 1]) || '_' == Keyword[Jdx - 1])
                {
                    if (char.IsLetterOrDigit(CA[Idx]) || '_' == CA[Idx])
                        break;
                }
                return true;
            }
            if (Keyword[Jdx] != CA[Idx])
                break;
            Jdx++; Idx++;
        }
        if (Jdx >= Key.Length)
            return true;
        Idx = OrigIdx;
        return false;
    }
}
