/* JSON Parser
   Copyright 2012-2021, Randall Maas

    Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

    1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

    2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

    3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System . Collections . Generic;

partial class JSONParser: ParseScalarExpr
{
    LexState currentStart;
   Dictionary<string, Token2> currentDict;
   List<Token> currentList;

   internal JSONParser()
   {
      // 
      Add(Token_Literal,  0, _DefND, null);
      Add("[",           80, _ArrayND, null);
      Add("]",            0, _DefND,   null);
      Add("{",           80, _HashND,  null);
      Add("}",            0, _DefND,   null);
      Add(":",           50, _DefND,   _To);
      Add(",",           20, _DefND,   _Comma);
      Constant["true"]=true;
      Constant["false"]=false;
      Constant["null"]=null;
   }

   internal Token Expr(Lexer Input)
   {
      return Expr(Token, Input, 0);
   }

   #region Handy Definitions of Operators
   static Token _DefND(TokenPrecedence.Tokenizer TP, Lexer L, Token T)
   {
      return T;
   }

    Token _ArrayND(TokenPrecedence.Tokenizer Tokenize, Lexer Input, Token T)
   {
      LexState State = Input . Save();
        var tmp = Tokenize(Input, out TokenPrecedence Prec);
        if (tmp.Value is string s && "]" == s)
      {
         return new Token(new List<Token>(), State, Input.Idx);
      }
      Input . Restore(State);

      var      tmpDict = currentDict;
      var      tmpList = currentList;
        var tmpStart = currentStart;
        currentStart = State;
        currentList = new List<Token>();
      currentDict = null;
      var   eRet  = Expr(Tokenize, Input, 0);
      currentList . Add(eRet);

      // Restore the previous dictionary
      var Ret = new Token(currentList, currentStart, Input.Idx);
      currentList = tmpList;
      currentDict = tmpDict;
        currentStart = tmpStart;

      tmp = Tokenize  (Input, out _);
      if (tmp.Value is string s2 && "]" == s2)
      {
            // Update to include the end symbol
            Ret.EndIdx = Input.Idx;
         return Ret;
      }

      Input . Restore(State);
      return null;
   }

    Token _HashND(TokenPrecedence.Tokenizer Tokenize, Lexer Input, Token T)
    {
        LexState State = Input.Save();
        var tmp = Tokenize(Input, out TokenPrecedence Prec);
        if (tmp.Value is string s && "}" == s)
        {
            return new Token(new Dictionary<string, Token2>(), State, Input.Idx);
        }
        Input.Restore(State);
        var tmpDict = currentDict;
        var tmpList = currentList;
        var tmpStart = currentStart;
        currentStart = State;
        currentList = null;
        currentDict = new Dictionary<string, Token2>();
        _ = Expr(Tokenize, Input, 0);

        // Restore the previous dictionary
        var Ret = new Token(currentDict, currentStart, Input.Idx);
        currentList = tmpList;
        currentDict = tmpDict;

        tmp = Tokenize(Input, out _);
        if (tmp.Value is string s2 && "}" == s2)
        {
            // Now return the dictionary
            // Update to include the end symbol
            Ret.EndIdx = Input.Idx;
            return Ret;
        }
        Input.Restore(State);
        return null;
    }
    #endregion

    Token _To(object Op, Token Left, Token Right)
    {
        // perform a key mapping
        if (null == currentDict)
            throw new System.Exception("Colon (:) is used outside of a object/hash table context!");
        if (!(Left.Value is string s))
            throw new System.Exception("LHS of : is expected to be a string, but is not!");
        if (currentDict.ContainsKey(s))
            throw new System.Exception("Thanks kids, but the LHS has been used multiple times");

        currentDict[s] = new Token2(Right, Left);
        return new Token(currentDict, Left, Right.Idx);
    }

    // comma
    Token _Comma (object Op, Token Left, Token Right)
   {
      // perform a key mapping
      if (null != currentDict)
         ;
      else if (null != currentList)
         currentList . Add(Left);
      else
         throw new System.Exception("Comma (,) is used outside of a hash table or list context!");
      return Right;
   }
   
   /// <summary>
   /// Scans the input stream to scan named constants, numbers and other lexical tokens
   /// </summary>
   /// <param name="Input">The input stream </param>
   /// <param name="Prec">The precedence structure of the matched token</param>
   /// <returns>The token</returns>
   internal Token Token(Lexer Input, out TokenPrecedence Prec)
   {
      var C = Input.String();
      if (null != C)
      {
         Prec = Tok2Prec[Token_Literal];
         return C;
      }

      // Pass the buck back to base class
      return base . Token(Input, out Prec);
   }
}
