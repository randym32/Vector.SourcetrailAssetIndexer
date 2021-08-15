/* Parse literals / constants
   Copyright 2008-2021, Randall Maas

    Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

    1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

    2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

    3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System . Collections . Generic;

partial class Token : LexState
{
    /// <summary>
    /// The column or index within the text
    /// </summary>
    internal int EndIdx;

    /// <summary>
    /// The parsed value
    /// </summary>
    internal object Value;
    public Token(object value, LexState s, int idx) : base(s.Idx, s.Line, s.LineStartIdx)
    {
        EndIdx = idx;
        this.Value = value;
    }
}
partial class Token2 : Token
{
    /// <summary>
    /// The key in a dictionary
    /// </summary>
    internal readonly Token key;
    public Token2(Token value, Token key) : base(value.Value, value, value.EndIdx)
    {
        this.key = key;
    }
}

/// <summary>
/// This section provides a tokenizer and precedence to parse expressions of constant values
/// </summary>
internal partial class ParseScalarExpr : ParserBase
{
   /// <summary>
   /// Description of named constants
   /// </summary>
   internal Dictionary<string, object> Constant = new Dictionary<string, object>();


   /// <summary>
   /// Copies this parser and its context, into the destination
   /// </summary>
   /// <param name="Destination"></param>
   /// <returns>The Destination</returns>
   internal ParseScalarExpr Copy(ParseScalarExpr Destination)
   {
      Destination . Constant = new Dictionary<string, object> ( Constant );
      return (ParseScalarExpr) base . Copy (Destination);
   }


   /// <summary>
   /// Scans the input stream to scan named constants, numbers and other lexical tokens
   /// </summary>
   /// <param name="Input">The input stream </param>
   /// <param name="Prec">The precedence structure of the matched token</param>
   /// <returns>The token</returns>
   internal override Token Token(Lexer Input, out TokenPrecedence Prec)
   {
        LexState Start = Input.Save();
        if (Input . Number(out double V))
        {
           Prec = Tok2Prec[Token_Literal];
           return new Token(V, Start, Input.Idx);
        }

      // Check to see if it is a defined constant
      LexState State = Input . Save();
      string   S     = Input . Symbol ();
        if (null != S
           && Constant.TryGetValue(S, out object CV))
        {
            Prec = Tok2Prec[Token_Literal];
            return new Token(CV, Start, Input.Idx);
        }
        Input . Restore(State);


        // Scan each of the tokens to see if they match
        foreach (TokenPrecedence T in TokPrecs)
            if (Input.KeywordMatch(T.Token))
            {
                Prec = T;
                return new Token(T.Token, State, Input.Idx);
            }


        // Pass the buck back to base class
        return base . Token(Input, out Prec);
   }

}
