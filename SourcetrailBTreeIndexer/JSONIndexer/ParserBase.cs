/* Parser
   Copyright 2008-2021, Randall Maas

    Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

    1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

    2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

    3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System . Collections . Generic;


/// <summary>
/// Implements a basic precedence-based parser, with some recursive descent.
/// </summary>
/// <remarks>
/// For                  Use
/// numerical expression Expr
/// (EXPR , )* EXPR      CommaSeparated 
/// </remarks>
internal partial class ParserBase
{

   /// <summary>
   /// This is a recursive descent parser, using operator precedence (eg arithmetic)
   /// </summary>
   /// <param name="Tokenize">Procedure to tokenize the input stream</param>
   /// <param name="Input">The input stream</param>
   /// <param name="RightPrecedence">The predence of the RHS symbol</param>
   /// <returns>The token that was scanned</returns>
   internal Token Expr (TokenPrecedence.Tokenizer Tokenize, Lexer Input, int RightPrecedence)
   {
        LexState State = Input.Save();
        var          T     = Tokenize  (Input, out TokenPrecedence Prec);
      if (null == T)
        {
           Input.Restore(State);
           return null;
        }

      var Left   = Prec.NullDenotation(Tokenize, Input, T);      // Call the null denotation
      while (true)
      {
         State = Input.Save();
         T     = Tokenize  (Input, out Prec);
         if (null == T)
           break;
         if (RightPrecedence >= Prec.Precedence)
           break;

            Left  = Prec.LeftDenotation(T, Left, Expr(Tokenize, Input, Prec.Precedence)); // Call the left denotation
      }
      Input.Restore(State);
      return Left;
   }


   internal delegate Token ExprParse (Lexer Input);

   /// <summary>
   /// This is used to parse a comma-separated list of strings
   ///   (name , )* name
   /// </summary>
   /// <param name="Input">Lexer input</param>
   /// <returns>null if there were no symbols, otherwise a list of the strings</returns>
   internal List<Token> CommaSeparated(Lexer Input, ExprParse SubParse)
   {
      var State = Input . Save();
      var Exprs = new List<Token>();
      do
      {
         var   Expr = SubParse(Input);
         if (null == Expr)
           {
              Input . Restore(State);
              return null;
           }
         Exprs . Add (Expr);
      }
      while (Input . KeywordMatch(","));

      // If there were expressions, return them
      if (Exprs . Count > 0)
        return Exprs;

      // Otherwise restore the stage
      Input . Restore(State);
      return null;
   }
}


