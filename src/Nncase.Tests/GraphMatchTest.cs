using System;
using Xunit;
using Nncase.Pattern;
using Nncase.Transform;
using Nncase.IR;
using System.Collections.Generic;
using Nncase.Pattern.Math;
using static Nncase.IR.F.Math;
using static Nncase.IR.F.Tensors;
using static Nncase.Pattern.Utility;
using static Nncase.Pattern.F.Math;
using static Nncase.Pattern.F.Tensors;
using static Nncase.IR.Utility;

namespace Nncase.Tests
{
    using static Nncase.Transform.DataFlowMatcher;

    public class UnitTestDataFlowMatch
    {
        [Fact]
        public void TestMatchCallCommutive()
        {
            Var x = "x", y = "y";
            var addpat = IsBinary(BinaryOp.Add, IsVar(), IsVar());
            Assert.Single(Match(x + y, addpat));
            Assert.Single(Match(y + x, addpat));
            var mulpat = IsBinary(BinaryOp.Mul, IsVar(), IsVar());
            Assert.Single(Match(y * x, mulpat));
            Assert.Single(Match(x * y, mulpat));
        }

        [Fact]
        public void TestMatchNoCallCommutive()
        {
            Var x = "x", y = "y";
            var addpat = IsBinary(BinaryOp.Sub, x, y);
            Assert.Single(Match(x - y, addpat));
            Assert.Empty(Match(y - x, addpat));
            var mulpat = IsBinary(BinaryOp.Div, x, y);
            Assert.Single(Match(x / y, mulpat));
            Assert.Empty(Match(y / x, mulpat));
        }

        [Fact]
        public void TestMatchCall()
        {
            Var x = "x", y = "y";
            var addpat = IsBinary(BinaryOp.Add, IsWildCard(), IsWildCard());
            Assert.Single(Match(x + y, addpat));

            var callpat = IsWildCard();
            Assert.Single(Match(Square(x), callpat));
            Assert.Single(Match(x + y, callpat));
        }

        [Fact]
        public void TestNoMatchFunc()
        {
            Var x = "x", y = "y";
            var pat = IsBinary(BinaryOp.Add, IsWildCard(), IsWildCard());
            Assert.Empty(Match(x - y, pat));
        }

        [Fact]
        public void TestMatchConst()
        {
            Var x = "x", y = "y";
            var pat = IsBinary(BinaryOp.Sub, IsWildCard(), IsConst());
            Assert.Single(Match((x + y) - 100, pat));
        }

        [Fact]
        public void TestMatchTuple()
        {
            Var x = "x", y = "y";
            var z = x + y;
            var tuple = new IR.Tuple(x, y, z);
            var tuplepat = IsTuple(IsVar(), IsWildCard(), IsBinary(BinaryOp.Add, IsWildCard(), IsWildCard()));

            Assert.Single(Match(tuple, tuplepat));

            var tuplepat2 = IsTuple();
            Assert.Single(Match(tuple, tuplepat2));
        }
    }
}