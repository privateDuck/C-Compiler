using System;
using System.Diagnostics;
using CodeGeneration;

namespace ABT {
    public abstract partial class IncDecExpr {

        // Integral
        // Before the actual calculation, the state is set to this.
        // 
        // regs:
        // ax = expr
        // bx = expr
        // cx = &expr
        // (Yes, both ax and bx are expr.)
        // 
        // stack:
        // +-------+
        // | ..... | <- sp
        // +-------+
        // 
        // After the calculation, the result should be in ax,
        // and memory should be updated.
        //
        public abstract void CalcAndSaveLong(CGenState state);

        public abstract void CalcAndSaveWord(CGenState state);

        public abstract void CalcAndSaveByte(CGenState state);

        public abstract void CalcAndSavePtr(CGenState state);

        // Float
        // Before the actual calculation, the state is set to this.
        // 
        // regs:
        // cx = &expr
        // 
        // stack:
        // +-------+
        // | ..... | <- %esp
        // +-------+
        // 
        // float stack:
        // +-------+
        // | expr  | <- %st(1)
        // +-------+
        // |  1.0  | <- %st(0)
        // +-------+
        // 
        // After the calculation, the result should be in %st(0),
        // and memory should be updated.
        // 
        public abstract void CalcAndSaveFloat(CGenState state);

        public abstract void CalcAndSaveDouble(CGenState state);

        public override sealed Reg CGenValue(CGenState state) {

            // 1. Get the address of expr.
            // 
            // regs:
            // %eax = &expr
            // 
            // stack:
            // +-------+
            // | ..... | <- %esp
            // +-------+
            // 
            this.Expr.CGenAddress(state);

            // 2. Push address.
            // 
            // regs:
            // %eax = &expr
            // 
            // stack:
            // +-------+
            // | ..... |
            // +-------+
            // | &expr | <- %esp
            // +-------+
            // 
            Int32 stack_size = state.CGenPushLong(Reg.EAX);

            // 3. Get current Value of expr.
            // 
            // 1) If expr is an integral or pointer:
            // 
            // regs:
            // %eax = expr
            // 
            // stack:
            // +-------+
            // | ..... |
            // +-------+
            // | &expr | <- %esp
            // +-------+
            // 
            // 
            // 2) If expr is a float:
            // 
            // regs:
            // %eax = &expr
            // 
            // stack:
            // +-------+
            // | ..... |
            // +-------+
            // | &expr | <- %esp
            // +-------+
            // 
            // float stack:
            // +-------+
            // | expr  | <- %st(0)
            // +-------+
            // 
            Reg ret = this.Expr.CGenValue(state);

            switch (ret) {
                case Reg.EAX:
                    // expr is an integral or pointer.

                    // 4. Pop address to %ecx.
                    // 
                    // regs:
                    // %eax = expr
                    // %ecx = &expr
                    // 
                    // stack:
                    // +-------+
                    // | ..... | <- %esp
                    // +-------+
                    // 
                    state.CGenPopLong(stack_size, Reg.ECX);

                    // 5. Cache current Value of Expr in %ebx.
                    // 
                    // regs:
                    // %eax = expr
                    // %ebx = expr
                    // %ecx = &expr
                    // 
                    // stack:
                    // +-------+
                    // | ..... | <- %esp
                    // +-------+
                    // 
                    state.MOV(Reg.EAX, Reg.EBX);

                    // 6. Calculate the new value in %ebx or %eax and save.
                    //    Set %eax to be the return Value.
                    // 
                    // regs:
                    // %eax = expr or (expr +- 1)
                    // %ebx = (expr +- 1) or expr
                    // %ecx = &expr
                    // 
                    // stack:
                    // +-------+
                    // | ..... | <- %esp
                    // +-------+
                    // 
                    switch (this.Expr.Type.Kind) {
                        case ExprTypeKind.CHAR:
                        case ExprTypeKind.UCHAR:
                            CalcAndSaveByte(state);
                            return Reg.EAX;

                        case ExprTypeKind.SHORT:
                        case ExprTypeKind.USHORT:
                            CalcAndSaveWord(state);
                            return Reg.EAX;

                        case ExprTypeKind.LONG:
                        case ExprTypeKind.ULONG:
                            CalcAndSaveByte(state);
                            return Reg.EAX;

                        case ExprTypeKind.POINTER:
                            CalcAndSavePtr(state);
                            return Reg.EAX;

                        default:
                            throw new InvalidProgramException();
                    }

                case Reg.ST0:
                    throw new InvalidProgramException("floats and structs are not supported");

                default:
                    throw new InvalidProgramException();
            }

        }

        public override sealed void CGenAddress(CGenState state) {
            throw new InvalidOperationException(
                "Cannot get the address of an increment/decrement expression."
            );
        }
    }

    public sealed partial class PostIncrement {
        public override void CalcAndSaveLong(CGenState state) {
            state.ADDL(1, Reg.EBX);
            state.MOV(Reg.EBX, 0, Reg.ECX);
        }

        public override void CalcAndSaveWord(CGenState state) {
            state.ADDL(1, Reg.EBX);
            state.MOV(Reg.EBX, 0, Reg.ECX);
        }

        public override void CalcAndSaveByte(CGenState state) {
            state.ADDL(1, Reg.EBX);
            state.MOV(Reg.EBX, 0, Reg.ECX);
        }

        public override void CalcAndSavePtr(CGenState state) {
            state.ADDL(this.Expr.Type.SizeOf, Reg.EBX);
            state.MOV(Reg.EBX, 0, Reg.ECX);
        }

        public override void CalcAndSaveFloat(CGenState state) {
            throw new InvalidProgramException("floats and structs are not supported");
        }

        public override void CalcAndSaveDouble(CGenState state) {
            throw new InvalidProgramException("floats and structs are not supported");
        }
    }

    public sealed partial class PostDecrement {
        public override void CalcAndSaveLong(CGenState state) {
            state.SUBL(1, Reg.EBX);
            state.MOV(Reg.EBX, 0, Reg.ECX);
        }

        public override void CalcAndSaveWord(CGenState state) {
            state.SUBL(1, Reg.EBX);
            state.MOV(Reg.EBX, 0, Reg.ECX);
        }

        public override void CalcAndSaveByte(CGenState state) {
            state.SUBL(1, Reg.EBX);
            state.MOV(Reg.EBX, 0, Reg.ECX);
        }

        public override void CalcAndSavePtr(CGenState state) {
            state.SUBL(this.Expr.Type.SizeOf, Reg.EBX);
            state.MOV(Reg.EBX, 0, Reg.ECX);
        }

        public override void CalcAndSaveFloat(CGenState state) {
            throw new InvalidProgramException("floats and structs are not supported");
        }

        public override void CalcAndSaveDouble(CGenState state) {
            throw new InvalidProgramException("floats and structs are not supported");
        }
    }

    public sealed partial class PreIncrement {
        public override void CalcAndSaveLong(CGenState state) {
            state.ADDL(1, Reg.EAX);
            state.MOV(Reg.EAX, 0, Reg.ECX);
        }

        public override void CalcAndSaveWord(CGenState state) {
            state.ADDL(1, Reg.EAX);
            state.MOV(Reg.EAX, 0, Reg.ECX);
        }

        public override void CalcAndSaveByte(CGenState state) {
            state.ADDL(1, Reg.EAX);
            state.MOV(Reg.EAX, 0, Reg.ECX);
        }

        public override void CalcAndSavePtr(CGenState state) {
            state.ADDL(this.Expr.Type.SizeOf, Reg.EAX);
            state.MOV(Reg.EAX, 0, Reg.ECX);
        }

        public override void CalcAndSaveFloat(CGenState state) {
            throw new InvalidProgramException("floats and structs are not supported");
        }

        public override void CalcAndSaveDouble(CGenState state) {
            throw new InvalidProgramException("floats and structs are not supported");
        }
    }

    public sealed partial class PreDecrement {
        public override void CalcAndSaveLong(CGenState state) {
            state.SUBL(1, Reg.EAX);
            state.MOV(Reg.EAX, 0, Reg.ECX);
        }

        public override void CalcAndSaveWord(CGenState state) {
            state.SUBL(1, Reg.EAX);
            state.MOV(Reg.EAX, 0, Reg.ECX);
        }

        public override void CalcAndSaveByte(CGenState state) {
            state.SUBL(1, Reg.EAX);
            state.MOV(Reg.EAX, 0, Reg.ECX);
        }

        public override void CalcAndSavePtr(CGenState state) {
            state.SUBL(this.Expr.Type.SizeOf, Reg.EAX);
            state.MOV(Reg.EAX, 0, Reg.ECX);
        }

        public override void CalcAndSaveFloat(CGenState state) {
            throw new InvalidProgramException("floats and structs are not supported");
        }

        public override void CalcAndSaveDouble(CGenState state) {
            throw new InvalidProgramException("floats and structs are not supported");
        }
    }

    public abstract partial class UnaryArithOp {
        public override sealed void CGenAddress(CGenState state) {
            throw new InvalidOperationException(
                "Cannot get the address of an unary arithmetic operator."
            );
        }
    }

    public sealed partial class Negative {
        public override Reg CGenValue(CGenState state) {
            Reg ret = this.Expr.CGenValue(state);
            switch (ret) {
                case Reg.EAX:
                    state.NEG(Reg.EAX);
                    return Reg.EAX;

                case Reg.ST0:
                    throw new InvalidProgramException("floats and structs are not supported");

                default:
                    throw new InvalidProgramException();
            }
        }
    }

    public sealed partial class BitwiseNot {
        public override Reg CGenValue(CGenState state) {
            Reg ret = this.Expr.CGenValue(state);
            if (ret != Reg.EAX) {
                throw new InvalidProgramException();
            }
            state.NOT(Reg.EAX);
            return Reg.EAX;
        }
    }

    public sealed partial class LogicalNot {
        public override Reg CGenValue(CGenState state) {
            Reg ret = this.Expr.CGenValue(state);
            switch (ret) {
                case Reg.EAX:
                    state.CMPL(Reg.EAX, Reg.EAX);
                    return Reg.EAX;

                // FP comparison is not supported.
                case Reg.ST0:
                default:
                    throw new Exception("FP comparison is not supported");
            }
        }
    }
}
