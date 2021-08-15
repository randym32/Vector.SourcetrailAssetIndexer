/* Precedence parsing
   Copyright 2008-2021, Randall Maas

    Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

    1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

    2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

    3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System.Collections.Generic;


/// <summary>
/// This adds some support for a single token-precedence table 
/// </summary>
partial class ParserBase
{
   /// <summary>
   /// Use this token reference when working with literal values (eg numbers, quoted strings, etc.)
   /// </summary>
   internal const string Token_Literal  = "(literal)";

   // Default table of operators
   internal List<TokenPrecedence>               TokPrecs = new List<TokenPrecedence>();
   internal Dictionary<string, TokenPrecedence> Tok2Prec = new Dictionary<string,TokenPrecedence>();

   void Add(TokenPrecedence T)
   {
      TokPrecs.Add(T);
      Tok2Prec[T.Token] = T;
   }

   protected void Add(string Token, int P, TokenPrecedence.d_NullDenotation ND, TokenPrecedence.d_LeftDenotation LD)
   {
      TokenPrecedence T = new TokenPrecedence(Token, P, ND, LD);
      Add(T);
   }

   protected ParserBase Copy(ParserBase Destination)
   {
      foreach (TokenPrecedence P in TokPrecs)
       Destination . Add(P);
      return Destination;
   }

   /// <summary>
   /// This is a stub procedure so that classes can inherit from the parser base
   /// class, but offer their own tokenizer for their own purpose
   /// </summary>
   /// <param name="Input"></param>
   /// <param name="Prec"></param>
   /// <returns></returns>
   internal virtual Token Token(Lexer Input, out TokenPrecedence Prec)
   {
      Prec = null;
      return null;
   }
}


/// <summary>
/// Maps a token to the amount it binds with the token on the left.
/// </summary>
/// <remarks>
/// Here is a good starting point for precedence levels:
///    0 non-binding operators like ;
///   10 assignment operators like =
///   20 ?
///   30 || &&
///   40 relational operators like ==
///   50 + -
///   60 * /
///   70 unary operators like !
///   80 . [ (
/// </remarks>
class TokenPrecedence
{
   /// <summary>
   /// The string that is to looked for.  Usually this a keyword or operator
   /// </summary>
   internal string Token;
   /// <summary>
   /// The precedence strength for this token
   /// </summary>
   internal int    Precedence;
   internal delegate Token Tokenizer       (Lexer L, out TokenPrecedence Prec);
   internal delegate Token d_NullDenotation(Tokenizer Tk, Lexer L, Token T);
   internal d_NullDenotation NullDenotation;
   internal delegate Token d_LeftDenotation(object OpToken, Token Left, Token Right);
   internal d_LeftDenotation LeftDenotation;
   internal TokenPrecedence(string A, int P, d_NullDenotation ND, d_LeftDenotation LD)
   {
      Token         = A;
      Precedence    = P;
      NullDenotation= ND;
      LeftDenotation= LD;
   }
}


