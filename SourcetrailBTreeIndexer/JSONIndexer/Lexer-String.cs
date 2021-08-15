/* Lex C-family strings
   Copyright 2012-2021, Randall Maas

    Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

    1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

    2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

    3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.IO;

partial class Lexer
{
    /// <summary>
    /// Fetches a string of stuff inside of quotes
    /// </summary>
    /// <returns>The symbol</returns>
    internal Token String()
    {
        Preprocess();
        var strStart = Save();
        // Check that we can treat it as a string
        if (Idx >= CA.Length || !('"' == CA[Idx] || '\'' == CA[Idx]))
            return null;

        Idx++;

        int Start = Idx;
        for (; Idx < CA.Length; Idx++)
            if (('"' == CA[Idx] || '\'' == CA[Idx]) && (Start != Idx && '\\' != CA[Idx - 1]))
                break;

        return new Token(new string(CA, Start, Idx++ - Start), strStart, Idx);
    }
}
