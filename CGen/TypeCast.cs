using System;
using CodeGeneration;

namespace ABT {
    public sealed partial class TypeCast {
        public override Reg CGenValue(CGenState state) {
            Reg ret = this.Expr.CGenValue(state);
            return ret;
            // Type casting has no effect on the value of the expression.
        }

        public override void CGenAddress(CGenState state) {
            throw new InvalidOperationException("Cannot get the address of a cast expression.");
        }
    }
}